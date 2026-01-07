using System;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints
{
    public class DashboardEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var admin = app.MapGroup("/api/admin/dashboard")
                           .WithTags("Dashboard")
                           .RequireAuthorization("RequireAdminUser");

            admin.MapGet("/summary", GetSummary)
                 .Produces(200);

            admin.MapGet("/totals", GetTotals)
                 .Produces(200);
        }

        public static async Task<IResult> GetSummary(
            [FromServices] IDashboardRepository repo,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var data = await repo.GetCommissionSummaryAsync(from, to);
            // shape to match example semantics (commissionBoxes and transactionsBoxes available via /totals)
            return Results.Ok(new
            {
                commissionBoxes = data.CommissionBoxes
            });
        }

        public static async Task<IResult> GetTotals(
            [FromServices] IDashboardRepository repo,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var totals = await repo.GetTotalsAsync(from, to);
            return Results.Ok(new
            {
                transactionsBoxes = new object[]
                {
                    new { key = "internalTransfers", value = totals.InternalTransfers },
                    new { key = "checkRequests", value = totals.CheckRequests },
                    new { key = "checkBookRequests", value = totals.CheckBookRequests },
                    new { key = "salaries", value = totals.Salaries }
                }
            });
        }
    }
}

