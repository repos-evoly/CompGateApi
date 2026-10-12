using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompGateApi.Core.WalletSalary;

public enum WalletSalaryDispatchClassification
{
    NotDispatched,
    ResponseReceived,
    OutcomeUnknown
}

public sealed record WalletSalaryProviderCall<T>(
    WalletSalaryDispatchClassification DispatchClassification,
    bool HttpSuccess,
    HttpStatusCode? HttpStatus,
    T? Data,
    string? ResponseRaw,
    string? ErrorCode,
    string? Error) where T : class;

public interface IWalletSalaryProviderClient
{
    Task<WalletSalaryProviderCall<WalletSalaryProviderBatchResponse>> SendBatchAsync(
        string rail,
        string exactRequestJson,
        CancellationToken cancellationToken);

    Task<WalletSalaryProviderCall<WalletSalaryProviderBatchResponse>> GetBatchAsync(
        string rail,
        string providerBatchId,
        CancellationToken cancellationToken);

    Task<WalletSalaryProviderCall<WalletSalaryProviderTransactionResponse>> GetTransactionAsync(
        string rail,
        string providerTransactionId,
        CancellationToken cancellationToken);
}

public sealed class WalletSalaryProviderBatchRequest
{
    // Created once, then persisted with the exact request before dispatch.
    [JsonPropertyName("clientReference")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientReference { get; set; }

    [JsonPropertyName("items")]
    public List<WalletSalaryProviderBatchRequestItem> Items { get; set; } = new();
}

public sealed class WalletSalaryProviderBatchRequestItem
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("evoId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EvoId { get; set; }

    [JsonPropertyName("phoneNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhoneNumber { get; set; }
}

public sealed class WalletSalaryProviderBatchResponse
{
    public string? BatchId { get; set; }
    public string? Status { get; set; }
    public int Total { get; set; }
    public int Approved { get; set; }
    public int Failed { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Commission { get; set; }
    public decimal SenderCommission { get; set; }
    public decimal ReceiverCommission { get; set; }
    public decimal DebitAmount { get; set; }
    public List<WalletSalaryProviderBatchResponseItem> Items { get; set; } = new();
}

public sealed class WalletSalaryProviderBatchResponseItem
{
    public string? Error { get; set; }
    public int Index { get; set; }
    public decimal Amount { get; set; }
    public string? EvoId { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal Commission { get; set; }
    public string? Status { get; set; }
    public string? TransactionId { get; set; }
    public string? TxCode { get; set; }
}

public sealed class WalletSalaryProviderTransactionResponse
{
    public string? Id { get; set; }
    public string? TypeId { get; set; }
    public string? FromAccount { get; set; }
    public string? ToAccount { get; set; }
    public decimal Amount { get; set; }
    public string? ChargesTotal { get; set; }
    public string? ChargesMatrix { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
    public string? TxCode { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? SenderEvoId { get; set; }
    public string? ReceiverEvoId { get; set; }
    public string? ReceiverPhoneNumber { get; set; }
}

public static class WalletSalaryProviderContract
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WalletSalaryProviderBatchRequestItem CreateRequestItem(
        decimal amount,
        string recipientType,
        string destination)
    {
        var normalizedType = WalletSalaryRecipientTypes.NormalizeWalletType(recipientType)
            ?? throw new ArgumentException("Wallet recipient type must be evoId or phoneNumber.", nameof(recipientType));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Wallet destination is required.", nameof(destination));

        return normalizedType == WalletSalaryRecipientTypes.EvoId
            ? new WalletSalaryProviderBatchRequestItem
            {
                Amount = amount,
                EvoId = destination.Trim()
            }
            : new WalletSalaryProviderBatchRequestItem
            {
                Amount = amount,
                PhoneNumber = destination.Trim()
            };
    }

    public static string SerializeBatch(WalletSalaryProviderBatchRequest request) =>
        JsonSerializer.Serialize(request, JsonOptions);

    public static string HashPayload(string exactJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exactJson))).ToLowerInvariant();
}

public sealed class WalletSalaryProviderClient : IWalletSalaryProviderClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WalletSalaryOptions _options;
    private readonly ILogger<WalletSalaryProviderClient> _logger;

    public WalletSalaryProviderClient(
        IHttpClientFactory httpClientFactory,
        IOptions<WalletSalaryOptions> options,
        ILogger<WalletSalaryProviderClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<WalletSalaryProviderCall<WalletSalaryProviderBatchResponse>> SendBatchAsync(
        string rail,
        string exactRequestJson,
        CancellationToken cancellationToken) =>
        SendAsync<WalletSalaryProviderBatchResponse>(
            rail,
            HttpMethod.Post,
            "batchQueue",
            exactRequestJson,
            cancellationToken);

    public Task<WalletSalaryProviderCall<WalletSalaryProviderBatchResponse>> GetBatchAsync(
        string rail,
        string providerBatchId,
        CancellationToken cancellationToken) =>
        SendAsync<WalletSalaryProviderBatchResponse>(
            rail,
            HttpMethod.Get,
            $"batch/{Uri.EscapeDataString(providerBatchId)}",
            null,
            cancellationToken);

    public Task<WalletSalaryProviderCall<WalletSalaryProviderTransactionResponse>> GetTransactionAsync(
        string rail,
        string providerTransactionId,
        CancellationToken cancellationToken) =>
        SendAsync<WalletSalaryProviderTransactionResponse>(
            rail,
            HttpMethod.Get,
            $"transaction/{Uri.EscapeDataString(providerTransactionId)}",
            null,
            cancellationToken);

    private async Task<WalletSalaryProviderCall<T>> SendAsync<T>(
        string rail,
        HttpMethod method,
        string relativeResource,
        string? exactRequestJson,
        CancellationToken cancellationToken) where T : class
    {
        var railOptions = _options.ForRail(rail);
        if (!railOptions.Enabled || !railOptions.IsConfigured)
        {
            return new WalletSalaryProviderCall<T>(
                WalletSalaryDispatchClassification.NotDispatched,
                false,
                null,
                null,
                null,
                "CONFIGURATION_MISSING",
                $"{rail} wallet salary integration is not configured.");
        }

        Uri requestUri;
        try
        {
            var baseUrl = railOptions.BaseUrl.TrimEnd('/');
            var version = _options.Version.Trim('/');
            requestUri = new Uri($"{baseUrl}/rest/{version}/external-transfer/{relativeResource}");
            if (exactRequestJson is not null)
                _ = JsonDocument.Parse(exactRequestJson);
        }
        catch (Exception ex) when (ex is UriFormatException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Wallet salary request preparation failed for rail {Rail}", rail);
            return new WalletSalaryProviderCall<T>(
                WalletSalaryDispatchClassification.NotDispatched,
                false,
                null,
                null,
                null,
                "REQUEST_PREPARATION_FAILED",
                "Wallet salary request could not be prepared.");
        }

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.TryAddWithoutValidation("x-api-key", railOptions.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (exactRequestJson is not null)
            request.Content = new StringContent(exactRequestJson, Encoding.UTF8, "application/json");

        try
        {
            var client = _httpClientFactory.CreateClient("WalletSalaryProvider");
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            var responseRaw = await response.Content.ReadAsStringAsync(cancellationToken);
            T? parsed = null;
            if (!string.IsNullOrWhiteSpace(responseRaw))
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<T>(
                        responseRaw,
                        WalletSalaryProviderContract.JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Wallet salary provider returned unreadable JSON for rail {Rail}, resource {Resource}, HTTP {StatusCode}",
                        rail,
                        relativeResource,
                        (int)response.StatusCode);
                }
            }

            var (errorCode, error) = ExtractError(responseRaw);
            if (string.IsNullOrWhiteSpace(error) && !response.IsSuccessStatusCode)
                error = $"Wallet provider returned HTTP {(int)response.StatusCode}.";
            if (parsed is null && string.IsNullOrWhiteSpace(error))
                error = "Wallet provider returned an unreadable response.";

            return new WalletSalaryProviderCall<T>(
                WalletSalaryDispatchClassification.ResponseReceived,
                response.IsSuccessStatusCode,
                response.StatusCode,
                parsed,
                responseRaw,
                errorCode,
                error);
        }
        catch (HttpRequestException ex) when (
            ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.SecureConnectionError)
        {
            // DNS and TLS negotiation fail before an HTTP request body can reach
            // the provider application. These are the only transport failures
            // classified as definitely not dispatched.
            _logger.LogWarning(ex, "Wallet salary provider connection failed before dispatch for rail {Rail}", rail);
            return new WalletSalaryProviderCall<T>(
                WalletSalaryDispatchClassification.NotDispatched,
                false,
                null,
                null,
                null,
                "PRE_DISPATCH_CONNECTION_FAILED",
                "Wallet provider could not be reached before the request was dispatched.");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Wallet salary provider call timed out or was cancelled for rail {Rail}", rail);
            return Unknown<T>("REQUEST_TIMEOUT", "Wallet provider outcome is unknown because no response was received.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Wallet salary provider transport failure for rail {Rail}", rail);
            return Unknown<T>("TRANSPORT_FAILURE", "Wallet provider outcome is unknown because the connection failed after dispatch may have started.");
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Wallet salary provider response read failure for rail {Rail}", rail);
            return Unknown<T>("RESPONSE_READ_FAILURE", "Wallet provider outcome is unknown because the response could not be read.");
        }
    }

    private static WalletSalaryProviderCall<T> Unknown<T>(string code, string error) where T : class =>
        new(
            WalletSalaryDispatchClassification.OutcomeUnknown,
            false,
            null,
            null,
            null,
            code,
            error);

    private static (string? Code, string? Message) ExtractError(string? responseRaw)
    {
        if (string.IsNullOrWhiteSpace(responseRaw)) return (null, null);
        try
        {
            using var document = JsonDocument.Parse(responseRaw);
            var root = document.RootElement;
            string? code = TryString(root, "code") ?? TryString(root, "errorCode");
            string? message = TryString(root, "message") ?? TryString(root, "error");
            return (code, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? TryString(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();
        }
        return null;
    }
}
