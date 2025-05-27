// CompGateApi.Core.Dtos/RoleDto.cs
namespace CompGateApi.Core.Dtos
{
    public class RoleDto
    {
        public int Id { get; set; }
        public string NameLT { get; set; } = string.Empty;
        public string NameAR { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // ← NEW:
        public bool IsGlobal { get; set; }
    }

    public class PermissionDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public bool IsGlobal { get; set; }
    }

}