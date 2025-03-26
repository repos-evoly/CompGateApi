using System.ComponentModel.DataAnnotations;

namespace BlockingApi.Core.Dtos
{
    public class SourceDto
    {
        public int Id { get; set; }
        public string? NameLT { get; set; } // ✅ Latin Name
        public string? NameAR { get; set; } // ✅ Arabic Name
    }

    public class EditSourceDto
    {
        [Required]
        [MaxLength(100)]
        public string? NameLT { get; set; } // ✅ Latin Name

        [Required]
        [MaxLength(100)]
        public string? NameAR { get; set; } // ✅ Arabic Name
    }
}
