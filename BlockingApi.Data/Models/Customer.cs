using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlockingApi.Data.Models
{
    [Table("Customers")]
    [Index(nameof(CID), IsUnique = true, Name = "Unique_CID")]
    public class Customer : Auditable
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string CID { get; set; } = string.Empty;

        [MaxLength(150)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? NationalId { get; set; }

        [MaxLength(15)]
        public string? Phone { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? Address { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        [MaxLength(10)]
        public string? StatusCode { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<BlockRecord> BlockRecords { get; set; } = new List<BlockRecord>();
    }
}
