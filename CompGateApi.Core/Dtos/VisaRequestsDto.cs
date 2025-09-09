using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Core.Dtos
{
    public class VisaRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CompanyId { get; set; }

        public int VisaId { get; set; }
        public int Quantity { get; set; }

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

        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }

        public List<AttachmentDto> Attachments { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    // This is serialized into the `Dto` field of multipart/form-data
    public class VisaRequestCreateDto
    {
        [Required]
        public int VisaId { get; set; }

        public int Quantity { get; set; } = 1;

        public string? Branch { get; set; }
        public DateTime? Date { get; set; }

        public string? AccountHolderName { get; set; }

        // REQUIRED for debiting
        [Required]
        public string AccountNumber { get; set; } = string.Empty;

        public long? NationalId { get; set; }
        public string? PhoneNumberLinkedToNationalId { get; set; }

        public string? Cbl { get; set; }
        public string? CardMovementApproval { get; set; }
        public string? CardUsingAcknowledgment { get; set; }

        public decimal? ForeignAmount { get; set; }
        public decimal? LocalAmount { get; set; }
        public string? Pldedge { get; set; }
    }

    public class VisaRequestStatusUpdateDto
    {
        [Required]
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }
    }
}
