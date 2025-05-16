using System;

namespace CompGateApi.Core.Dtos
{
    public class CurrencyDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CurrencyCreateDto
    {
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class CurrencyUpdateDto
    {
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
