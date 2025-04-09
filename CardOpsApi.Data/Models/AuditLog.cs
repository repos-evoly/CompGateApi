using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CardOpsApi.Data.Models
{
    [Table("AuditLogs")]
    public class AuditLog : Auditable
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [MaxLength(200)]
        public string Action { get; set; } = string.Empty;

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    }
}
