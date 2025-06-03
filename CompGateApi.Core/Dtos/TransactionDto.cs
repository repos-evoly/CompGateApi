using System;

namespace CompGateApi.Core.Dtos
{

   

    public class ExternalTransactionDto
    {
        public string? PostingDate { get; set; }
        public List<string> Narratives { get; set; } = new();
        public decimal Amount { get; set; }
        public string? DrCr { get; set; }
    }
}
