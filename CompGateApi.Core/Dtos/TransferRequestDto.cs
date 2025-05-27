
// CompGateApi.Core.Dtos/TransferRequestDto.cs
namespace CompGateApi.Core.Dtos
{
    public class TransferRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string FromAccount { get; set; } = string.Empty;
        public string ToAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime RequestedAt { get; set; }
    }
}

// CompGateApi.Core.Dtos/TransferRequestCreateDto.cs
namespace CompGateApi.Core.Dtos
{
    public class TransferRequestCreateDto
    {
        public int TransactionCategoryId { get; set; }
        public string FromAccount { get; set; } = string.Empty;
        public string ToAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string? Description { get; set; }

    }
}

// CompGateApi.Core.Dtos/TransferRequestStatusUpdateDto.cs
namespace CompGateApi.Core.Dtos
{
    public class TransferRequestStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
    }
}

// CompGateApi.Core.Dtos/AccountDto.cs
namespace CompGateApi.Core.Dtos
{
    public class AccountDto
    {
        public string AccountString { get; set; } = string.Empty;
        public decimal AvailableBalance { get; set; }    // ← YBCD01CABL
        public decimal DebitBalance { get; set; }        // ← YBCD01LDBL
    }
}

// CompGateApi.Core.Dtos/StatementEntryDto.cs
namespace CompGateApi.Core.Dtos
{
    public class StatementEntryDto
    {
        public string PostingDate { get; set; } = string.Empty;
        public string DrCr { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public List<string> Narratives { get; set; } = new();
    }
}
