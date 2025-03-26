public class NotificationDto
{
    public int FromUserId { get; set; }
    public string FromUserName { get; set; } = string.Empty;

    public int ToUserId { get; set; }
    public string ToUserName { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string Link { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
