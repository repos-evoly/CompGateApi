// for GET /api/servicepackages
public class ServicePackageListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }

    public IReadOnlyList<ServicePackageCategoryDto> Categories { get; set; }
        = Array.Empty<ServicePackageCategoryDto>();
}

// for GET /api/servicepackages/{id}
public class ServicePackageDetailsDto : ServicePackageListDto { }

// each category + its per-package settings
public class ServicePackageCategoryDto
{
    public int ServicePackageId { get; set; }
    public string ServicePackageName { get; set; } = null!;

    public int TransactionCategoryId { get; set; }
    public string TransactionCategoryName { get; set; } = null!;

    public bool TransactionCategoryHasLimits { get; set; }          // ← new

    public bool IsEnabledForPackage { get; set; }

    // ← make all of these nullable ↓
    public decimal? B2BTransactionLimit { get; set; }
    public decimal? B2CTransactionLimit { get; set; }
    public decimal? B2BFixedFee { get; set; }
    public decimal? B2CFixedFee { get; set; }
    public decimal? B2BMinPercentage { get; set; }
    public decimal? B2CMinPercentage { get; set; }

    public decimal? B2BMaxAmount { get; set; }
    public decimal? B2CMaxAmount { get; set; }

    public decimal? B2BCommissionPct { get; set; }
    public decimal? B2CCommissionPct { get; set; }
}

// for POST
public class ServicePackageCreateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
}

// for PUT
public class ServicePackageUpdateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
}
