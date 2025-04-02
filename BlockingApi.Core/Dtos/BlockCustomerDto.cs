using System;
using System.ComponentModel.DataAnnotations;

namespace BlockingApi.Core.Dtos
{
    public class BlockCustomerDto
    {
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        public int ReasonId { get; set; }

        [Required]
        public int SourceId { get; set; }

        public string? DecisionFromPublicProsecution { get; set; }
        public string? DecisionFromCentralBankGovernor { get; set; }
        public string? DecisionFromFIU { get; set; }
        public string? OtherDecision { get; set; }


        [Required]
        public int BlockedByUserId { get; set; }

        public DateTimeOffset? ToBlockDate { get; set; }
    }
}
