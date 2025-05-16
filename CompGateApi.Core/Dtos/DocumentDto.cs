using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Core.Dtos
{
    public class DocumentDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string DocumentType { get; set; } = "reports"; // ✅ "reports" or "cbl_bank_documents"

        [Required]
        public int UploadedByUserId { get; set; }
    }

    public class DocumentResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FileMimeType { get; set; } = string.Empty;
        public int FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTimeOffset UploadedAt { get; set; }
        public int UploadedByUserId { get; set; }
        public string UploadedBy { get; set; } = string.Empty; // ✅ Store uploader's name
    }
}
