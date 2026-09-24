using CompGateApi.Core.Startup;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CompGateApi.Tests;

public sealed class Always200ResponseMiddlewareTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task MobileAccessContext_PreservesFailureStatus(int status)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/mobile-internal/users/6/access-context";
        context.Response.Body = new MemoryStream();
        var middleware = new Always200ResponseMiddleware(ctx =>
        {
            ctx.Response.StatusCode = status;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context);

        Assert.Equal(status, context.Response.StatusCode);
    }
}
