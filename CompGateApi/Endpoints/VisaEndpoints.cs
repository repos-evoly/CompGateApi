// CompGateApi.Endpoints/VisaEndpoints.cs
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;
using CompGateApi.Abstractions;
using System.Text.Json;

namespace CompGateApi.Endpoints
{
    public class VisaEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var grp = app.MapGroup("/api/admin/visas")
                         .RequireAuthorization("RequireAdminUser")
                         .WithTags("Visa");

            grp.MapGet("/", GetAll).Produces<List<VisaDto>>(200);
            grp.MapGet("/{id:int}", GetById).Produces<VisaDto>(200).Produces(404);

            // Create Visa + (optional) attachments in ONE multipart request
            grp.MapPost("/", Create)
                .Accepts<IFormFile>("multipart/form-data")
                .Produces<VisaDto>(201)
                .Produces(400);

            grp.MapPut("/{id:int}", Update)
               .Accepts<VisaUpdateDto>("application/json")
               .Produces<VisaDto>(200)
               .Produces(404);
            // POST alias for update
            grp.MapPost("/{id:int}/update", Update)
               .Accepts<VisaUpdateDto>("application/json")
               .Produces<VisaDto>(200)
               .Produces(404);

            grp.MapDelete("/{id:int}", Delete)
               .Produces(204)
               .Produces(404);
            // POST alias for delete
            grp.MapPost("/{id:int}/delete", Delete)
               .Produces(204)
               .Produces(404);

            var comp = app.MapGroup("/api/visas/company")
                    .RequireAuthorization("RequireCompanyUser")
                    .WithTags("Visa");

            comp.MapGet("/", GetAll).Produces<List<VisaDto>>(200);
            comp.MapGet("/{id:int}", GetById).Produces<VisaDto>(200).Produces(404);

            // Create Visa + (optional) attachments in ONE multipart request

        }

        public static async Task<IResult> GetAll(
     [FromServices] IVisaRepository repo,
     [FromServices] IAttachmentRepository attRepo,
     CancellationToken ct)
        {
            var visas = await repo.GetAllAsync(ct);

            // N+1 (simple). If performance matters, add a batch API in the repo later.
            var result = new List<VisaDto>();
            foreach (var v in visas)
            {
                var atts = await attRepo.GetByVisa(v.Id);
                result.Add(ToDto(v, atts.ToList()));
            }

            return Results.Ok(result);
        }


        public static async Task<IResult> GetById(
      [FromServices] IVisaRepository repo,
      [FromServices] IAttachmentRepository attRepo,
      int id,
      CancellationToken ct)
        {
            var visa = await repo.GetByIdAsync(id, ct);
            if (visa == null) return Results.NotFound();

            var atts = await attRepo.GetByVisa(id);
            return Results.Ok(ToDto(visa, atts.ToList()));
        }


        // MULTIPART: fields + files (optional). Supports batch files with Subject[] & Description[] aligned by index.
        public static async Task<IResult> Create(
     [FromServices] IVisaRepository repo,
     [FromServices] IAttachmentRepository attRepo,
     HttpRequest request,
     HttpContext ctx,
     CancellationToken ct)
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("Content-Type must be multipart/form-data.");

            var metaJson = request.Form["visa"].ToString();
            if (string.IsNullOrWhiteSpace(metaJson))
                return Results.BadRequest("Form field 'visa' (JSON) is required.");

            VisaCreateDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<VisaCreateDto>(
                    metaJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? throw new InvalidOperationException("Empty visa JSON.");
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Invalid 'visa' JSON: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(dto.NameEn) || string.IsNullOrWhiteSpace(dto.NameAr))
                return Results.BadRequest("NameEn and NameAr are required.");

            var visa = new Visa
            {
                NameEn = dto.NameEn.Trim(),
                NameAr = dto.NameAr.Trim(),
                Price = dto.Price,
                DescriptionEn = dto.DescriptionEn,
                DescriptionAr = dto.DescriptionAr
            };

            var saved = await repo.CreateAsync(visa, ct);

            // upload any files and collect their DTOs
            var uploaded = new List<AttachmentDto>();
            var files = request.Form.Files;
            if (files.Count > 0)
            {
                var subjects = request.Form["subject"].ToArray();
                var descriptions = request.Form["description"].ToArray();
                var createdBy = ctx.User.Identity?.Name ?? "admin";

                for (int i = 0; i < files.Count; i++)
                {
                    var subject = i < subjects.Length ? subjects[i] ?? string.Empty : string.Empty;
                    var description = i < descriptions.Length ? descriptions[i] ?? string.Empty : string.Empty;

                    var attDto = await attRepo.UploadForVisa(
                        files[i],
                        saved.Id,
                        subject,
                        description,
                        createdBy
                    );
                    uploaded.Add(attDto);
                }
            }

            return Results.Created($"/api/admin/visas/{saved.Id}", ToDto(saved, uploaded));
        }


        public static async Task<IResult> Update(
            [FromServices] IVisaRepository visaRepo,
            int id,
            [FromBody] VisaUpdateDto dto,
            CancellationToken ct)
        {
            var updated = await visaRepo.UpdateAsync(id, new Visa
            {
                NameEn = dto.NameEn.Trim(),
                NameAr = dto.NameAr.Trim(),
                Price = dto.Price,
                DescriptionEn = dto.DescriptionEn,
                DescriptionAr = dto.DescriptionAr
            }, ct);

            return updated == null ? Results.NotFound() : Results.Ok(ToDto(updated));
        }

        public static async Task<IResult> Delete(
            [FromServices] IVisaRepository visaRepo,
            int id,
            CancellationToken ct)
        {
            var ok = await visaRepo.DeleteAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        }

        private static VisaDto ToDto(Visa v, IReadOnlyList<AttachmentDto>? attachments = null) => new()
        {
            Id = v.Id,
            NameEn = v.NameEn,
            NameAr = v.NameAr,
            Price = v.Price,
            DescriptionEn = v.DescriptionEn,
            DescriptionAr = v.DescriptionAr,
            Attachments = attachments != null ? attachments.ToList() : new List<AttachmentDto>()
        };

    }
}
