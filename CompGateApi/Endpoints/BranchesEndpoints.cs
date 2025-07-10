using System.Net.Http.Json;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Endpoints
{
    public class BranchesEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app
                .MapGroup("/api/branches")
                .WithTags("Branches");

            grp.MapGet("/", GetAll)
               .WithName("GetActiveBranches")
               .Produces<ActiveBranchesResponseDto>(200);
        }

        public static async Task<IResult> GetAll(
            [FromServices] IHttpClientFactory httpFactory)
        {
            var client = httpFactory.CreateClient("KycApi");
            // assuming KycApi.BaseAddress = http://10.3.3.11/kycapi/api/core/
            var resp = await client.GetFromJsonAsync<ActiveBranchesResponseDto>("getActiveBranches");
            return resp is null
                ? Results.StatusCode(StatusCodes.Status502BadGateway)
                : Results.Ok(resp);
        }
    }
}
