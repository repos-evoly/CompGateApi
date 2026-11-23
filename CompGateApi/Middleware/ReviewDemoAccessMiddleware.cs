using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace CompGateApi.Middleware;

public sealed class ReviewDemoAccessOptions
{
    public int[] AuthUserIds { get; init; } = [];

    public bool Contains(int authUserId) => AuthUserIds.Contains(authUserId);
}

// The access-context route still requires its existing user and Mobile API service policies.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ReviewDemoAccessContextAttribute : Attribute;

public sealed class ReviewDemoAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<ReviewDemoAccessOptions> options)
    {
        var userId = context.User.FindFirstValue("nameid") ??
                     context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated == true &&
            int.TryParse(userId, out var id) && options.Value.Contains(id) &&
            context.GetEndpoint()?.Metadata.GetMetadata<ReviewDemoAccessContextAttribute>() is null)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
