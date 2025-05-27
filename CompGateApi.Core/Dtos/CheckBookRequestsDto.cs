// CompGateApi.Core.Dtos/CheckBookRequestDtos.cs
using System;
using System.Collections.Generic;

namespace CompGateApi.Core.Dtos
{
    public class CheckBookRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? AccountNumber { get; set; }
        public string? PleaseSend { get; set; }
        public string? Branch { get; set; }
        public DateTime? Date { get; set; }
        public string? BookContaining { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int? CompanyId { get; set; }
    }

    // Company calls this to create a new request; UserId is inferred from token.
    public class CheckBookRequestCreateDto
    {
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? AccountNumber { get; set; }
        public string? PleaseSend { get; set; }
        public string? Branch { get; set; }
        public DateTime? Date { get; set; }
        public string? BookContaining { get; set; }
    }

    // Admin uses this dto to update only the Status.
    public class CheckBookRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
    }

    // Standard paged‐result wrapper

}
