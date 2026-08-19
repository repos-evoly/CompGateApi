using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Models;

[Table("Notifications")]
[Index(nameof(ToAuthUserId), nameof(CreatedAt))]
[Index(nameof(ToAuthUserId), nameof(ReadAt))]
[Index(nameof(EventId), IsUnique = true)]
public sealed class Notification : Auditable
{
    [Key]
    public int Id { get; set; }

    public Guid EventId { get; set; }

    public int? FromUserId { get; set; }
    public User? FromUser { get; set; }

    public int ToUserId { get; set; }
    public User ToUser { get; set; } = null!;

    public int ToAuthUserId { get; set; }
    public int? CompanyId { get; set; }

    [Required, MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string EntityType { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string EntityId { get; set; } = string.Empty;

    // Retained for the existing web DTO. Values are generated internally only.
    [MaxLength(256)]
    public string Link { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public NotificationOutbox? Outbox { get; set; }
}
