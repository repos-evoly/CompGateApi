// CompGateApi.Core.Dtos/TransactionCategoryDtos.cs
namespace CompGateApi.Core.Dtos
{

    // per-package extension: toggle + fees/limits
    public class TransactionCategoryByPackageDto : TransactionCategoryDto
    {
        public bool? IsEnabledForPackage { get; set; }
        public decimal? B2BTransactionLimit { get; set; }
        public decimal? B2CTransactionLimit { get; set; }
        public decimal? B2BFixedFee { get; set; }
        public decimal? B2CFixedFee { get; set; }
        public decimal? B2BMinPercentage { get; set; }
        public decimal? B2CMinPercentage { get; set; }
        public decimal? B2BCommissionPct { get; set; }
        public decimal? B2CCommissionPct { get; set; }
    }

    // only Name for create/update
    public class TransactionCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public bool HasLimits { get; set; }          // ← new

    }

    // for create/update, with optional package link
    public class TransactionCategoryCreateDto
    {
        public string Name { get; set; } = null!;
        public bool HasLimits { get; set; }          // ← new

        // if provided, link this new category to a package
        public int? ServicePackageId { get; set; }
        public bool? IsEnabledForPackage { get; set; }

        // optional details for the package-link
        public decimal? B2BTransactionLimit { get; set; }
        public decimal? B2CTransactionLimit { get; set; }
        public decimal? B2BFixedFee { get; set; }
        public decimal? B2CFixedFee { get; set; }
        public decimal? B2BMinPercentage { get; set; }
        public decimal? B2CMinPercentage { get; set; }
        public decimal? B2BCommissionPct { get; set; }
        public decimal? B2CCommissionPct { get; set; }
    }

    public class TransactionCategoryUpdateDto : TransactionCategoryCreateDto { }
}


