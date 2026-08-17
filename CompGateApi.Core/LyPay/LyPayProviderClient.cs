using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompGateApi.Core.OnePay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.LyPay;

public interface ILyPayProviderClient
{
    bool IsConfigured { get; }
    Task<LyPayProviderCall<LyPayInstitutionData>> GetInstitutionsAsync(
        string accountNo,
        string language,
        string referenceNo,
        CancellationToken cancellationToken);
    Task<LyPayProviderCall<LyPayValidateResponseData>> ValidateOutgoingTransferAsync(
        LyPayProviderValidationCommand command,
        CancellationToken cancellationToken);
    Task<LyPayProviderCall<object>> ExecuteOutgoingTransferAsync(
        LyPayProviderExecutionCommand command,
        CancellationToken cancellationToken);
    Task<LyPayProviderCall<LyPayStatusResponseData>> CheckTransactionStatusAsync(
        LyPayProviderStatusCommand command,
        CancellationToken cancellationToken);
}

public sealed record LyPayProviderValidationCommand(
    string AccountNo,
    string Language,
    string ReferenceNo,
    string FromFullAccount,
    string FromAccountName,
    string FromAccountPhoneNo,
    string ToInstitutionId,
    string ToFullAccount,
    decimal Amount,
    string Currency,
    string Description,
    LyPayProviderDeviceInfo DeviceInfo);

public sealed record LyPayProviderExecutionCommand(
    string AccountNo,
    string Language,
    string ReferenceNo,
    string HostReference,
    string DhbReference,
    string AgreementReference,
    string FromFullAccount,
    decimal Amount,
    string Currency,
    string Description);

public sealed record LyPayProviderStatusCommand(
    string AccountNo,
    string Language,
    string ReferenceNo,
    string HostReference,
    string DhbReference,
    string AgreementReference);

public sealed class LyPayProviderDeviceInfo
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("deviceLat")]
    public string DeviceLat { get; set; } = string.Empty;

    [JsonPropertyName("deviceLon")]
    public string DeviceLon { get; set; } = string.Empty;

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty;

    [JsonPropertyName("imei")]
    public string Imei { get; set; } = string.Empty;

    [JsonPropertyName("osType")]
    public string OsType { get; set; } = string.Empty;

    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = string.Empty;
}

public sealed record LyPayProviderCall<T>(
    bool WasSent,
    bool HttpSuccess,
    LyPayProviderMainResponse? Main,
    T? Data,
    string? Error) where T : class
{
    public bool HasProviderResponse => Main is not null;
}

public sealed class LyPayProviderMainResponse
{
    public string? SystemID { get; set; }
    public string? ReferenceNo { get; set; }
    public string? BankReferenceNo { get; set; }
    public bool? Success { get; set; }
    public string? ReturnMessageCode { get; set; }
    public string? ReturnMessage { get; set; }
    public bool? StillUnderProcessing { get; set; }
    public string? GeneralError { get; set; }
}

public sealed class LyPayInstitutionData
{
    public List<LyPayInstitutionItem>? Institutions { get; set; }
}

public sealed class LyPayInstitutionItem
{
    public string? Reference { get; set; }
    public string? OnePayReference { get; set; }
    public string? LYPayReference { get; set; }
    public string? ShortName { get; set; }
    public string? FullName { get; set; }
}

public sealed class LyPayValidateResponseData
{
    public string? HostReference { get; set; }
    public string? DHBReference { get; set; }
    public string? AgreementReference { get; set; }
    public string? ToAccountName { get; set; }
}

public sealed class LyPayStatusResponseData
{
    public bool? IsFinished { get; set; }

    [JsonPropertyName("IsTransactionSucess")]
    public bool? IsTransactionSuccess { get; set; }

    public string? TransactionStatus { get; set; }
}

public sealed class LyPayProviderClient : ILyPayProviderClient
{
    private const string ProviderDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz";

    private static readonly TimeZoneInfo LibyaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Africa/Tripoli");

    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly OnePayOptions _options;
    private readonly ILogger<LyPayProviderClient> _logger;

    public LyPayProviderClient(
        HttpClient httpClient,
        IOptions<OnePayOptions> options,
        ILogger<LyPayProviderClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_options.SystemId) &&
        !string.IsNullOrWhiteSpace(_options.ChecksumPassword);

    public Task<LyPayProviderCall<LyPayInstitutionData>> GetInstitutionsAsync(
        string accountNo,
        string language,
        string referenceNo,
        CancellationToken cancellationToken)
    {
        var requestTimeUtc = DateTimeOffset.UtcNow;
        var payload = new
        {
            Main = CreateMain(accountNo, language, referenceNo, requestTimeUtc)
        };
        return SendAsync<LyPayInstitutionData>(
            "CBL/GetInstitutionList",
            payload,
            requestTimeUtc,
            cancellationToken);
    }

    public Task<LyPayProviderCall<LyPayValidateResponseData>> ValidateOutgoingTransferAsync(
        LyPayProviderValidationCommand command,
        CancellationToken cancellationToken)
    {
        var requestTimeUtc = DateTimeOffset.UtcNow;
        var payload = new
        {
            Main = CreateMain(command.AccountNo, command.Language, command.ReferenceNo, requestTimeUtc),
            Data = new
            {
                command.FromFullAccount,
                command.FromAccountName,
                command.FromAccountPhoneNo,
                ToInstitutionID = command.ToInstitutionId,
                command.ToFullAccount,
                command.Amount,
                command.Currency,
                command.Description
            },
            DeviceInfo = command.DeviceInfo
        };
        return SendAsync<LyPayValidateResponseData>(
            "CBLT/ValidateOutgoingTransfer",
            payload,
            requestTimeUtc,
            cancellationToken);
    }

