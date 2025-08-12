// CompGateApi.Core.Dtos/ServicePackageDetailDtos.cs
namespace CompGateApi.Core.Dtos
{
    public class ServicePackageCategoryDto
    {
        public int TransactionCategoryId { get; set; }
        public string TransactionCategoryName { get; set; } = null!;
        public bool IsEnabledForPackage { get; set; }

        public decimal B2BTransactionLimit { get; set; }
        public decimal B2CTransactionLimit { get; set; }
        public decimal B2BFixedFee { get; set; }
        public decimal B2CFixedFee { get; set; }
        public decimal B2BMinPercentage { get; set; }
        public decimal B2CMinPercentage { get; set; }
        public decimal B2BMaxAmount { get; set; }
        public decimal B2CMaxAmount { get; set; }

        public decimal B2BCommissionPct { get; set; }
        public decimal B2CCommissionPct { get; set; }
    }

    public class ServicePackageCategoryUpdateDto
    {
        public bool IsEnabledForPackage { get; set; }
        public decimal B2BTransactionLimit { get; set; }
        public decimal B2CTransactionLimit { get; set; }
        public decimal B2BFixedFee { get; set; }
        public decimal B2CFixedFee { get; set; }
        public decimal B2BMinPercentage { get; set; }
        public decimal B2CMinPercentage { get; set; }
        public decimal B2BCommissionPct { get; set; }
        public decimal B2CCommissionPct { get; set; }

        public decimal B2BMaxAmount { get; set; }
        public decimal B2CMaxAmount { get; set; }
    }
}
