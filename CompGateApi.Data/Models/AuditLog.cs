using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompGateApi.Data.Models
{
    [Table("AuditLogs")]
    public class AuditLog : Auditable
    {
        [Key]
        public long Id { get; set; }

        public int? AuthUserId { get; set; }
        public int? AppUserId { get; set; }
        public int? CompanyId { get; set; }

        [MaxLength(200)]
        public string? Username { get; set; }
        [MaxLength(200)]
        public string? Role { get; set; }

        [MaxLength(10)]
        public string Method { get; set; } = string.Empty;
        [MaxLength(512)]
        public string Path { get; set; } = string.Empty;
        [MaxLength(1024)]
        public string? QueryString { get; set; }
        [MaxLength(256)]
        public string? RouteName { get; set; }

        // ── Client / Network ────────────────────────────────────────────────
        [MaxLength(64)]
        public string? Ip { get; set; }
        [MaxLength(512)]
        public string? UserAgent { get; set; }

        // ── Result ──────────────────────────────────────────────────────────
        public int StatusCode { get; set; }
        public long DurationMs { get; set; }

        // ── Bodies (trimmed for size) ───────────────────────────────────────
        public string? RequestBody { get; set; }
        public string? ResponseBody { get; set; }

        // ── Extra (free-form JSON) ──────────────────────────────────────────
        public string? ExtrasJson { get; set; }

        // ── When ────────────────────────────────────────────────────────────
    }
}
