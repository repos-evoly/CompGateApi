using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlockingApi.Data.Models
{
    [Table("BlockRecords")]
    public class BlockRecord : Auditable
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Required]
        public int ReasonId { get; set; }
        public Reason Reason { get; set; } = null!;

        [Required]
        public int SourceId { get; set; }
        public Source Source { get; set; } = null!;

        [MaxLength(250)]
        public string? DecisionFromPublicProsecution { get; set; } // قرارات واردة من النيابة العامة

        [MaxLength(250)]
        public string? DecisionFromCentralBankGovernor { get; set; } // قرارات واردة من محافظ مصرف ليبيا المركزي

        [MaxLength(250)]
        public string? DecisionFromFIU { get; set; } // قرارات واردة من وحدة المعلومات المالية الليبية

        [MaxLength(250)]
        public string? OtherDecision { get; set; } // قرارات أخري تذكر


        public DateTimeOffset BlockDate { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset? ScheduledUnblockDate { get; set; }

        public DateTimeOffset? ActualUnblockDate { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int BlockedByUserId { get; set; }
        public User BlockedBy { get; set; } = null!;

        public int? UnblockedByUserId { get; set; }
        public User? UnblockedBy { get; set; }
    }
}
