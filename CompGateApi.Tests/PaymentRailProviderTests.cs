using System.Net;
using System.Text;
using System.Text.Json;
using CompGateApi.Core.LyPay;
using CompGateApi.Core.OnePay;
using CompGateApi.Core.Startup;
using CompGateApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CompGateApi.Tests;

public sealed class PaymentRailProviderTests
{
    [Theory]
    [InlineData(false, "CBLB")]
    [InlineData(true, "CBLT")]
    public async Task ExecuteUsesSharedConfigurationAndSignsTheExactNumericPayload(bool lyPay, string endpoint)
    {
        const string signingKey = "test-only-signing-key";
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://provider.invalid/api/") };
        var options = Options.Create(new OnePayOptions
        {
            BaseUrl = http.BaseAddress.ToString(), SystemId = "TestCorpGate", ChecksumPassword = signingKey
        });
        if (lyPay)
        {
            var provider = new LyPayProviderClient(http, options, NullLogger<LyPayProviderClient>.Instance);
            var result = await provider.ExecuteOutgoingTransferAsync(new LyPayProviderExecutionCommand(
                "TEST", "AR", "REF", "HOST", "DHB", "AGREEMENT", "TESTACCOUNT", 10.5000m, "LYD", "Test"), CancellationToken.None);
            Assert.True(result.HasProviderResponse);
        }
        else
        {
            var provider = new OnePayProviderClient(http, options, NullLogger<OnePayProviderClient>.Instance);
            var result = await provider.ExecuteOutgoingTransferAsync(new OnePayProviderExecutionCommand(
                "TEST", "AR", "REF", "HOST", "DHB", "AGREEMENT", "TESTACCOUNT", 10.5000m, "LYD", "Test"), CancellationToken.None);
            Assert.True(result.HasProviderResponse);
        }

        Assert.Equal($"https://provider.invalid/api/{endpoint}/ExecuteOutgoingTransfer", handler.Url!.ToString());
        using var payload = JsonDocument.Parse(handler.Body!);
        var main = payload.RootElement.GetProperty("Main");
        Assert.Equal("TestCorpGate", main.GetProperty("SystemID").GetString());
        Assert.Equal("10.5", payload.RootElement.GetProperty("Data").GetProperty("Amount").GetRawText());
        Assert.Equal(OnePayChecksum.Generate(handler.Body!, signingKey,
            DateTimeOffset.Parse(main.GetProperty("Time").GetString()!)), handler.Checksum);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task BeneficiaryAndPaymentRailRoutesHaveUniqueHttpMethodAndPathPairs()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.RegisterRepos(builder.Configuration, builder.Environment);
        await using var app = builder.Build();
        new BeneficiaryEndpoints().RegisterEndpoints(app);
        new OnePayEndpoints().RegisterEndpoints(app);
        new LyPayEndpoints().RegisterEndpoints(app);
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().ToList();
        var pairs = routes.SelectMany(route => route.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods
            .Select(method => method + " " + route.RoutePattern.RawText)).ToList();
        Assert.Equal(pairs.Count, pairs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(pairs, pair => pair.Contains("/api/onepay"));
        Assert.Contains(pairs, pair => pair.Contains("/api/lypay"));
        Assert.Contains("DELETE /api/beneficiaries/{id:int}", pairs);
        Assert.Contains("POST /api/beneficiaries/{id:int}/delete", pairs);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? Url { get; private set; }
        public string? Body { get; private set; }
        public string? Checksum { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Url = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(token);
            Checksum = request.Headers.GetValues("Checksum").Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Main\":{\"Success\":true},\"Data\":{}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
