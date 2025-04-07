using Microsoft.AspNetCore.Mvc;
using BlockingApi.Data.Context;
using AutoMapper;
using BlockingApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using BlockingApi.Core.Dtos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlockingApi.Abstractions;

namespace BlockingApi.Endpoints
{
    public class BranchesEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var branches = app.MapGroup("/api/branches").RequireAuthorization("requireAuthUser");

            // GET endpoint with optional searchBy, search, page, and limit.
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

        // GET: Retrieve all branches with optional filtering and pagination.
        public static async Task<IResult> GetAll(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            [FromQuery] string? search,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100000)
        {
            // Build the base query including the related Area.
            var query = context.Branches.Include(b => b.Area).AsQueryable();

            // If both search and searchBy parameters are provided, apply filtering.
            if (!string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(searchBy))
            {
                switch (searchBy.ToLower())
                {
                    case "cabbn":
                        query = query.Where(b => b.CABBN.Contains(search));
                        break;
                    case "name":
                        query = query.Where(b => b.Name.Contains(search));
                        break;
                    case "address":
                        query = query.Where(b => b.Address.Contains(search));
                        break;
                    case "phone":
                        query = query.Where(b => b.Phone.Contains(search));
                        break;
                    default:
                        // Optionally ignore or return an empty result if searchBy is not recognized.
                        break;
                }
            }

            // Order the query by Id.
            query = query.OrderBy(b => b.Id);

            // Apply pagination.
            var pagedBranches = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            return TypedResults.Ok(mapper.Map<IEnumerable<BranchDto>>(pagedBranches));
        }

        public static async Task<IResult> GetById(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            int id)
        {
            var branch = await context.Branches.Include(b => b.Area).FirstOrDefaultAsync(b => b.Id == id);
            return branch != null ? TypedResults.Ok(mapper.Map<BranchDto>(branch)) : TypedResults.NotFound("Branch not found.");
        }

        public static async Task<IResult> Create(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            [FromBody] EditBranchDto branchDto)
        {
            if (branchDto == null) return TypedResults.BadRequest("Invalid branch data.");

            var branch = mapper.Map<Branch>(branchDto);
            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            return TypedResults.Created($"/api/branches/{branch.Id}", mapper.Map<BranchDto>(branch));
        }

        public static async Task<IResult> Update(
            [FromServices] BlockingApiDbContext context,
            [FromServices] IMapper mapper,
            int id,
            [FromBody] EditBranchDto branchDto)
        {
            var branch = await context.Branches.FindAsync(id);
            if (branch == null) return TypedResults.BadRequest("Invalid branch data.");

            mapper.Map(branchDto, branch);
            context.Branches.Update(branch);
            await context.SaveChangesAsync();

            return TypedResults.Ok(mapper.Map<BranchDto>(branch));
        }

        public static async Task<IResult> Delete(
            [FromServices] BlockingApiDbContext context,
            int id)
        {
            var branch = await context.Branches.FindAsync(id);
            if (branch == null) return TypedResults.NotFound("Branch not found.");

            context.Branches.Remove(branch);
            await context.SaveChangesAsync();

            return TypedResults.NoContent();
        }
    }
}
