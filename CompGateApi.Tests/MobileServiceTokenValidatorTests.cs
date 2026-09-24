using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CompGateApi.Core.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CompGateApi.Tests;

public sealed class MobileServiceTokenValidatorTests : IDisposable
{
    private const string ClientId = "company-gateway-mobile-bff-uat";
    private readonly RSA _rsa = RSA.Create(2048);

    [Fact]
    public void Validate_RepeatedRequestsWithSameTokenSucceed()
    {
        var validator = CreateValidator();
        var token = CreateToken();
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(ClientId, validator.Validate(token).FindFirst("client_id")?.Value);
        }
    }

    [Fact]
    public void Validate_AcceptsExpectedUatServiceIdentity()
    {
        var validator = CreateValidator();

        var principal = validator.Validate(CreateToken());

        Assert.Equal("service", principal.FindFirst("token_type")?.Value);
        Assert.Equal(ClientId, principal.FindFirst("client_id")?.Value);
    }

    [Theory]
    [InlineData("token_type", "user")]
    [InlineData("client_id", "unapproved-client")]
    [InlineData("environment", "live")]
    [InlineData("scope", "different.scope")]
    public void Validate_RejectsInvalidServiceIdentityClaims(string claimType, string value)
    {
        var validator = CreateValidator();

        Assert.Throws<SecurityTokenValidationException>(() =>
            validator.Validate(CreateToken(new Dictionary<string, string>
            {
                [claimType] = value
            })));
    }

    [Fact]
    public void Validate_RejectsWrongAudience()
    {
        var validator = CreateValidator();

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            validator.Validate(CreateToken(audience: "company-gateway-internal-apis-live")));
    }

    private MobileServiceTokenValidator CreateValidator()
    {
        var options = new MobileServiceTokenOptions
        {
            Issuer = "company-gateway-service-auth-uat",
            Audience = "company-gateway-internal-apis-uat",
            Environment = "uat",
            RequiredScope = "company-gateway.mobile",
            PublicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem(),
            Clients = new Dictionary<string, MobileServiceClientOptions>
            {
                [ClientId] = new() { AllowedScopes = "company-gateway.mobile" }
            }
        };
        return new MobileServiceTokenValidator(new StaticOptionsMonitor<MobileServiceTokenOptions>(options));
    }

    private string CreateToken(
        IReadOnlyDictionary<string, string>? overrides = null,
        string audience = "company-gateway-internal-apis-uat")
    {
        var values = new Dictionary<string, string>
        {
            ["token_type"] = "service",
            ["client_id"] = ClientId,
            ["environment"] = "uat",
            ["scope"] = "company-gateway.mobile"
        };
        if (overrides is not null)
        {
            foreach (var item in overrides)
            {
                values[item.Key] = item.Value;
            }
        }

        var token = new JwtSecurityToken(
            issuer: "company-gateway-service-auth-uat",
            audience: audience,
            claims: values.Select(item => new Claim(item.Key, item.Value)),
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(_rsa),
                SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose() => _rsa.Dispose();

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
