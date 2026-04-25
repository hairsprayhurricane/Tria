namespace Tria.Models;

public class UserCourseAssignment
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int CourseId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
