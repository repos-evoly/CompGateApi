using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("BankAccounts")]
    public class BankAccount : Auditable
    {
        [Key]
        public int Id { get; set; }

        // FK to User
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // FK to Currency
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        [MaxLength(30)]
        public string AccountNumber { get; set; } = string.Empty;

        // Current balance or ledger
        // Adjust precision to match your needs or your DB server's capabilities
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }
    }
}
