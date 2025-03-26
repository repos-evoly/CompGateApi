using BlockingApi.Data.Models;
using BlockingApi.Core.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class CustomerEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var customers = app.MapGroup("/api/customers").RequireAuthorization("requireAuthUser");

            customers.MapGet("/blocked", GetBlockedCustomers)
                .RequireAuthorization("ViewBlockedCustomers")
                .Produces(200);

            customers.MapGet("/unblocked", GetUnblockedCustomers)
                .RequireAuthorization("ViewUnblockedCustomers")
                .Produces(200);

            customers.MapGet("/search", SearchCustomers)
                .RequireAuthorization("ViewCustomers")
                .Produces(200);
        }

        private static async Task<bool> UserHasPermission(ClaimsPrincipal user, string permission, IRoleRepository roleRepository)
        {
            var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userPermissions = await roleRepository.GetUserPermissions(userId);
            return userPermissions.Contains(permission);
        }

        public static async Task<IResult> GetBlockedCustomers([FromServices] ICustomerRepository customerRepository)
        {
            var blockedCustomers = await customerRepository.GetBlockedCustomers();
            return Results.Ok(blockedCustomers);
        }

        public static async Task<IResult> GetUnblockedCustomers([FromServices] ICustomerRepository customerRepository)
        {
            var unblockedCustomers = await customerRepository.GetUnblockedCustomers();
            return Results.Ok(unblockedCustomers);
        }

        public static async Task<IResult> SearchCustomers(
            [FromQuery] string searchTerm,
            [FromServices] ICustomerRepository customerRepository)
        {
            var customers = await customerRepository.SearchCustomers(searchTerm);
            return Results.Ok(customers);
        }
    }
}
