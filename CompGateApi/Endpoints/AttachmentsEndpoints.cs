// CompGateApi.Endpoints/CompanyAttachmentsEndpoints.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Abstractions;

namespace CompGateApi.Endpoints
{
  public class CompanyAttachmentsEndpoints : IEndpoints
  {
    public void RegisterEndpoints(WebApplication app)
    {
      var grp = app
          .MapGroup("/api/companies/{code}/attachments")
          // .RequireAuthorization("RequireCompanyUser")
          // .RequireAuthorization("RequireCompanyAdmin")
          .WithTags("CompanyAttachments");

      // GET /api/companies/{code}/attachments
      grp.MapGet("/", GetByCompany)
         .WithName("GetCompanyAttachments")
         .Produces<IEnumerable<AttachmentDto>>(StatusCodes.Status200OK)
         .Produces(StatusCodes.Status404NotFound);

      // POST /api/companies/{code}/attachments
      grp.MapPost("/", Upload)
         .WithName("UploadCompanyAttachment")
         .Accepts<IFormFile>("multipart/form-data")
         .Produces<AttachmentDto>(StatusCodes.Status201Created)
         .Produces(StatusCodes.Status400BadRequest)
         .Produces(StatusCodes.Status404NotFound);

      grp.MapPost("/UploadBatch", UploadBatch)
        .WithName("UploadCompanyAttachmentsBatch")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<IEnumerable<AttachmentDto>>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

      // DELETE /api/companies/{code}/attachments/{id}
      grp.MapDelete("/{id:guid}", Delete)
         .WithName("DeleteCompanyAttachment")
         .Produces<AttachmentDto>(StatusCodes.Status200OK)
         .Produces(StatusCodes.Status404NotFound);
    }

    public static async Task<IResult> GetByCompany(
         [FromServices] ICompanyRepository companyRepo,
         [FromServices] IAttachmentRepository repo,
         [FromRoute] string code,
         [FromQuery] string? subject)    // 🔸 NEW
    {
      var company = await companyRepo.GetByCodeAsync(code);
      if (company == null)
        return Results.NotFound($"Company '{code}' not found.");

      var list = await repo.GetByCompany(company.Id, subject);   // 🔸 pass filter
      return Results.Ok(list);
    }

    public static async Task<IResult> Upload(
        [FromServices] ICompanyRepository companyRepo,
        [FromServices] IAttachmentRepository repo,
        HttpRequest request,
        [FromRoute] string code)
    {
      var company = await companyRepo.GetByCodeAsync(code);
      if (company == null)
        return Results.NotFound($"Company '{code}' not found.");

      if (!request.HasFormContentType || request.Form.Files.Count == 0)
        return Results.BadRequest("No file uploaded.");

      var file = request.Form.Files[0];
      var subject = request.Form["Subject"].ToString();
      var description = request.Form["Description"].ToString();
      var createdBy = request.Form["CreatedBy"].ToString();

      var dto = await repo.Upload(
          file,
          company.Id,
          subject,
          description,
          createdBy
      );

      return Results.Created(
          $"/api/companies/{code}/attachments/{dto.Id}",
          dto
      );
    }

    // in CompGateApi.Endpoints/CompanyAttachmentsEndpoints.cs
    public static async Task<IResult> UploadBatch(
        [FromServices] ICompanyRepository companyRepo,
        [FromServices] IAttachmentRepository repo,
        HttpRequest request,
        [FromRoute] string code)
    {
      // 1) Get company
      var company = await companyRepo.GetByCodeAsync(code);
      if (company == null)
        return Results.NotFound($"Company '{code}' not found.");

      // 2) Must be multipart/form-data
      if (!request.HasFormContentType)
        return Results.BadRequest("Request must be multipart/form-data.");

      var files = request.Form.Files;
      var subjects = request.Form["Subject"].ToArray();
      var descriptions = request.Form["Description"].ToArray();
      var createdBy = request.Form["CreatedBy"].ToString();

      // 3) Validate counts
      if (files.Count == 0)
        return Results.BadRequest("No files uploaded.");

      if (subjects.Length != files.Count || descriptions.Length != files.Count)
        return Results.BadRequest("Each file must have a matching Subject and Description.");

      // 4) Loop & upload
      var dtos = new List<AttachmentDto>();
      for (int i = 0; i < files.Count; i++)
      {
        var file = files[i];
        var subject = subjects[i] ?? string.Empty;
        var description = descriptions[i] ?? string.Empty;

        var dto = await repo.Upload(
            file,
            company.Id,
            subject,
            description,
            createdBy
        );
        dtos.Add(dto);
      }

      // 5) Return the batch
      // 201 with a Location header pointing to the list endpoint
      return Results.Created(
          $"/api/companies/{code}/attachments",
          dtos
      );
    }


    public static async Task<IResult> Delete(
        [FromServices] IAttachmentRepository repo,
        [FromRoute] Guid id)
    {
      var dto = await repo.Delete(id);
      return dto == null
           ? Results.NotFound()
           : Results.Ok(dto);
    }


  }
}
