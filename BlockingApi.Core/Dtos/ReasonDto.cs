using System.ComponentModel.DataAnnotations;

namespace BlockingApi.Core.Dtos
{
    public class ReasonDto
    {
        public int Id { get; set; }
        public string? NameLT { get; set; } // ✅ Latin Name
        public string? NameAR { get; set; } // ✅ Arabic Name
    }

    public class EditReasonDto
    {
        [Required]
        [MaxLength(250)]
        public string? NameLT { get; set; } // ✅ Latin Name

        [Required]
        [MaxLength(250)]
        public string? NameAR { get; set; } // ✅ Arabic Name
    }
}
