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

        public static async Task<IResult> GetBlockedCustomers(
             [FromServices] ICustomerRepository customerRepository,
             [FromQuery] string? search,
             [FromQuery] string? searchBy,
             [FromQuery] int page = 1,
             [FromQuery] int limit = 100)
        {
            var blockedCustomers = await customerRepository.GetBlockedCustomers(search, searchBy, page, limit);
            return Results.Ok(blockedCustomers);
        }

        // GET: Unblocked customers
        public static async Task<IResult> GetUnblockedCustomers(
            [FromServices] ICustomerRepository customerRepository,
            [FromQuery] string? search,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100000)
        {
            var unblockedCustomers = await customerRepository.GetUnblockedCustomers(search, searchBy, page, limit);
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
