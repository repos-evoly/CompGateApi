// CertifiedBankStatementRequestDtos.cs
using System;

namespace CompGateApi.Core.Dtos
{
    public class ServicesRequestDto
    {
        public bool ReactivateIdfaali { get; set; }
        public bool DeactivateIdfaali { get; set; }
        public bool ResetDigitalBankPassword { get; set; }
        public bool ResendMobileBankingPin { get; set; }
        public bool ChangePhoneNumber { get; set; }
    }

    public class StatementRequestDto
    {
        public bool? CurrentAccountStatementArabic { get; set; }
        public bool? CurrentAccountStatementEnglish { get; set; }
        public bool? VisaAccountStatement { get; set; }
        public bool? AccountStatement { get; set; }
        public bool? JournalMovement { get; set; }
        public bool? NonFinancialCommitment { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class CertifiedBankStatementRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string AccountHolderName { get; set; } = null!;
        public string AuthorizedOnTheAccountName { get; set; } = null!;
        public long AccountNumber { get; set; }
        public long? OldAccountNumber { get; set; }
        public long? NewAccountNumber { get; set; }
        public decimal TotalAmountLyd { get; set; }

        public ServicesRequestDto? ServiceRequests { get; set; }
        public StatementRequestDto? StatementRequest { get; set; }

        public string Status { get; set; } = null!;
        public string? Reason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int CompanyId { get; set; }
    }

    public class CertifiedBankStatementRequestCreateDto
    {
        public string AccountHolderName { get; set; } = null!;
        public string AuthorizedOnTheAccountName { get; set; } = null!;
        public long AccountNumber { get; set; }
        public long? OldAccountNumber { get; set; }
        public long? NewAccountNumber { get; set; }
        public decimal TotalAmountLyd { get; set; }

        public ServicesRequestDto? ServiceRequests { get; set; }
        public StatementRequestDto? StatementRequest { get; set; }
    }

    public class CertifiedBankStatementRequestStatusUpdateDto
    {
        public string Status { get; set; } = null!;
        public string Reason { get; set; } = null!;
    }
}
