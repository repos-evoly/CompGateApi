using System.Net.Http.Json;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;

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

            var external = await client.GetFromJsonAsync<ExternalActiveBranchesResponseDto>("kycapi/api/core/getActiveBranches");

            if (external is null)
                return Results.StatusCode(StatusCodes.Status502BadGateway);

            var mapped = new ActiveBranchesResponseDto
            {
                Header = new HeaderDto
                {
                    System = external.Header.System,
                    ReferenceId = external.Header.ReferenceId,
                    SentTime = external.Header.SentTime,
                    ReturnCode = external.Header.ReturnCode,
                    ReturnMessageCode = external.Header.ReturnMessageCode,
                    ReturnMessage = external.Header.ReturnMessage
                },
                Details = new BranchesDetailsDto
                {
                    Branches = external.Details.Branches.Select(b => new BranchDto
                    {
                        BranchNumber = b.CABBN,
                        BranchName = b.CABRN,
                        BranchMnemonic = b.CABRNM
                    }).ToList()
                }
            };

            return Results.Ok(mapped);
        }
    }
}
