// CompGateApi.Core.Dtos/CheckRequestDtos.cs
using System;
using System.Collections.Generic;

namespace CompGateApi.Core.Dtos
{
    public class CheckRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public int? RepresentativeId { get; set; }
        public string? Branch { get; set; }
        public string? BranchNum { get; set; }
        public DateTime? Date { get; set; }
        public string? CustomerName { get; set; }
        public string? CardNum { get; set; }
        public string? AccountNum { get; set; }
        public string? Beneficiary { get; set; }
        public string Status { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public List<CheckRequestLineItemDto> LineItems { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CheckRequestLineItemDto
    {
        public int Id { get; set; }
        public string? Dirham { get; set; }
        public string? Lyd { get; set; }
    }

    public class CheckRequestCreateDto
    {
        public string? Branch { get; set; }

        public int? RepresentativeId { get; set; }
        public string? BranchNum { get; set; }
        public DateTime? Date { get; set; }
        public string? CustomerName { get; set; }
        public string? CardNum { get; set; }
        public string? AccountNum { get; set; }
        public string? Beneficiary { get; set; }
        public List<CheckRequestLineItemCreateDto> LineItems { get; set; } = new();
    }

    public class CheckRequestLineItemCreateDto
    {
        public string? Dirham { get; set; }
        public string? Lyd { get; set; }
    }

    public class CheckRequestUpdateDto : CheckRequestCreateDto
    {
        // you can override or add fields here if needed
    }

    public class CheckRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
    }

}
