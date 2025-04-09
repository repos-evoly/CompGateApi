namespace CardOpsApi.Core.Dtos
{
    public class ReasonCreateDto
    {
        public string NameLT { get; set; } = string.Empty;
        public string NameAR { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class ReasonUpdateDto
    {
        public string NameLT { get; set; } = string.Empty;
        public string NameAR { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class ReasonDto
    {
        public int Id { get; set; }
        public string NameLT { get; set; } = string.Empty;
        public string NameAR { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
