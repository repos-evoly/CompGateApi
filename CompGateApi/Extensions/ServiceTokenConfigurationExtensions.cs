namespace CompGateApi.Extensions;

public static class ServiceTokenConfigurationExtensions
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SERVICE_TOKEN_ISSUER"] = "ServiceTokens:Issuer",
            ["SERVICE_TOKEN_AUDIENCE"] = "ServiceTokens:Audience",
            ["SERVICE_TOKEN_ENVIRONMENT"] = "ServiceTokens:Environment",
            ["SERVICE_TOKEN_REQUIRED_SCOPE"] = "ServiceTokens:RequiredScope",
            ["SERVICE_TOKEN_PUBLIC_KEY"] = "ServiceTokens:PublicKeyPem"
        };

    public static IConfigurationBuilder AddServiceTokenEnvironmentAliases(
        this IConfigurationBuilder configuration)
    {
        var values = Aliases
            .Select(alias => new
            {
                alias.Value,
                Setting = Environment.GetEnvironmentVariable(alias.Key)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Setting))
            .ToDictionary(item => item.Value, item => item.Setting, StringComparer.OrdinalIgnoreCase);

        var clientId = Environment.GetEnvironmentVariable("SERVICE_AUTH_CLIENT_ID");
        var scopes = Environment.GetEnvironmentVariable("SERVICE_AUTH_ALLOWED_SCOPES");
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            values[$"ServiceTokens:Clients:{clientId}:AllowedScopes"] =
                string.IsNullOrWhiteSpace(scopes) ? "company-gateway.mobile" : scopes;
        }

        return configuration.AddInMemoryCollection(values!);
    }
}
