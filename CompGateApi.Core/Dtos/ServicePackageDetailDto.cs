// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Core.Dtos/ServicePackageDetailDtos.cs
// ─────────────────────────────────────────────────────────────────────────────
namespace CompGateApi.Core.Dtos
{
    public class ServicePackageDetailDto
    {
        public int Id { get; set; }
        public int ServicePackageId { get; set; }
        public int TransactionCategoryId { get; set; }
        public decimal CommissionPct { get; set; }
        public decimal FeeFixed { get; set; }
    }

    public class ServicePackageDetailCreateDto
    {
        public int ServicePackageId { get; set; }
        public int TransactionCategoryId { get; set; }
        public decimal CommissionPct { get; set; }
        public decimal FeeFixed { get; set; }
    }

    public class ServicePackageDetailUpdateDto
    {
        public decimal CommissionPct { get; set; }
        public decimal FeeFixed { get; set; }
    }
}
