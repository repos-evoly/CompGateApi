using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("Settings")]
    public class Settings : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string CommissionAccount { get; set; } = string.Empty;

        public string CommissionAccountUSD { get; set; } = string.Empty;

        public decimal GlobalLimit { get; set; }

        public string? EvoWallet { get; set; } = null!;


    }
}
