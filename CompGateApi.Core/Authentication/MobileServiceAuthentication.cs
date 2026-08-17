using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CompGateApi.Core.Authentication;

public sealed class MobileServiceTokenOptions
{
    public const string SectionName = "ServiceTokens";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string RequiredScope { get; init; } = "company-gateway.mobile";
    public string PublicKeyPem { get; init; } = string.Empty;
    public IDictionary<string, MobileServiceClientOptions> Clients { get; init; } =
        new Dictionary<string, MobileServiceClientOptions>(StringComparer.Ordinal);
}

public sealed class MobileServiceClientOptions
{
    public string AllowedScopes { get; init; } = "company-gateway.mobile";
}

public static class MobileServiceAuthenticationDefaults
{
    public const string Scheme = "MobileBffService";
    public const string HeaderName = "X-Service-Authorization";
    public const string RequireMobileBffServicePolicy = "RequireMobileBffService";
    public const string RequireCompanyUserAndMobileBffServicePolicy =
        "RequireCompanyUserAndMobileBffService";
}

public interface IMobileServiceTokenValidator
{
    ClaimsPrincipal Validate(string token);
}

public sealed class MobileServiceTokenValidator(
    IOptionsMonitor<MobileServiceTokenOptions> options) : IMobileServiceTokenValidator
{
    public ClaimsPrincipal Validate(string token)
    {
        var settings = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(settings.Issuer) ||
            string.IsNullOrWhiteSpace(settings.Audience) ||
            string.IsNullOrWhiteSpace(settings.Environment) ||
            string.IsNullOrWhiteSpace(settings.RequiredScope) ||
            string.IsNullOrWhiteSpace(settings.PublicKeyPem))
        {
            throw new MobileServiceTokenConfigurationException();
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(settings.PublicKeyPem.Replace("\\n", "\n", StringComparison.Ordinal));
        }
        catch (CryptographicException)
        {
            throw new MobileServiceTokenConfigurationException();
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            },
            out _);

        var clientId = principal.FindFirst("client_id")?.Value;
        var scopes = ParseScopes(principal.FindFirst("scope")?.Value);
        var client = !string.IsNullOrWhiteSpace(clientId) &&
                     settings.Clients.TryGetValue(clientId, out var configured)
            ? configured
            : null;
        if (!string.Equals(principal.FindFirst("token_type")?.Value, "service", StringComparison.Ordinal) ||
            client is null ||
            !string.Equals(
                principal.FindFirst("environment")?.Value,
                settings.Environment,
                StringComparison.Ordinal) ||
            !scopes.Contains(settings.RequiredScope) ||
            !scopes.IsSubsetOf(ParseScopes(client.AllowedScopes)))
        {
            throw new SecurityTokenValidationException(
                "The service token does not contain the required identity claims.");
        }

        return principal;
    }

    private static HashSet<string> ParseScopes(string? value) =>
        (value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
}

public sealed class MobileServiceAuthenticationHandler :
    AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IMobileServiceTokenValidator _validator;

    public MobileServiceAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IMobileServiceTokenValidator validator)
        : base(options, logger, encoder)
    {
        _validator = validator;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var value = Request.Headers[MobileServiceAuthenticationDefaults.HeaderName]
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(value, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "The service authorization header is invalid."));
        }

        try
        {
            var principal = _validator.Validate(header.Parameter);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
        catch (Exception exception)
        {
            Logger.LogWarning(
                "Service-token authentication failed with reason {FailureType}.",
                exception.GetType().Name);
            return Task.FromResult(AuthenticateResult.Fail("The service token is invalid."));
        }
    }
}

public sealed class MobileServiceTokenConfigurationException : Exception;
