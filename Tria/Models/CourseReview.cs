namespace Tria.Models;

public class CourseReview
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int CourseId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
}
