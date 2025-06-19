namespace CompGateApi.Core.Dtos
{
    public class EconomicSectorDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class EconomicSectorCreateDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class EconomicSectorUpdateDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
