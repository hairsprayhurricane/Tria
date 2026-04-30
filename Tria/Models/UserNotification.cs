namespace Tria.Models;

public class UserNotification
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? LinkUrl { get; set; }
}
