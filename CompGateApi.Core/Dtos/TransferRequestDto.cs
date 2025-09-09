using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CompGateApi.Core.Dtos
{

    // What we send back for each transfer
    public class TransferRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        // ← these three must be declared so your .ForMember(…) calls compile:
        public string CategoryName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;

        public string FromAccount { get; set; } = string.Empty;
        public string ToAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int EconomicSectorId { get; set; }
        public string EconomicSectorName { get; set; } = string.Empty;

        // ← and these two for your commission mappings:
        public decimal CommissionAmount { get; set; }
        public bool CommissionOnRecipient { get; set; }

        public string Status { get; set; } = string.Empty;
        public int? ServicePackageId { get; set; }
        public string? Description { get; set; }

        public decimal Rate { get; set; }
        public string TransferMode { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
    }

    // What the client posts to create a transfer
    public class TransferRequestCreateDto
    {
        public int TransactionCategoryId { get; set; }
        public string FromAccount { get; set; } = null!;
        public string ToAccount { get; set; } = null!;
        public decimal Amount { get; set; }

        [Required]
        public int CurrencyId { get; set; }
        public int EconomicSectorId { get; set; }


        public decimal Rate { get; set; }
        public string TransferMode { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string? BankReference { get; set; }
    }

    // What the client posts to update only the status
    public class TransferRequestStatusUpdateDto
    {
        public string Status { get; set; } = null!;
    }

    // For /api/transfers/accounts
    public class AccountDto
    {
        public string AccountString { get; set; } = null!;
        public decimal AvailableBalance { get; set; }
        public decimal DebitBalance { get; set; }
        public string? TransferType { get; set; } // (values: "B2B", "B2C", or null)

    }

    // For /api/transfers/statement
    public class StatementEntryDto
    {
        public DateTime PostingDate { get; set; }
        public string DrCr { get; set; } = null!;
        public decimal Amount { get; set; }
        public List<string> Narratives { get; set; } = new();
    }

    public class BankResponseDto
    {
        public BankHeaderDto? Header { get; set; }
    }

    public class BankHeaderDto
    {
        public string? ReturnCode { get; set; }
        public string? ReturnMessage { get; set; }
        public string? ReturnMessageCode { get; set; }
    }
}
