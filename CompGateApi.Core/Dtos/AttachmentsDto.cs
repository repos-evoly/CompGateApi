using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Core.Dtos
{
  public class EditAttachmentDto
  {
    public string AttSubject { get; set; } = string.Empty;

    public string AttFileName { get; set; } = string.Empty;

    public string AttMime { get; set; } = string.Empty;
    public string AttUrl { get; set; } = string.Empty;
    public string AttOriginalFileName { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AttSize { get; set; }
    public string CompanyId { get; set; } = string.Empty;
  }

  public class AttachmentDto : EditAttachmentDto
  {
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
  }

  public class AttachmentUploadDto
  {
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = null!;

    [FromForm]
    public string Subject { get; set; } = null!;

    [FromForm]
    public string Description { get; set; } = null!;

    [FromForm]
    public string CreatedBy { get; set; } = null!;
  }

}