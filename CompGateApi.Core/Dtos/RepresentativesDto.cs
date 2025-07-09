// CompGateApi.Core.Dtos/RepresentativeDto.cs
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompGateApi.Core.Dtos
{
    // Core/Dtos/RepresentativeCreateDto.cs
    public class RepresentativeCreateDto
    {
        [FromForm] public string Name { get; set; } = null!;
        [FromForm] public string Number { get; set; } = null!;
        [FromForm] public string PassportNumber { get; set; } = null!;
        [FromForm(Name = "photo")] public IFormFile Photo { get; set; } = null!;
    }

    // Core/Dtos/RepresentativeUpdateDto.cs
    public class RepresentativeUpdateDto
    {
        [FromForm] public string Name { get; set; } = null!;
        [FromForm] public string Number { get; set; } = null!;
        [FromForm] public string PassportNumber { get; set; } = null!;
        [FromForm] public bool IsActive { get; set; }
        [FromForm(Name = "photo")] public IFormFile? Photo { get; set; }
    }

    // Core/Dtos/RepresentativeDto.cs
    public class RepresentativeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Number { get; set; } = null!;
        public string PassportNumber { get; set; } = null!;
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

}