    public Task<LyPayProviderCall<object>> ExecuteOutgoingTransferAsync(
        LyPayProviderExecutionCommand command,
        CancellationToken cancellationToken)
    {
        var requestTimeUtc = DateTimeOffset.UtcNow;
        var payload = new
        {
            Main = CreateMain(command.AccountNo, command.Language, command.ReferenceNo, requestTimeUtc),
            Data = new
            {
                command.HostReference,
                DHBReference = command.DhbReference,
                command.AgreementReference,
                command.FromFullAccount,
                command.Amount,
                command.Currency,
                command.Description
            }
        };
        return SendAsync<object>(
            "CBLT/ExecuteOutgoingTransfer",
            payload,
            requestTimeUtc,
            cancellationToken);
    }

    public Task<LyPayProviderCall<LyPayStatusResponseData>> CheckTransactionStatusAsync(
        LyPayProviderStatusCommand command,
        CancellationToken cancellationToken)
    {
        var requestTimeUtc = DateTimeOffset.UtcNow;
        var payload = new
        {
            Main = CreateMain(command.AccountNo, command.Language, command.ReferenceNo, requestTimeUtc),
            Data = new
            {
                command.HostReference,
                // The CBLT status sample prefixes the raw validation reference,
                // while execute uses it unchanged. Keep the stored value raw and
                // derive the status-only provider identifier idempotently here.
                DHBReference = StatusDhbReference(command.DhbReference),
                command.AgreementReference
            }
        };
        return SendAsync<LyPayStatusResponseData>(
            "CBLT/CheckTransactionStatus",
            payload,
            requestTimeUtc,
            cancellationToken);
    }

    private object CreateMain(
        string accountNo,
        string language,
        string referenceNo,
        DateTimeOffset requestTimeUtc) => new
        {
            SystemID = _options.SystemId,
            Time = TimeZoneInfo.ConvertTime(requestTimeUtc, LibyaTimeZone)
            .ToString(ProviderDateTimeFormat, CultureInfo.InvariantCulture),
            Language = NormalizeLanguage(language),
            ReferenceNo = referenceNo,
            AccountNo = accountNo
        };

    private async Task<LyPayProviderCall<T>> SendAsync<T>(
        string relativePath,
        object payload,
        DateTimeOffset requestTimeUtc,
        CancellationToken cancellationToken) where T : class
    {
        if (!IsConfigured)
        {
            return new LyPayProviderCall<T>(
                false,
                false,
                null,
                default,
                "LyPay integration is not configured.");
        }

        var exactJson = JsonSerializer.Serialize(payload, WireJsonOptions);
        var checksum = OnePayChecksum.Generate(
            exactJson,
            _options.ChecksumPassword,
            requestTimeUtc);

        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        request.Headers.TryAddWithoutValidation("Checksum", checksum);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(exactJson, Encoding.UTF8, "application/json");

        try
        {
            // Buffer the small provider response inside SendAsync so HttpClient.Timeout
            // also bounds the response body, not only the response headers.
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            LyPayProviderEnvelope<T>? envelope = null;
            if (!string.IsNullOrWhiteSpace(responseJson))
            {
                try
                {
                    envelope = JsonSerializer.Deserialize<LyPayProviderEnvelope<T>>(
                        responseJson,
                        WireJsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "LyPay returned an invalid JSON response for {Path} with HTTP {StatusCode}",
                        relativePath,
                        (int)response.StatusCode);
                }
            }

            var error = envelope?.Main?.ReturnMessage;
            if (string.IsNullOrWhiteSpace(error))
                error = envelope?.Main?.GeneralError;
            if (string.IsNullOrWhiteSpace(error) && !response.IsSuccessStatusCode)
                error = $"LyPay returned HTTP {(int)response.StatusCode}.";
            if (envelope is null && string.IsNullOrWhiteSpace(error))
                error = "LyPay returned an unreadable response.";

            return new LyPayProviderCall<T>(
                true,
                response.IsSuccessStatusCode,
                envelope?.Main,
                envelope?.Data,
                error);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "LyPay request timed out or was cancelled for {Path}", relativePath);
            return new LyPayProviderCall<T>(
                true,
                false,
                null,
                default,
                "LyPay did not return a response before the request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "LyPay transport error for {Path}", relativePath);
            return new LyPayProviderCall<T>(
                true,
                false,
                null,
                default,
                "LyPay could not be reached.");
        }
    }

    private static string NormalizeLanguage(string language) =>
        string.Equals(language, "AR", StringComparison.OrdinalIgnoreCase) ? "AR" : "EN";

    private static string StatusDhbReference(string dhbReference) =>
        dhbReference.StartsWith("LYPay_", StringComparison.OrdinalIgnoreCase)
            ? dhbReference
            : $"LYPay_{dhbReference}";

    private sealed class LyPayProviderEnvelope<T> where T : class
    {
        public LyPayProviderMainResponse? Main { get; set; }
        public T? Data { get; set; }
    }
}
