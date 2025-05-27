// CompGateApi.Endpoints/TransferRequestEndpoints.cs
using System.Security.Claims;
using AutoMapper;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Endpoints
{
    public class TransferRequestEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var transfers = app
                .MapGroup("/api/transfers")
                .RequireAuthorization("RequireCompanyUser")
                .WithTags("Transfers");

            transfers.MapGet("/", GetMyTransfers)
                     .Produces<PagedResult<TransferRequestDto>>(200);

            transfers.MapGet("/{id:int}", GetMyTransferById)
                     .Produces<TransferRequestDto>(200)
                     .Produces(404);

            transfers.MapPost("/", CreateTransfer)
                     .Accepts<TransferRequestCreateDto>("application/json")
                     .Produces<TransferRequestDto>(201)
                     .Produces(400)
                     .Produces(401);

            transfers.MapGet("/accounts", LookupAccounts)
             .WithName("LookupAccounts")
             .Produces<List<AccountDto>>(200)
             .Produces(400)
             .Produces(404);

            transfers.MapGet("/statement", GetStatement)
                     .Produces<List<StatementEntryDto>>(200)
                     .Produces(400);

            var admin = app.MapGroup("/api/admin/transfers")
                           .RequireAuthorization("RequireAdminUser")
                           .WithTags("Transfers");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<TransferRequestDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<TransferRequestStatusUpdateDto>("application/json")
                 .Produces<TransferRequestDto>(200)
                 .Produces(400)
                 .Produces(404);
        }

        // Try both ClaimTypes.NameIdentifier and the raw "nameid" JWT claim
        static bool TryGetAuthUserId(HttpContext ctx, out int userId)
        {
            userId = 0;

            // first: the standard NameIdentifier
            var raw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("nameid");

            if (int.TryParse(raw, out userId))
                return true;

            return false;
        }

        public static async Task<IResult> GetMyTransfers(
     HttpContext ctx,
     ITransferRequestRepository repo,
     IMapper mapper,
     IUserRepository userRepo,
     [FromQuery] int page = 1,
     [FromQuery] int limit = 50,
     [FromQuery] string? searchTerm = null)
        {
            if (!TryGetAuthUserId(ctx, out var auth))
                return Results.Unauthorized();

            // load local user
            var me = await userRepo.GetUserByAuthId(auth, ctx.Request.Headers["Authorization"]);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            var total = await repo.GetCountByCompanyAsync(me.CompanyId.Value, searchTerm);
            var list = await repo.GetAllByCompanyAsync(me.CompanyId.Value, searchTerm, page, limit);
            var dtos = list.Select(r => mapper.Map<TransferRequestDto>(r)).ToList();

            return Results.Ok(new PagedResult<TransferRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }


        public static async Task<IResult> GetMyTransferById(
    int id,
    HttpContext ctx,
    ITransferRequestRepository repo,
    IUserRepository userRepo,
    IMapper mapper)
        {
            // 1) Auth
            if (!TryGetAuthUserId(ctx, out var authUserId))
                return Results.Unauthorized();

            // 2) Load our local user & ensure they belong to a company
            var token = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authUserId, token);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            // 3) Fetch the transfer and lock to their company
            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            // 4) Map & return
            return Results.Ok(mapper.Map<TransferRequestDto>(ent));
        }


        public static async Task<IResult> CreateTransfer(
       [FromBody] TransferRequestCreateDto dto,
       HttpContext ctx,
       ITransferRequestRepository repo,
       IUserRepository userRepo,
       IValidator<TransferRequestCreateDto> validator,
       IMapper mapper,
       ILogger<TransferRequestEndpoints> log,
       IHttpClientFactory httpFactory,
       CompGateApiDbContext db)
        {
            // 1) Validate DTO
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            // 2) Auth
            if (!TryGetAuthUserId(ctx, out var authUserId))
                return Results.Unauthorized();

            var me = await userRepo.GetUserByAuthId(
                authUserId, ctx.Request.Headers["Authorization"]);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            // 3) Fetch limits for both packages
            var userPkgId = me.ServicePackageId;
            var companyPkgId = me.CompanyServicePackageId; // however you expose it on UserDetailsDto

            // get all user‐level limits
            var userLimits = await db.TransferLimits
                .Where(l =>
                    l.ServicePackageId == userPkgId &&
                    l.TransactionCategoryId == dto.TransactionCategoryId &&
                    l.CurrencyId == dto.CurrencyId)
                .ToListAsync();

            // get all company‐level limits
            var companyLimits = await db.TransferLimits
                .Where(l =>
                    l.ServicePackageId == companyPkgId &&
                    l.TransactionCategoryId == dto.TransactionCategoryId &&
                    l.CurrencyId == dto.CurrencyId)
                .ToListAsync();

            if (!companyLimits.Any())
                return Results.BadRequest("No transfer limits configured for your company’s package.");

            // if the user has limits defined, use those; otherwise use the company limits
            var useUserLimits = userLimits.Any();
            var limitsToCheck = useUserLimits ? userLimits : companyLimits;

            var now = DateTime.UtcNow;
            foreach (var limit in limitsToCheck)
            {
                // clamp min/max to the intersection of user & company
                var compMatch = companyLimits.Single(l => l.Period == limit.Period);
                decimal effectiveMin = Math.Max(limit.MinAmount, compMatch.MinAmount);
                decimal effectiveMax = Math.Min(limit.MaxAmount, compMatch.MaxAmount);

                // 3a) minimum per‐transaction
                if (dto.Amount < effectiveMin)
                    return Results.BadRequest(
                        $"Minimum amount per {limit.Period} is {effectiveMin}.");

                // calculate start of the period
                DateTime periodStart = limit.Period switch
                {
                    LimitPeriod.Daily => now.Date,
                    LimitPeriod.Weekly => now.Date.AddDays(-((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7),
                    LimitPeriod.Monthly => new DateTime(now.Year, now.Month, 1),
                    _ => DateTime.MinValue
                };

                // 3b) rolling sum
                decimal sumInPeriod;
                if (useUserLimits)
                {
                    // sum *just this user’s* transfers
                    sumInPeriod = await db.TransferRequests
                        .Where(tr =>
                            tr.UserId == me.UserId &&
                            tr.TransactionCategoryId == dto.TransactionCategoryId &&
                            tr.CurrencyId == dto.CurrencyId &&
                            tr.RequestedAt >= periodStart)
                        .SumAsync(tr => (decimal?)tr.Amount) ?? 0m;
                }
                else
                {
                    // sum *all users in the company* (so if company total is exhausted nobody can send more)
                    sumInPeriod = await db.TransferRequests
                        .Where(tr =>
                            tr.CompanyId == me.CompanyId.Value &&
                            tr.TransactionCategoryId == dto.TransactionCategoryId &&
                            tr.CurrencyId == dto.CurrencyId &&
                            tr.RequestedAt >= periodStart)
                        .SumAsync(tr => (decimal?)tr.Amount) ?? 0m;
                }

                if (sumInPeriod + dto.Amount > effectiveMax)
                    return Results.BadRequest(
                        $"Your total for this {limit.Period} would exceed the maximum of {effectiveMax}.");
            }

            // 4) External API call
            var bankClient = httpFactory.CreateClient("BankApi");
            var extPayload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16),
                    userName = "TEDMOB",
                    customerNumber = dto.FromAccount,
                    requestTime = now.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
        {
            { "@TRFCCY", dto.CurrencyId.ToString() },
            { "@SRCACC", dto.FromAccount },
            { "@DSTACC", dto.ToAccount },
            { "@APLYTRN2","N" },
            { "@TRFAMT", ((long)(dto.Amount * 1000)).ToString("D15") },
            { "@NR2",     "" }
        }
            };

            var extResp = await bankClient.PostAsJsonAsync("/api/mobile/postTransfer", extPayload);
            if (!extResp.IsSuccessStatusCode)
            {
                log.LogError("Bank transfer failed: {Status}", extResp.StatusCode);
                return Results.BadRequest("External transfer failed.");
            }

            // 5) Persist locally
            var tr = new TransferRequest
            {
                UserId = me.UserId,
                CompanyId = me.CompanyId.Value,
                TransactionCategoryId = dto.TransactionCategoryId,
                FromAccount = dto.FromAccount,
                ToAccount = dto.ToAccount,
                Amount = dto.Amount,
                CurrencyId = dto.CurrencyId,
                ServicePackageId = me.ServicePackageId,
                Status = "Completed",
                RequestedAt = now,
                Description = dto.Description
            };
            await repo.CreateAsync(tr);

            // 6) Map & return
            var outDto = mapper.Map<TransferRequestDto>(tr);
            return Results.Created($"/api/transfers/{tr.Id}", outDto);
        }


        public static async Task<IResult> LookupAccounts(
    [FromQuery] string account,
    ITransferRequestRepository repo)
        {
            // Must be exactly 6 or 13 digits
            if (account.Length != 6 && account.Length != 13)
                return Results.BadRequest("Account must be 6-digit code or 13-digit account.");

            // Grab them all
            var all = await repo.GetAccountsAsync(account);

            if (account.Length == 6)
            {
                // return the full list for a code
                return Results.Ok(all);
            }
            else
            {
                // full account → filter down
                var matches = all
                    .Where(a => a.AccountString == account)
                    .ToList();

                if (matches.Count == 0)
                    return Results.NotFound($"Account {account} not found.");

                return Results.Ok(matches);
            }
        }

        public static async Task<IResult> GetStatement(
            [FromQuery] string account,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            ITransferRequestRepository repo)
        {
            if (fromDate > toDate)
                return Results.BadRequest("FromDate must precede ToDate.");

            var stm = await repo.GetStatementAsync(account, fromDate, toDate);
            return Results.Ok(stm);
        }

        public static async Task<IResult> GetAllAdmin(
            ITransferRequestRepository repo,
            IMapper mapper,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null)
        {
            var total = await repo.GetCountAsync(searchTerm);
            var list = await repo.GetAllAsync(searchTerm, page, limit);
            var dtos = list.Select(r => mapper.Map<TransferRequestDto>(r)).ToList();

            return Results.Ok(new PagedResult<TransferRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> UpdateStatus(
            int id,
            [FromBody] TransferRequestStatusUpdateDto dto,
            ITransferRequestRepository repo,
            IValidator<TransferRequestStatusUpdateDto> validator,
            IMapper mapper,
            ILogger<TransferRequestEndpoints> log)
        {
            var v = await validator.ValidateAsync(dto);
            if (!v.IsValid)
                return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("Not found.");

            ent.Status = dto.Status;
            await repo.UpdateAsync(ent);

            return Results.Ok(mapper.Map<TransferRequestDto>(ent));
        }
    }
}
