using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CompGateApi.Core.Startup;

internal static class CompAuthSessionValidation
{
    internal static async Task<bool> CheckAsync(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;

        var factory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("AuthApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/session/validate");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.StatusCode == HttpStatusCode.NoContent;
        }
        catch (HttpRequestException) { return false; }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return false; }
    }
}
