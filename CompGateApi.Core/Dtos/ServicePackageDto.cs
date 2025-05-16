// ─────────────────────────────────────────────────────────────────────────────
// CompGateApi.Core.Dtos/ServicePackageDtos.cs
// ─────────────────────────────────────────────────────────────────────────────
namespace CompGateApi.Core.Dtos
{
    // Returned to client
    public class ServicePackageDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public IReadOnlyList<ServicePackageDetailDto> Details { get; set; } = Array.Empty<ServicePackageDetailDto>();
        public IReadOnlyList<TransferLimitDto> Limits { get; set; } = Array.Empty<TransferLimitDto>();
    }

    // Used to create a new package
    public class ServicePackageCreateDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

    // Used to update existing package metadata
    public class ServicePackageUpdateDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

   

    // Per-period limits for a package/category/currency
    
}
