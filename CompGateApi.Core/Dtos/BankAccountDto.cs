using System;

namespace CompGateApi.Core.Dtos
{
    public class BankAccountDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CurrencyId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class BankAccountCreateDto
    {
        public int UserId { get; set; }
        public int CurrencyId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    public class BankAccountUpdateDto
    {
        public int CurrencyId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

      public class PagedResult<T>
    {
        public IEnumerable<T> Data { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
    }
}
