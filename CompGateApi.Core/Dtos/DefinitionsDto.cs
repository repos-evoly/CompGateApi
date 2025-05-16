using System;

namespace CompGateApi.Core.Dtos
{
    public class DefinitionDto
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // New currency details
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class DefinitionCreateDto
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // Instead of a string property for currency, now use the foreign key.
        public int CurrencyId { get; set; }
        // Expected values: "ATM" or "POS"
        public string Type { get; set; } = string.Empty;
    }

    public class DefinitionUpdateDto
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
