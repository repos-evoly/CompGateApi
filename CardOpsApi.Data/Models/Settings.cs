using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("Settings")]
    public class Settings : Auditable
    {
        [Key]
        public int Id { get; set; }

        public int TransactionAmount { get; set; }

        public int TransactionAmountForeign { get; set; }

        public string TransactionTimeTo { get; set; } = string.Empty;

        public string TimeToIdle { get; set; } = string.Empty;

    }
}
