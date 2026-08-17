using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Models;

[Table("NotificationOutbox")]
[Index(nameof(IdempotencyKey), IsUnique = true)]
[Index(nameof(CompletedAt), nameof(DeadLetteredAt), nameof(AvailableAt), nameof(LeaseExpiresAt))]
public sealed class NotificationOutbox
{
    [Key]
    public Guid EventId { get; set; }

    [Required, MaxLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public int NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    public int RecipientAuthUserId { get; set; }

    [Required, MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    public int EventVersion { get; set; } = 1;

    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }

    [MaxLength(100)]
    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }

    [MaxLength(100)]
    public string? LastFailureCategory { get; set; }
}
