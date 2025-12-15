// CompGateApi.Endpoints/ForeignTransferEndpoints.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CompGateApi.Abstractions;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompGateApi.Endpoints
{
    public class ForeignTransferEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // Company portal
            var userGroup = app.MapGroup("/api/foreigntransfers")
                               .RequireAuthorization("RequireCompanyUser")
                               .WithTags("ForeignTransfers");

            userGroup.MapGet("/", GetMyRequests)
                     .Produces<PagedResult<ForeignTransferDto>>(200);

            userGroup.MapGet("/{id:int}", GetMyById)
                     .Produces<ForeignTransferDto>(200)
                     .Produces(404);

            userGroup.MapPost("/", CreateMyRequest)
                     .Accepts<ForeignTransferCreateDto>("application/json")
                     .Produces<ForeignTransferDto>(201)
                     .Produces(400)
                     .Produces(401);

            userGroup.MapPut("/{id:int}", UpdateMyRequest)
                    .WithName("UpdateForeignTransfer")
                    .Accepts<ForeignTransferCreateDto>("application/json")
                    .Produces<ForeignTransferDto>(200)
                    .Produces(400)
                    .Produces(404);

            // POST alias for update
            userGroup.MapPost("/{id:int}/update", UpdateMyRequest)
                     .Accepts<ForeignTransferCreateDto>("application/json")
                     .Produces<ForeignTransferDto>(200)
                     .Produces(400)
                     .Produces(404);


            // Admin portal
            var admin = app.MapGroup("/api/admin/foreigntransfers")
                           .RequireAuthorization("RequireAdminUser")
                           .WithTags("ForeignTransfers");

            admin.MapGet("/", GetAllAdmin)
                 .Produces<PagedResult<ForeignTransferDto>>(200);

            admin.MapPut("/{id:int}/status", UpdateStatus)
                 .Accepts<ForeignTransferStatusUpdateDto>("application/json")
                 .Produces<ForeignTransferDto>(200)
                 .Produces(400)
                 .Produces(404);

            // POST alias for status update
            admin.MapPost("/{id:int}/status/update", UpdateStatus)
                 .Accepts<ForeignTransferStatusUpdateDto>("application/json")
                 .Produces<ForeignTransferDto>(200)
                 .Produces(400)
                 .Produces(404);

            admin.MapGet("/{id:int}", GetByIdAdmin)
                    .WithName("AdminGetForeignTransferById")
                    .Produces<ForeignTransferDto>(200)
                    .Produces(404);
        }

        private static int GetAuthUserId(HttpContext ctx)
        {
            // token uses "nameid" claim
            var raw = ctx.User.FindFirst("nameid")?.Value;
            if (int.TryParse(raw, out var id)) return id;
            throw new UnauthorizedAccessException("Missing/invalid 'nameid' claim.");
        }

        public static async Task<IResult> GetMyRequests(
            HttpContext ctx,
            [FromServices] IForeignTransferRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromServices] ILogger<ForeignTransferEndpoints> log,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? searchBy = null)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null) return Results.Unauthorized();

            if (!me.CompanyId.HasValue)
                return Results.Unauthorized();
            var cid = me.CompanyId.Value;
            var list = await repo.GetAllByCompanyAsync(cid, searchTerm, searchBy, page, limit);
            var total = await repo.GetCountByCompanyAsync(cid, searchTerm, searchBy);

            var dtos = list.Select(r => new ForeignTransferDto
            {
                Id = r.Id,
                UserId = r.UserId,
                ToBank = r.ToBank,
                Branch = r.Branch,
                ResidentSupplierName = r.ResidentSupplierName,
                ResidentSupplierNationality = r.ResidentSupplierNationality,
                NonResidentPassportNumber = r.NonResidentPassportNumber,
                PlaceOfIssue = r.PlaceOfIssue,
                DateOfIssue = r.DateOfIssue,
                NonResidentNationality = r.NonResidentNationality,
                NonResidentAddress = r.NonResidentAddress,
                TransferAmount = r.TransferAmount,
                ToCountry = r.ToCountry,
                BeneficiaryName = r.BeneficiaryName,
                BeneficiaryAddress = r.BeneficiaryAddress,
                ExternalBankName = r.ExternalBankName,
                ExternalBankAddress = r.ExternalBankAddress,
                TransferToAccountNumber = r.TransferToAccountNumber,
                TransferToAddress = r.TransferToAddress,
                AccountHolderName = r.AccountHolderName,
                PermanentAddress = r.PermanentAddress,
                PurposeOfTransfer = r.PurposeOfTransfer,
                Status = r.Status,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<ForeignTransferDto>
            {
                Data = dtos,
                Page = page,
                Limit = limit,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)limit)
            });
        }

        public static async Task<IResult> GetMyById(
            int id,
            HttpContext ctx,
            [FromServices] IForeignTransferRepository repo,
            [FromServices] IUserRepository userRepo)
        {
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null) return Results.Unauthorized();

            var ent = await repo.GetByIdAsync(id);
            if (ent == null
                || !me.CompanyId.HasValue
                || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            return Results.Ok(new ForeignTransferDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                ToBank = ent.ToBank,
                Branch = ent.Branch,
                ResidentSupplierName = ent.ResidentSupplierName,
                ResidentSupplierNationality = ent.ResidentSupplierNationality,
                NonResidentPassportNumber = ent.NonResidentPassportNumber,
                PlaceOfIssue = ent.PlaceOfIssue,
                DateOfIssue = ent.DateOfIssue,
                NonResidentNationality = ent.NonResidentNationality,
                NonResidentAddress = ent.NonResidentAddress,
                TransferAmount = ent.TransferAmount,
                ToCountry = ent.ToCountry,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAddress = ent.BeneficiaryAddress,
                ExternalBankName = ent.ExternalBankName,
                ExternalBankAddress = ent.ExternalBankAddress,
                TransferToAccountNumber = ent.TransferToAccountNumber,
                TransferToAddress = ent.TransferToAddress,
                AccountHolderName = ent.AccountHolderName,
                PermanentAddress = ent.PermanentAddress,
                PurposeOfTransfer = ent.PurposeOfTransfer,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
        }

        public static async Task<IResult> CreateMyRequest(
            [FromBody] ForeignTransferCreateDto dto,
            HttpContext ctx,
            [FromServices] IForeignTransferRepository repo,
            [FromServices] IUserRepository userRepo,
            [FromServices] IValidator<ForeignTransferCreateDto> validator,
            [FromServices] ILogger<ForeignTransferEndpoints> log)
        {
            var v = await validator.ValidateAsync(dto);
            if (!v.IsValid)
                return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null) return Results.Unauthorized();

            if (!me.CompanyId.HasValue)
                return Results.Unauthorized();


            var ent = new ForeignTransfer
            {
                UserId = me.UserId,
                CompanyId = me.CompanyId.Value,
                ToBank = dto.ToBank,
                Branch = dto.Branch,
                ResidentSupplierName = dto.ResidentSupplierName,
                ResidentSupplierNationality = dto.ResidentSupplierNationality,
                NonResidentPassportNumber = dto.NonResidentPassportNumber,
                PlaceOfIssue = dto.PlaceOfIssue,
                DateOfIssue = dto.DateOfIssue,
                NonResidentNationality = dto.NonResidentNationality,
                NonResidentAddress = dto.NonResidentAddress,
                TransferAmount = dto.TransferAmount,
                ToCountry = dto.ToCountry,
                BeneficiaryName = dto.BeneficiaryName,
                BeneficiaryAddress = dto.BeneficiaryAddress,
                ExternalBankName = dto.ExternalBankName,
                ExternalBankAddress = dto.ExternalBankAddress,
                TransferToAccountNumber = dto.TransferToAccountNumber,
                TransferToAddress = dto.TransferToAddress,
                AccountHolderName = dto.AccountHolderName,
                PermanentAddress = dto.PermanentAddress,
                PurposeOfTransfer = dto.PurposeOfTransfer,
                Status = "Pending"
            };

            await repo.CreateAsync(ent);
            log.LogInformation("Created ForeignTransfer Id={Id}", ent.Id);

            var outDto = new ForeignTransferDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                ToBank = ent.ToBank,
                Branch = ent.Branch,
                ResidentSupplierName = ent.ResidentSupplierName,
                ResidentSupplierNationality = ent.ResidentSupplierNationality,
                NonResidentPassportNumber = ent.NonResidentPassportNumber,
                PlaceOfIssue = ent.PlaceOfIssue,
                DateOfIssue = ent.DateOfIssue,
                NonResidentNationality = ent.NonResidentNationality,
                NonResidentAddress = ent.NonResidentAddress,
                TransferAmount = ent.TransferAmount,
                ToCountry = ent.ToCountry,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAddress = ent.BeneficiaryAddress,
                ExternalBankName = ent.ExternalBankName,
                ExternalBankAddress = ent.ExternalBankAddress,
                TransferToAccountNumber = ent.TransferToAccountNumber,
                TransferToAddress = ent.TransferToAddress,
                AccountHolderName = ent.AccountHolderName,
                PermanentAddress = ent.PermanentAddress,
                PurposeOfTransfer = ent.PurposeOfTransfer,
                Status = ent.Status,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Created($"/api/foreigntransfers/{ent.Id}", outDto);
        }

        public static async Task<IResult> UpdateMyRequest(
    int id,
    [FromBody] ForeignTransferCreateDto dto,
    HttpContext ctx,
    IForeignTransferRepository repo,
    IUserRepository userRepo,
    ILogger<ForeignTransferEndpoints> log)
        {
            log.LogInformation("UpdateMyRequest payload: {@Dto}", dto);

            // auth
            var authId = GetAuthUserId(ctx);
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "";
            var me = await userRepo.GetUserByAuthId(authId, bearer);
            if (me == null || !me.CompanyId.HasValue)
                return Results.Unauthorized();

            // fetch
            var ent = await repo.GetByIdAsync(id);
            if (ent == null || ent.CompanyId != me.CompanyId.Value)
                return Results.NotFound();

            if (ent.Status.Equals("printed", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("Cannot edit a printed form.");

            // update fields
            ent.ToBank = dto.ToBank;
            ent.Branch = dto.Branch;
            ent.ResidentSupplierName = dto.ResidentSupplierName;
            ent.ResidentSupplierNationality = dto.ResidentSupplierNationality;
            ent.NonResidentPassportNumber = dto.NonResidentPassportNumber;
            ent.PlaceOfIssue = dto.PlaceOfIssue;
            ent.DateOfIssue = dto.DateOfIssue;
            ent.NonResidentNationality = dto.NonResidentNationality;
            ent.NonResidentAddress = dto.NonResidentAddress;
            ent.TransferAmount = dto.TransferAmount;
            ent.ToCountry = dto.ToCountry;
            ent.BeneficiaryName = dto.BeneficiaryName;
            ent.BeneficiaryAddress = dto.BeneficiaryAddress;
            ent.ExternalBankName = dto.ExternalBankName;
            ent.ExternalBankAddress = dto.ExternalBankAddress;
            ent.TransferToAccountNumber = dto.TransferToAccountNumber;
            ent.TransferToAddress = dto.TransferToAddress;
            ent.AccountHolderName = dto.AccountHolderName;
            ent.PermanentAddress = dto.PermanentAddress;
            ent.PurposeOfTransfer = dto.PurposeOfTransfer;
            // leave Status and Reason as-is

            await repo.UpdateAsync(ent);
            log.LogInformation("Updated ForeignTransfer Id={Id}", id);

            var outDto = new ForeignTransferDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                ToBank = ent.ToBank,
                Branch = ent.Branch,
                ResidentSupplierName = ent.ResidentSupplierName,
                ResidentSupplierNationality = ent.ResidentSupplierNationality,
                NonResidentPassportNumber = ent.NonResidentPassportNumber,
                PlaceOfIssue = ent.PlaceOfIssue,
                DateOfIssue = ent.DateOfIssue,
                NonResidentNationality = ent.NonResidentNationality,
                NonResidentAddress = ent.NonResidentAddress,
                TransferAmount = ent.TransferAmount,
                ToCountry = ent.ToCountry,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAddress = ent.BeneficiaryAddress,
                ExternalBankName = ent.ExternalBankName,
                ExternalBankAddress = ent.ExternalBankAddress,
                TransferToAccountNumber = ent.TransferToAccountNumber,
                TransferToAddress = ent.TransferToAddress,
                AccountHolderName = ent.AccountHolderName,
                PermanentAddress = ent.PermanentAddress,
                PurposeOfTransfer = ent.PurposeOfTransfer,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };

            return Results.Ok(outDto);
        }


        public static async Task<IResult> GetAllAdmin(
            [FromServices] IForeignTransferRepository repo,
            [FromServices] ILogger<ForeignTransferEndpoints> log,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? searchBy = null)
        {
            var list = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var total = await repo.GetCountAsync(searchTerm, searchBy);

            var dtos = list.Select(r => new ForeignTransferDto
            {
                Id = r.Id,
                UserId = r.UserId,
                ToBank = r.ToBank,
                Branch = r.Branch,
                ResidentSupplierName = r.ResidentSupplierName,
                ResidentSupplierNationality = r.ResidentSupplierNationality,
                NonResidentPassportNumber = r.NonResidentPassportNumber,
                PlaceOfIssue = r.PlaceOfIssue,
                DateOfIssue = r.DateOfIssue,
                NonResidentNationality = r.NonResidentNationality,
                NonResidentAddress = r.NonResidentAddress,
                TransferAmount = r.TransferAmount,
                ToCountry = r.ToCountry,
                BeneficiaryName = r.BeneficiaryName,
                BeneficiaryAddress = r.BeneficiaryAddress,
                ExternalBankName = r.ExternalBankName,
                ExternalBankAddress = r.ExternalBankAddress,
                TransferToAccountNumber = r.TransferToAccountNumber,
                TransferToAddress = r.TransferToAddress,
                AccountHolderName = r.AccountHolderName,
                PermanentAddress = r.PermanentAddress,
                PurposeOfTransfer = r.PurposeOfTransfer,
                Status = r.Status,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Results.Ok(new PagedResult<ForeignTransferDto>
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
            [FromBody] ForeignTransferStatusUpdateDto dto,
            [FromServices] IForeignTransferRepository repo,
            [FromServices] IValidator<ForeignTransferStatusUpdateDto> validator,
            [FromServices] ILogger<ForeignTransferEndpoints> log)
        {
            var v = await validator.ValidateAsync(dto);
            if (!v.IsValid)
                return Results.BadRequest(v.Errors.Select(e => e.ErrorMessage));

            var ent = await repo.GetByIdAsync(id);
            if (ent == null) return Results.NotFound();

            ent.Status = dto.Status;
            ent.Reason = dto.Reason;

            await repo.UpdateAsync(ent);

            return Results.Ok(new ForeignTransferDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                ToBank = ent.ToBank,
                Branch = ent.Branch,
                ResidentSupplierName = ent.ResidentSupplierName,
                ResidentSupplierNationality = ent.ResidentSupplierNationality,
                NonResidentPassportNumber = ent.NonResidentPassportNumber,
                PlaceOfIssue = ent.PlaceOfIssue,
                DateOfIssue = ent.DateOfIssue,
                NonResidentNationality = ent.NonResidentNationality,
                NonResidentAddress = ent.NonResidentAddress,
                TransferAmount = ent.TransferAmount,
                ToCountry = ent.ToCountry,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAddress = ent.BeneficiaryAddress,
                ExternalBankName = ent.ExternalBankName,
                ExternalBankAddress = ent.ExternalBankAddress,
                TransferToAccountNumber = ent.TransferToAccountNumber,
                TransferToAddress = ent.TransferToAddress,
                AccountHolderName = ent.AccountHolderName,
                PermanentAddress = ent.PermanentAddress,
                PurposeOfTransfer = ent.PurposeOfTransfer,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            });
        }

        public static async Task<IResult> GetByIdAdmin(
    int id,
    [FromServices] IForeignTransferRepository repo,
    [FromServices] ILogger<ForeignTransferEndpoints> log)
        {
            log.LogInformation("Admin:GetByIdAdmin({Id})", id);

            var ent = await repo.GetByIdAsync(id);
            if (ent == null)
                return Results.NotFound("Foreign transfer not found.");

            var dto = new ForeignTransferDto
            {
                Id = ent.Id,
                UserId = ent.UserId,
                ToBank = ent.ToBank,
                Branch = ent.Branch,
                ResidentSupplierName = ent.ResidentSupplierName,
                ResidentSupplierNationality = ent.ResidentSupplierNationality,
                NonResidentPassportNumber = ent.NonResidentPassportNumber,
                PlaceOfIssue = ent.PlaceOfIssue,
                DateOfIssue = ent.DateOfIssue,
                NonResidentNationality = ent.NonResidentNationality,
                NonResidentAddress = ent.NonResidentAddress,
                TransferAmount = ent.TransferAmount,
                ToCountry = ent.ToCountry,
                BeneficiaryName = ent.BeneficiaryName,
                BeneficiaryAddress = ent.BeneficiaryAddress,
                ExternalBankName = ent.ExternalBankName,
                ExternalBankAddress = ent.ExternalBankAddress,
                TransferToAccountNumber = ent.TransferToAccountNumber,
                TransferToAddress = ent.TransferToAddress,
                AccountHolderName = ent.AccountHolderName,
                PermanentAddress = ent.PermanentAddress,
                PurposeOfTransfer = ent.PurposeOfTransfer,
                Status = ent.Status,
                Reason = ent.Reason,
                CreatedAt = ent.CreatedAt,
                UpdatedAt = ent.UpdatedAt
            };
            return Results.Ok(dto);
        }
    }
}
