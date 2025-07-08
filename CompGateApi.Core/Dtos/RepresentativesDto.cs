// CompGateApi.Core.Dtos/RepresentativeDto.cs
using System;

namespace CompGateApi.Core.Dtos
{
    public class RepresentativeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string PassportNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class RepresentativeCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string PassportNumber { get; set; } = string.Empty;
    }

    public class RepresentativeUpdateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string PassportNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; } = false; // Default to true
    }
}
