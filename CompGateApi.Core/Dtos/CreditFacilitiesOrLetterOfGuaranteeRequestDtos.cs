// CompGateApi.Core.Dtos/CreditFacilitiesOrLetterOfGuaranteeRequestDtos.cs
using System;

namespace CompGateApi.Core.Dtos
{
    public class CreditFacilitiesOrLetterOfGuaranteeRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string AccountNumber { get; set; } = null!;
        public DateTime Date { get; set; }
        public DateTime ValidUntil { get; set; }                 // NEW
        public decimal Amount { get; set; }
        public string Purpose { get; set; } = null!;
        public string AdditionalInfo { get; set; } = null!;
        public string Curr { get; set; } = null!;
        public string ReferenceNumber { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string LetterOfGuarenteePct { get; set; } = null!; // NEW

        public string Status { get; set; } = null!;
        public string? Reason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreditFacilitiesOrLetterOfGuaranteeRequestCreateDto
    {
        public string AccountNumber { get; set; } = null!;
        public DateTime Date { get; set; }
        public DateTime ValidUntil { get; set; }                 // NEW
        public decimal Amount { get; set; }
        public string Purpose { get; set; } = null!;
        public string AdditionalInfo { get; set; } = null!;
        public string Curr { get; set; } = null!;
        public string ReferenceNumber { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string LetterOfGuarenteePct { get; set; } = null!; // NEW
    }

    public class CreditFacilitiesOrLetterOfGuaranteeRequestStatusUpdateDto
    {
        public string Status { get; set; } = null!;
        public string Reason { get; set; } = null!;
    }
}
