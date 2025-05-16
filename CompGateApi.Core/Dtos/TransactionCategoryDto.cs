namespace CompGateApi.Core.Dtos
{
    public class TransactionCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class TransactionCategoryCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class TransactionCategoryUpdateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    
}
