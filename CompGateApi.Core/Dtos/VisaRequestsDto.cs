// ── CompGateApi.Core.Dtos/VisaRequestDtos.cs ────────────────────────────
using System;

namespace CompGateApi.Core.Dtos
{
    public class VisaRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Branch { get; set; }
        public DateTime? Date { get; set; }
        public string? AccountHolderName { get; set; }
        public string? AccountNumber { get; set; }
        public long? NationalId { get; set; }
        public string? PhoneNumberLinkedToNationalId { get; set; }
        public string? Cbl { get; set; }
        public string? CardMovementApproval { get; set; }
        public string? CardUsingAcknowledgment { get; set; }
        public decimal? ForeignAmount { get; set; }
        public decimal? LocalAmount { get; set; }
        public string? Pldedge { get; set; }
        public string Status { get; set; } = string.Empty;

        public string? Reason { get; set; } = string.Empty; // Optional reason for status change

        public Guid? AttachmentId { get; set; }
        public List<AttachmentDto>? Attachments { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class VisaRequestCreateDto
    {
        public string? Branch { get; set; }
        public DateTime? Date { get; set; }
        public string? AccountHolderName { get; set; }
        public string? AccountNumber { get; set; }
        public long? NationalId { get; set; }
        public string? PhoneNumberLinkedToNationalId { get; set; }
        public string? Cbl { get; set; }
        public string? CardMovementApproval { get; set; }
        public string? CardUsingAcknowledgment { get; set; }
        public decimal? ForeignAmount { get; set; }
        public decimal? LocalAmount { get; set; }
        public string? Pldedge { get; set; }
        public Guid? AttachmentId { get; set; }
        public List<AttachmentDto>? Attachments { get; set; }
    }

    public class VisaRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
    }
}
