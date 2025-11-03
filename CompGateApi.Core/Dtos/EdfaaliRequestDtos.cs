using System;
using System.Collections.Generic;

namespace CompGateApi.Core.Dtos
{
    public class EdfaaliRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CompanyId { get; set; }

        public int? RepresentativeId { get; set; }
        public string? NationalId { get; set; }
        public string? IdentificationNumber { get; set; }
        public string? IdentificationType { get; set; }
        public string? CompanyEnglishName { get; set; }
        public string? WorkAddress { get; set; }
        public string? StoreAddress { get; set; }
        public string? City { get; set; }
        public string? Area { get; set; }
        public string? Street { get; set; }
        public string? MobileNumber { get; set; }
        public string? ServicePhoneNumber { get; set; }
        public string? BankAnnouncementPhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? AccountNumber { get; set; }

        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }

        public List<AttachmentDto> Attachments { get; set; } = new();

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    // Incoming body (JSON inside multipart 'Dto' field), mirrors TEdfaaliFormValues
    public class EdfaaliRequestCreateDto
    {
        public string? RepresentativeId { get; set; }
        public string? NationalId { get; set; }
        public string? IdentificationNumber { get; set; }
        public string? IdentificationType { get; set; }
        public string? CompanyEnglishName { get; set; }
        public string? WorkAddress { get; set; }
        public string? StoreAddress { get; set; }
        public string? City { get; set; }
        public string? Area { get; set; }
        public string? Street { get; set; }
        public string? MobileNumber { get; set; }
        public string? ServicePhoneNumber { get; set; }
        public string? BankAnnouncementPhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? AccountNumber { get; set; }
    }

    public class EdfaaliRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}

