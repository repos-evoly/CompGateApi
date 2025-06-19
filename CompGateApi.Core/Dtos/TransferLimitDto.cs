// CompGateApi.Core.Dtos/TransferLimitDtos.cs
using System;
namespace CompGateApi.Core.Dtos
{
    // Returned to clients
    public class TransferLimitDto
    {
        public int Id { get; set; }
        public int ServicePackageId { get; set; }
        public int TransactionCategoryId { get; set; }
        public int CurrencyId { get; set; }
        public string Period { get; set; } = string.Empty;
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }

        public string TransactionCategoryName { get; set; } = string.Empty; // new
        public string CurrencyCode { get; set; } = string.Empty; // new
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    // For POST
    public class TransferLimitCreateDto
    {
        public int ServicePackageId { get; set; }
        public int TransactionCategoryId { get; set; }
        public int CurrencyId { get; set; }
        public string Period { get; set; } = string.Empty;   // "Daily", "Weekly", "Monthly"
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
    }

    // For PUT
    public class TransferLimitUpdateDto
    {
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
    }
}
