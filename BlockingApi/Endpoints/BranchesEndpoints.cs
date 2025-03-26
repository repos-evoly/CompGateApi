using BlockingApi.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlockingApi.Data.Context;
using AutoMapper;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class BranchesEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var branches = app.MapGroup("/api/branches").RequireAuthorization("requireAuthUser");

            branches.MapGet("/", GetAll)
                .WithName("GetBranches")
                .Produces<IEnumerable<BranchDto>>(200);

            branches.MapGet("/{id:int}", GetById)
                .WithName("GetBranchById")
                .Produces<BranchDto>(200)
                .Produces(404);

            branches.MapPost("/", Create)
                .WithName("CreateBranch")
                .Accepts<EditBranchDto>("application/json")
                .Produces<BranchDto>(201)
                .Produces(400);

            branches.MapPut("/{id:int}", Update)
                .WithName("UpdateBranch")
                .Accepts<EditBranchDto>("application/json")
                .Produces<BranchDto>(200)
                .Produces(400);

            branches.MapDelete("/{id:int}", Delete)
                .WithName("DeleteBranch")
                .Produces(204)
                .Produces(400);
        }

        public static async Task<IResult> GetAll([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper)
        {
            var branches = await context.Branches.Include(b => b.Area).ToListAsync();
            return TypedResults.Ok(mapper.Map<IEnumerable<BranchDto>>(branches));
        }

        public static async Task<IResult> GetById([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper, int id)
        {
            var branch = await context.Branches.Include(b => b.Area).FirstOrDefaultAsync(b => b.Id == id);
            return branch != null ? TypedResults.Ok(mapper.Map<BranchDto>(branch)) : TypedResults.NotFound("Branch not found.");
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Create([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper, [FromBody] EditBranchDto branchDto)
        {
            if (branchDto == null) return TypedResults.BadRequest("Invalid branch data.");

            var branch = mapper.Map<Branch>(branchDto);
            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            return TypedResults.Created($"/api/branches/{branch.Id}", mapper.Map<BranchDto>(branch));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Update([FromServices] BlockingApiDbContext context, [FromServices] IMapper mapper, int id, [FromBody] EditBranchDto branchDto)
        {
            var branch = await context.Branches.FindAsync(id);
            if (branch == null) return TypedResults.BadRequest("Invalid branch data.");

            mapper.Map(branchDto, branch);
            context.Branches.Update(branch);
            await context.SaveChangesAsync();

            return TypedResults.Ok(mapper.Map<BranchDto>(branch));
        }

        [Authorize(Roles = "Admin")]
        public static async Task<IResult> Delete([FromServices] BlockingApiDbContext context, int id)
        {
            var branch = await context.Branches.FindAsync(id);
            if (branch == null) return TypedResults.NotFound("Branch not found.");

            context.Branches.Remove(branch);
            await context.SaveChangesAsync();

            return TypedResults.NoContent();
        }
    }
}
