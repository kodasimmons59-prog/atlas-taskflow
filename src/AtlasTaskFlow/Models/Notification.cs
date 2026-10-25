namespace AtlasTaskFlow.Models;

public class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int TeamId { get; set; }
    public int? TaskId { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A line in the dashboard's Recent Activity feed.</summary>
public class ActivityEntry
{
    public int ActivityEntryId { get; set; }
    public int TeamId { get; set; }
    public int? TaskId { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
