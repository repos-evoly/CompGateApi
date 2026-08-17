using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.OnePay;

public interface IOnePayProviderClient
{
    bool IsConfigured { get; }
    Task<OnePayProviderCall<OnePayInstitutionData>> GetInstitutionsAsync(
        string accountNo,
        string language,
        string referenceNo,
        CancellationToken cancellationToken);
    Task<OnePayProviderCall<OnePayValidateResponseData>> ValidateOutgoingTransferAsync(
        OnePayProviderValidationCommand command,
        CancellationToken cancellationToken);
    Task<OnePayProviderCall<object>> ExecuteOutgoingTransferAsync(
        OnePayProviderExecutionCommand command,
        CancellationToken cancellationToken);
    Task<OnePayProviderCall<OnePayStatusResponseData>> CheckTransactionStatusAsync(
        OnePayProviderStatusCommand command,
        CancellationToken cancellationToken);
}

public sealed record OnePayProviderValidationCommand(
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
    OnePayProviderDeviceInfo DeviceInfo);

public sealed record OnePayProviderExecutionCommand(
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

public sealed record OnePayProviderStatusCommand(
    string AccountNo,
    string Language,
    string ReferenceNo,
    string HostReference,
    string DhbReference,
    string AgreementReference);

public sealed class OnePayProviderDeviceInfo
{
    [JsonPropertyName("deviceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get; set; }

    [JsonPropertyName("deviceLat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceLat { get; set; }

    [JsonPropertyName("deviceLon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceLon { get; set; }

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty;

    [JsonPropertyName("imei")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Imei { get; set; }

    [JsonPropertyName("osType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsType { get; set; }

    [JsonPropertyName("osVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsVersion { get; set; }
}

public sealed record OnePayProviderCall<T>(
    bool WasSent,
    bool HttpSuccess,
    OnePayProviderMainResponse? Main,
    T? Data,
    string? Error) where T : class
{
    public bool HasProviderResponse => Main is not null;
}

public sealed class OnePayProviderMainResponse
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

public sealed class OnePayInstitutionData
{
    public List<OnePayInstitutionItem>? Institutions { get; set; }
}

public sealed class OnePayInstitutionItem
{
    public string? Reference { get; set; }
    public string? OnePayReference { get; set; }
    public string? LYPayReference { get; set; }
    public string? ShortName { get; set; }
    public string? FullName { get; set; }
}

public sealed class OnePayValidateResponseData
{
    public string? HostReference { get; set; }
    public string? DHBReference { get; set; }
    public string? AgreementReference { get; set; }
    public string? ToAccountName { get; set; }
}

public sealed class OnePayStatusResponseData
{
    public bool? IsFinished { get; set; }

    [JsonPropertyName("IsTransactionSucess")]
    public bool? IsTransactionSuccess { get; set; }

    public string? TransactionStatus { get; set; }
}

public sealed class OnePayProviderClient : IOnePayProviderClient
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

    private static readonly JsonSerializerOptions ExecuteWireJsonOptions =
        CreateExecuteWireJsonOptions();

    private readonly HttpClient _httpClient;
    private readonly OnePayOptions _options;
    private readonly ILogger<OnePayProviderClient> _logger;

    public OnePayProviderClient(
        HttpClient httpClient,
        IOptions<OnePayOptions> options,
        ILogger<OnePayProviderClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_options.SystemId) &&
        !string.IsNullOrWhiteSpace(_options.ChecksumPassword);

    public Task<OnePayProviderCall<OnePayInstitutionData>> GetInstitutionsAsync(
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
        return SendAsync<OnePayInstitutionData>(
            "CBL/GetInstitutionList",
            payload,
            requestTimeUtc,
            cancellationToken);
    }

    public Task<OnePayProviderCall<OnePayValidateResponseData>> ValidateOutgoingTransferAsync(
        OnePayProviderValidationCommand command,
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
        return SendAsync<OnePayValidateResponseData>(
            "CBLB/ValidateOutgoingTransfer",
            payload,
            requestTimeUtc,
            cancellationToken);
    }

    public Task<OnePayProviderCall<object>> ExecuteOutgoingTransferAsync(
        OnePayProviderExecutionCommand command,
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
            "CBLB/ExecuteOutgoingTransfer",
            payload,
            requestTimeUtc,
            cancellationToken,
            ExecuteWireJsonOptions);
    }

    public Task<OnePayProviderCall<OnePayStatusResponseData>> CheckTransactionStatusAsync(
        OnePayProviderStatusCommand command,
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
                command.AgreementReference
            }
        };
        return SendAsync<OnePayStatusResponseData>(
            "CBLB/CheckTransactionStatus",
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

    private async Task<OnePayProviderCall<T>> SendAsync<T>(
        string relativePath,
        object payload,
        DateTimeOffset requestTimeUtc,
        CancellationToken cancellationToken,
        JsonSerializerOptions? requestJsonOptions = null) where T : class
    {
        if (!IsConfigured)
        {
            return new OnePayProviderCall<T>(
                false,
                false,
                null,
                default,
                "OnePay integration is not configured.");
        }

        var exactJson = JsonSerializer.Serialize(
            payload,
            requestJsonOptions ?? WireJsonOptions);
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

            OnePayProviderEnvelope<T>? envelope = null;
            if (!string.IsNullOrWhiteSpace(responseJson))
            {
                try
                {
                    envelope = JsonSerializer.Deserialize<OnePayProviderEnvelope<T>>(
                        responseJson,
                        WireJsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "OnePay returned an invalid JSON response for {Path} with HTTP {StatusCode}",
                        relativePath,
                        (int)response.StatusCode);
                }
            }

            var error = envelope?.Main?.ReturnMessage;
            if (string.IsNullOrWhiteSpace(error))
                error = envelope?.Main?.GeneralError;
            if (string.IsNullOrWhiteSpace(error) && !response.IsSuccessStatusCode)
                error = $"OnePay returned HTTP {(int)response.StatusCode}.";
            if (envelope is null && string.IsNullOrWhiteSpace(error))
                error = "OnePay returned an unreadable response.";

            return new OnePayProviderCall<T>(
                true,
                response.IsSuccessStatusCode,
                envelope?.Main,
                envelope?.Data,
                error);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "OnePay request timed out or was cancelled for {Path}", relativePath);
            return new OnePayProviderCall<T>(
                true,
                false,
                null,
                default,
                "OnePay did not return a response before the request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OnePay transport error for {Path}", relativePath);
            return new OnePayProviderCall<T>(
                true,
                false,
                null,
                default,
                "OnePay could not be reached.");
        }
    }

    private static string NormalizeLanguage(string language) =>
        string.Equals(language, "AR", StringComparison.OrdinalIgnoreCase) ? "AR" : "EN";

    private static JsonSerializerOptions CreateExecuteWireJsonOptions()
    {
        var options = new JsonSerializerOptions(WireJsonOptions);
        options.Converters.Add(new ProviderDecimalJsonConverter());
        return options;
    }

    private sealed class ProviderDecimalJsonConverter : JsonConverter<decimal>
    {
        // Validation succeeds with canonical numeric values such as 10. Strip
        // insignificant zeros introduced by SQL decimal(18,4) so Execute signs
        // the same numeric representation while preserving meaningful digits.
        private const string Format = "0.############################";

        public override decimal Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.GetDecimal();

        public override void Write(
            Utf8JsonWriter writer,
            decimal value,
            JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }

    private sealed class OnePayProviderEnvelope<T> where T : class
    {
        public OnePayProviderMainResponse? Main { get; set; }
        public T? Data { get; set; }
    }
}
