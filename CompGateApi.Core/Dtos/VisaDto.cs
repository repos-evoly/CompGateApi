using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Core.Dtos
{
    public class VisaDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }
        public decimal Price { get; set; }

        public List<AttachmentDto> Attachments { get; set; } = new();
    }

    public class VisaCreateDto
    {
        [Required, MaxLength(150)]
        public string NameAr { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? DescriptionAr { get; set; }

        [MaxLength(500)]
        public string? DescriptionEn { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
    }

    public class VisaUpdateDto
    {
        [Required, MaxLength(150)]
        public string NameAr { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string NameEn { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? DescriptionAr { get; set; }

        [MaxLength(500)]
        public string? DescriptionEn { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
    }
}
