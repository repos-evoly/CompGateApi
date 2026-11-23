using System.Security.Claims;
using CompGateApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace CompGateApi.Tests;

public sealed class ReviewDemoAccessTests
{
    [Theory]
    [InlineData(31, "/api/transfers/10/post", true, 403)]
    [InlineData(32, "/api/employees/salarycycles/12/post", true, 403)]
    [InlineData(31, "/api/companies/900001/dashboard", true, 403)]
    [InlineData(33, "/api/transfers/10/post", true, 204)]
    [InlineData(31, "/api/transfers/10/post", false, 204)]
    public async Task LiveRoutesBlockOnlyVerifiedReviewIdentities(int userId, string path, bool authenticated, int expected)
    {
        var context = Context(userId, authenticated);
        context.Request.Path = path;
        await Guard().InvokeAsync(context, Options.Create(new ReviewDemoAccessOptions { AuthUserIds = [31, 32] }));
        Assert.Equal(expected, context.Response.StatusCode);
    }

    [Fact]
    public async Task AccessContextExemptionRequiresEndpointMetadata()
    {
        var context = Context(31, true);
        context.Request.Path = "/api/mobile-internal/users/31/access-context";
        var options = Options.Create(new ReviewDemoAccessOptions { AuthUserIds = [31, 32] });
        await Guard().InvokeAsync(context, options);
        Assert.Equal(403, context.Response.StatusCode);

        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ReviewDemoAccessContextAttribute()), "Access context"));
        await Guard().InvokeAsync(context, options);
        Assert.Equal(204, context.Response.StatusCode);
    }

    private static DefaultHttpContext Context(int userId, bool authenticated)
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("nameid", userId.ToString())],
                authenticated ? "verified-test-authentication" : null))
        };

    private static ReviewDemoAccessMiddleware Guard()
        => new(context =>
        {
            context.Response.StatusCode = 204;
            return Task.CompletedTask;
        });
}
