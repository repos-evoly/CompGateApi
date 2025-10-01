// CompGateApi.Endpoints/CheckRequestEndpoints.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class CheckRequestEndpoints : IEndpoints
    {
        // Configure your TransactionCategory for Check Requests
        private const int TRXCAT_CHECKREQUEST = 5;   // ← set to your real category id
        private const int DEFAULT_UNIT = 1;          // unit for check request pricing
        private const string DEFAULT_CURRENCY = "LYD";

        public void RegisterEndpoints(WebApplication app)
        {
            // ── COMPANY PORTAL ─────────────────────────────────────────────
            var company = app
                .MapGroup("/api/checkrequests")
                .WithTags("CheckRequests")
                .RequireAuthorization("RequireCompanyUser");

            company.MapGet("/", GetCompanyRequests)
                   .WithName("GetCompanyCheckRequests")
                   .Produces<PagedResult<CheckRequestDto>>(200);

            company.MapGet("/{id:int}", GetCompanyRequestById)
                   .WithName("GetCompanyCheckRequestById")
                   .Produces<CheckRequestDto>(200)
                   .Produces(404);

            company.MapPost("/", CreateCompanyRequest)
                   .WithName("CreateCheckRequest")
                   .Accepts<CheckRequestCreateDto>("application/json")
                   .Produces<CheckRequestDto>(201)
                   .Produces(400)
                   .Produces(401);

            company.MapPut("/{id:int}", UpdateCompanyRequest)
                   .WithName("UpdateCheckRequest")
                   .Accepts<CheckRequestCreateDto>("application/json")
                   .Produces<CheckRequestDto>(200)
                   .Produces(400)
                   .Produces(404);

            // ── ADMIN PORTAL ───────────────────────────────────────────────
            var admin = app
                .MapGroup("/api/admin/checkrequests")
                .WithTags("CheckRequests")
                .RequireAuthorization("RequireAdminUser")
                .RequireAuthorization("AdminAccess");

            admin.MapGet("/", AdminGetAll)
                 .WithName("AdminGetAllCheckRequests")
                 .Produces<PagedResult<CheckRequestDto>>(200);

            admin.MapGet("/{id:int}", AdminGetById)
                 .WithName("AdminGetCheckRequestById")
                 .Produces<CheckRequestDto>(200)
                 .Produces(404);

            admin.MapPut("/{id:int}/status", AdminUpdateStatus)
                 .WithName("AdminUpdateCheckRequestStatus")
                 .Accepts<CheckRequestStatusUpdateDto>("application/json")
                 .Produces<CheckRequestDto>(200)
                 .Produces(400)
                 .Produces(404);
        }

        // helper to extract authenticated user
        private static int GetAuthUserId(HttpContext ctx)
        {
            var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? ctx.User.FindFirst("nameid")?.Value;
            if (!int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException(
                    $"Missing or invalid 'nameid' claim. Raw='{raw ?? "(null)"}'.");
            return id;
        }

        // branch-aware GL1 resolver (same logic you use elsewhere)
        private static string ResolveDestinationAccount(string? pricingGl1, string fromAccount, string? branchField)
        {
            if (string.IsNullOrWhiteSpace(pricingGl1))
                throw new ArgumentException("Pricing.GL1 is not configured.");

            if (!string.IsNullOrWhiteSpace(branchField) &&
                branchField.Trim().Equals("xxxx", StringComparison.OrdinalIgnoreCase))
            {
                var senderBranch = (fromAccount?.Length >= 4) ? fromAccount.Substring(0, 4) : "";
                if (string.IsNullOrWhiteSpace(senderBranch) || senderBranch.Length != 4)
                    throw new ArgumentException("Cannot derive branch from sender account.");

                if (pricingGl1.IndexOf("{BRANCH}", StringComparison.OrdinalIgnoreCase) >= 0)
                    return pricingGl1.Replace("{BRANCH}", senderBranch, StringComparison.OrdinalIgnoreCase);

                if (pricingGl1.Length == 13 && fromAccount?.Length == 13)
                    return senderBranch + pricingGl1.Substring(4);
            }

            return pricingGl1;
        }

        private static decimal SumLineItemsLyd(IEnumerable<CheckRequestLineItem> items)
        {
            decimal total = 0m;
            foreach (var li in items)
            {
                if (!string.IsNullOrWhiteSpace(li.Lyd) && decimal.TryParse(li.Lyd, out var v))
                    total += v;
            }
            return total;
        }

        // ── COMPANY: list requests by company ───────────────────────────
        public static async Task<IResult> GetCompanyRequests(
            HttpContext ctx,
            ICheckRequestRepository repo,
            IRepresentativeRepository repRepo,
            IUserRepository userRepo,
            ILogger<CheckRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var cid = me.CompanyId.Value;
                var list = await repo.GetAllByCompanyAsync(cid, searchTerm, searchBy, page, limit);
                var total = await repo.GetCountByCompanyAsync(cid, searchTerm, searchBy);

                var dtos = new List<CheckRequestDto>();
                foreach (var r in list)
                {
                    var dto = new CheckRequestDto
                    {
                        Id = r.Id,
                        UserId = r.UserId,
                        Branch = r.Branch,
                        BranchNum = r.BranchNum,
                        Date = r.Date,
                        CustomerName = r.CustomerName,
                        CardNum = r.CardNum,
                        AccountNum = r.AccountNum,
                        Beneficiary = r.Beneficiary,
                        Phone = r.Phone,
                        Status = r.Status,
                        Reason = r.Reason,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        LineItems = r.LineItems.Select(li => new CheckRequestLineItemDto
                        {
                            Id = li.Id,
                            Dirham = li.Dirham,
                            Lyd = li.Lyd
                        }).ToList(),
                        RepresentativeId = r.RepresentativeId
                    };

                    if (r.RepresentativeId.HasValue)
                    {
                        var rep = await repRepo.GetByIdAsync(r.RepresentativeId.Value);
                        if (rep != null)
                        {
                            dto.Representative = new RepresentativeDto
                            {
                                Id = rep.Id,
                                Name = rep.Name,
                                Number = rep.Number
                            };
                        }
                    }

                    dtos.Add(dto);
                }

                return Results.Ok(new PagedResult<CheckRequestDto>
                {
                    Data = dtos,
                    Page = page,
                    Limit = limit,
                    TotalPages = (int)Math.Ceiling(total / (double)limit),
                    TotalRecords = total
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in GetCompanyRequests");
                return Results.Unauthorized();
            }
        }

        // ── COMPANY: get single by id ─────────────────────────────────
        public static async Task<IResult> GetCompanyRequestById(
            int id,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IRepresentativeRepository repRepo,
            IUserRepository userRepo,
            ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("GetCompanyRequestById({Id})", id);
            try
            {
                var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? ctx.User.FindFirst("nameid")?.Value;
                if (!int.TryParse(raw, out var authId))
                    return Results.Unauthorized();

                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound("Check request not found.");

                Representative? rep = null;
                if (ent.RepresentativeId.HasValue)
                    rep = await repRepo.GetByIdAsync(ent.RepresentativeId.Value);

                var dto = new CheckRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    Branch = ent.Branch,
                    BranchNum = ent.BranchNum,
                    Date = ent.Date,
                    CustomerName = ent.CustomerName,
                    CardNum = ent.CardNum,
                    AccountNum = ent.AccountNum,
                    Beneficiary = ent.Beneficiary,
                    Phone = ent.Phone,
                    Status = ent.Status,
                    Reason = ent.Reason,
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt,
                    RepresentativeId = ent.RepresentativeId,
                    Representative = rep == null ? null : new RepresentativeDto
                    {
                        Id = rep.Id,
                        Name = rep.Name,
                        Number = rep.Number,
                        PassportNumber = rep.PassportNumber,
                        IsActive = rep.IsActive,
                        IsDeleted = rep.IsDeleted,
                        PhotoUrl = rep.PhotoUrl,
                        CreatedAt = rep.CreatedAt,
                        UpdatedAt = rep.UpdatedAt
                    },
                    LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                    {
                        Id = li.Id,
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList()
                };

                return Results.Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in GetCompanyRequestById");
                return Results.Unauthorized();
            }
        }

        // ───────────────────────────────
        // Add these small helpers
        // ───────────────────────────────
        private static decimal ParseFirstLineAmountLyd(CheckRequestCreateDto dto)
        {
            if (dto.LineItems == null || dto.LineItems.Count == 0)
                throw new ArgumentException("At least one line item is required.");

            var first = dto.LineItems[0];
            // Lyd and Dirham are strings in your model; parse safely
            decimal lyd = 0m, dirham = 0m;
            if (!string.IsNullOrWhiteSpace(first.Lyd)) decimal.TryParse(first.Lyd, out lyd);
            if (!string.IsNullOrWhiteSpace(first.Dirham)) decimal.TryParse(first.Dirham, out dirham);

            // Libya: 1 LYD = 1000 dirhams → 500 dirhams = 0.500 LYD
            return lyd + (dirham / 1000m);
        }

        private static string BuildBranchGlFrom(string? branchNum, string? pricingGl1)
        {
            // Prefer configured GL1 with {BRANCH} token; fallback to BranchNum + 831892434
            var br = (branchNum ?? "").Trim();
            if (br.Length != 4) throw new ArgumentException("BranchNum must be exactly 4 digits.");

            if (!string.IsNullOrWhiteSpace(pricingGl1) &&
                pricingGl1.IndexOf("{BRANCH}", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return pricingGl1.Replace("{BRANCH}", br, StringComparison.InvariantCultureIgnoreCase);
            }

            return $"{br}831892434"; // 4 + 9 = 13 digits
        }


        // ── COMPANY: create new request (+ debit now) ─────────────────
        // ── COMPANY: create new request (+ debit now) ─────────────────
        public static async Task<IResult> CreateCompanyRequest(
            [FromBody] CheckRequestCreateDto dto,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            IGenericTransferRepository genericTransferRepo,
            CompGateApiDbContext db,
            IValidator<CheckRequestCreateDto> validator,
            ILogger<CheckRequestEndpoints> log)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                // 0) Guard rails
                if (string.IsNullOrWhiteSpace(dto.AccountNum))
                    return Results.BadRequest("AccountNum (debit) is required.");
                if (string.IsNullOrWhiteSpace(dto.BranchNum) || dto.BranchNum.Trim().Length != 4)
                    return Results.BadRequest("BranchNum must be exactly 4 digits.");
                if (dto.LineItems == null || dto.LineItems.Count == 0)
                    return Results.BadRequest("At least one line item is required.");

                // 1) Persist the form first (Pending)
                var ent = new CheckRequest
                {
                    UserId = me.UserId,
                    CompanyId = me.CompanyId.Value,
                    Branch = dto.Branch,
                    BranchNum = dto.BranchNum,
                    Date = dto.Date,
                    CustomerName = dto.CustomerName,
                    CardNum = dto.CardNum,
                    AccountNum = dto.AccountNum,
                    Beneficiary = dto.Beneficiary,
                    Phone = dto.Phone,
                    Status = "Pending",
                    RepresentativeId = dto.RepresentativeId,
                    LineItems = dto.LineItems.Select(li => new CheckRequestLineItem
                    {
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList()
                };
                await repo.CreateAsync(ent);
                log.LogInformation("Created CheckRequest Id={Id}", ent.Id);

                // 2) Pricing (TrxCatId=TRXCAT_CHECKREQUEST, Unit=1) → only to fetch GL1/NR2/Codes
                var pricing = await db.Pricings.AsNoTracking()
                                  .Where(p => p.TrxCatId == TRXCAT_CHECKREQUEST && p.Unit == DEFAULT_UNIT)
                                  .FirstOrDefaultAsync();

                // 3) Amount = first line (LYD + dirham/1000)
                decimal amountToCharge;
                try
                {
                    amountToCharge = ParseFirstLineAmountLyd(dto);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(ex.Message);
                }

                if (amountToCharge <= 0m)
                    return Results.BadRequest("Computed amount (first line) must be > 0.");

                // 4) Destination GL = BranchNum + 831892434, unless GL1 with {BRANCH} is configured
                string toAccount;
                try
                {
                    toAccount = BuildBranchGlFrom(dto.BranchNum, pricing?.GL1);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Failed to build destination GL for CheckRequest.");
                    return Results.BadRequest(ex.Message);
                }

                // 5) Narrative & codes from pricing (optional)
                var narrative = string.IsNullOrWhiteSpace(pricing?.NR2)
                    ? "Check request amount"
                    : pricing!.NR2;

                // 6) Debit via GenericTransferRepository → CompanyGatewayPostTransfer
                var debit = await genericTransferRepo.DebitForServiceAsync(
                    userId: me.UserId,
                    companyId: me.CompanyId.Value,
                    servicePackageId: (me.ServicePackageId as int?) ?? 0,
                    trxCategoryId: TRXCAT_CHECKREQUEST,
                    fromAccount: dto.AccountNum!,
                    toAccount: toAccount,
                    amount: amountToCharge,
                    description: narrative,
                    currencyCode: DEFAULT_CURRENCY,
                    dtc: pricing?.DTC,
                    ctc: pricing?.CTC,
                    dtc2: pricing?.DTC2,
                    ctc2: pricing?.CTC2,
                    applySecondLeg: pricing?.APPLYTR2 ?? false,
                    narrativeOverride: narrative
                );

                if (!debit.Success)
                    return Results.BadRequest(debit.Error);

                // 7) Link transfer + bank ref and return DTO
                ent.TransferRequestId = debit.Entity!.Id;
                ent.BankReference = debit.BankReference;
                await repo.UpdateAsync(ent);

                var outDto = new CheckRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    Branch = ent.Branch,
                    BranchNum = ent.BranchNum,
                    Date = ent.Date,
                    CustomerName = ent.CustomerName,
                    CardNum = ent.CardNum,
                    AccountNum = ent.AccountNum,
                    Beneficiary = ent.Beneficiary,
                    Phone = ent.Phone,
                    Status = ent.Status,
                    RepresentativeId = ent.RepresentativeId ?? 0,
                    LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                    {
                        Id = li.Id,
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList(),
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };

                return Results.Created($"/api/checkrequests/{ent.Id}", outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Unauthorized in CreateCompanyRequest");
                return Results.Unauthorized();
            }
        }


        public static async Task<IResult> UpdateCompanyRequest(
            int id,
            [FromBody] CheckRequestCreateDto dto,
            HttpContext ctx,
            ICheckRequestRepository repo,
            IUserRepository userRepo,
            IValidator<CheckRequestCreateDto> validator,
            ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("UpdateCompanyRequest payload: {@Dto}", dto);

            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                log.LogWarning("Validation failed: {Errors}", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));
            }

            try
            {
                var authId = GetAuthUserId(ctx);
                var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
                var me = await userRepo.GetUserByAuthId(authId, bearer);
                if (me == null || !me.CompanyId.HasValue)
                    return Results.Unauthorized();

                var ent = await repo.GetByIdAsync(id);
                if (ent == null || ent.CompanyId != me.CompanyId.Value)
                    return Results.NotFound();

                if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("Cannot edit a printed form.");

                // update fields
                ent.Branch = dto.Branch;
                ent.BranchNum = dto.BranchNum;
                ent.Date = dto.Date;
                ent.CustomerName = dto.CustomerName;
                ent.CardNum = dto.CardNum;
                ent.AccountNum = dto.AccountNum;
                ent.Beneficiary = dto.Beneficiary;
                ent.Phone = dto.Phone;
                ent.RepresentativeId = dto.RepresentativeId;

                // replace line items
                ent.LineItems = dto.LineItems
                    .Select(li => new CheckRequestLineItem { Dirham = li.Dirham, Lyd = li.Lyd })
                    .ToList();

                await repo.UpdateAsync(ent);
                log.LogInformation("Updated CheckRequest Id={Id}", id);

                var outDto = new CheckRequestDto
                {
                    Id = ent.Id,
                    UserId = ent.UserId,
                    Branch = ent.Branch,
                    BranchNum = ent.BranchNum,
                    Date = ent.Date,
                    CustomerName = ent.CustomerName,
                    CardNum = ent.CardNum,
                    AccountNum = ent.AccountNum,
                    Beneficiary = ent.Beneficiary,
                    Phone = ent.Phone,
                    Status = ent.Status,
                    Reason = ent.Reason,
                    RepresentativeId = ent.RepresentativeId ?? 0,
                    LineItems = ent.LineItems
                        .Select(li => new CheckRequestLineItemDto { Id = li.Id, Dirham = li.Dirham, Lyd = li.Lyd })
                        .ToList(),
                    CreatedAt = ent.CreatedAt,
                    UpdatedAt = ent.UpdatedAt
                };

                return Results.Ok(outDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.LogError(ex, "Auth error in UpdateCompanyRequest");
                return Results.Unauthorized();
            }
        }

        // ── ADMIN: list all ───────────────────────────────────────────
        public static async Task<IResult> AdminGetAll(
            ICheckRequestRepository repo,
            IRepresentativeRepository repRepo,
            ILogger<CheckRequestEndpoints> log,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50)
        {
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = new List<CheckRequestDto>();
            foreach (var r in list)
            {
                var dto = new CheckRequestDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Branch = r.Branch,
                    BranchNum = r.BranchNum,
                    Date = r.Date,
                    CustomerName = r.CustomerName,
                    CardNum = r.CardNum,
                    AccountNum = r.AccountNum,
                    Beneficiary = r.Beneficiary,
                    Phone = r.Phone,
                    Status = r.Status,
                    Reason = r.Reason,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    LineItems = r.LineItems.Select(li => new CheckRequestLineItemDto
                    {
                        Id = li.Id,
                        Dirham = li.Dirham,
                        Lyd = li.Lyd
                    }).ToList(),
                    RepresentativeId = r.RepresentativeId
                };

                if (r.RepresentativeId.HasValue)
                {
                    var rep = await repRepo.GetByIdAsync(r.RepresentativeId.Value);
                    if (rep != null)
                    {
                        dto.Representative = new RepresentativeDto
                        {
                            Id = rep.Id,
                            Name = rep.Name,
                            Number = rep.Number
                        };
                    }
                }

                dtos.Add(dto);
            }

            return Results.Ok(new PagedResult<CheckRequestDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling(total / (double)limit),
                TotalRecords = total
            });
        }

        // ── ADMIN: get by id ──────────────────────────────────────────
        public static async Task<IResult> AdminGetById(
            int id,
            [FromServices] ICheckRequestRepository repo,
            [FromServices] IRepresentativeRepository repRepo,
            [FromServices] ILogger<CheckRequestEndpoints> log)
        {
            log.LogInformation("AdminGetById({Id})", id);

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
            {
                log.LogWarning("CheckRequest {Id} not found", id);
                return Results.NotFound("Check request not found.");
            }

            Representative? rep = null;
            if (ent.RepresentativeId.HasValue)
                rep = await repRepo.GetByIdAsync(ent.RepresentativeId.Value);

            var dto = new CheckRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                BranchNum = ent.BranchNum,
                Date = ent.Date,
                CustomerName = ent.CustomerName,
                CardNum = ent.CardNum,
                AccountNum = ent.AccountNum,
                Beneficiary = ent.Beneficiary,
                Phone = ent.Phone,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt,
                RepresentativeId = ent.RepresentativeId,
                Representative = rep == null ? null : new RepresentativeDto
                {
                    Id = rep.Id,
                    Name = rep.Name,
                    Number = rep.Number,
                    PassportNumber = rep.PassportNumber,
                    IsActive = rep.IsActive,
                    IsDeleted = rep.IsDeleted,
                    PhotoUrl = rep.PhotoUrl,
                    CreatedAt = rep.CreatedAt,
                    UpdatedAt = rep.UpdatedAt
                },
                LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                {
                    Id = li.Id,
                    Dirham = li.Dirham,
                    Lyd = li.Lyd
                }).ToList()
            };

            return Results.Ok(dto);
        }

        // ── ADMIN: update status (refund if Rejected) ─────────────────
        public static async Task<IResult> AdminUpdateStatus(
            int id,
            [FromBody] CheckRequestStatusUpdateDto dto,
            [FromServices] ICheckRequestRepository repo,
            [FromServices] IGenericTransferRepository genericTransferRepo,
            [FromServices] CompGateApiDbContext db,
            [FromServices] IValidator<CheckRequestStatusUpdateDto> validator,
            HttpContext ctx)
        {
            var validation = await validator.ValidateAsync(dto);
            if (!validation.IsValid)
                return Results.BadRequest(validation.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("Check request not found.");

            // If Rejected and we have a transfer → reverse money back to customer
            if (dto.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)
                && ent.TransferRequestId.HasValue)
            {
                var tr = await db.TransferRequests.AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Id == ent.TransferRequestId.Value);

                if (tr == null)
                    return Results.BadRequest("Refund failed: original transfer not found.");

                var originalRef = !string.IsNullOrWhiteSpace(ent.BankReference)
                    ? ent.BankReference!
                    : (string.IsNullOrWhiteSpace(tr.BankReference) ? null : tr.BankReference);

                if (string.IsNullOrWhiteSpace(originalRef))
                    return Results.BadRequest("Refund failed: original bank reference missing.");

                var srcAcc = tr.ToAccount;   // fees (received) → debit
                var dstAcc = tr.FromAccount; // customer (sent)  → credit

                var rv = await genericTransferRepo.RefundByOriginalRefAsync(
                    originalBankRef: originalRef!,
                    currencyCode: tr.CurrencyId == 2 ? "USD" : tr.CurrencyId == 3 ? "EUR" : DEFAULT_CURRENCY,
                    srcAcc: srcAcc,
                    dstAcc: dstAcc,
                    srcAcc2: srcAcc,
                    dstAcc2: dstAcc,
                    amount: tr.Amount,
                    note: $"Refund check request #{ent.Id}");

                if (!rv.Success)
                    return Results.BadRequest("Refund failed: " + rv.Error);
            }

            ent.Status = dto.Status;
            ent.Reason = dto.Reason;
            await repo.UpdateAsync(ent);

            var dtoOut = new CheckRequestDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                Branch = ent.Branch,
                BranchNum = ent.BranchNum,
                Date = ent.Date,
                CustomerName = ent.CustomerName,
                CardNum = ent.CardNum,
                AccountNum = ent.AccountNum,
                Beneficiary = ent.Beneficiary,
                Phone = ent.Phone,
                Status = ent.Status,
                Reason = ent.Reason,
                LineItems = ent.LineItems.Select(li => new CheckRequestLineItemDto
                {
                    Id = li.Id,
                    Dirham = li.Dirham,
                    Lyd = li.Lyd
                }).ToList(),
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dtoOut);
        }
    }
}
