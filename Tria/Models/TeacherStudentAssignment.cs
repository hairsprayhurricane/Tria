namespace Tria.Models;

public class TeacherStudentAssignment
{
    public int Id { get; set; }
    public string TeacherId { get; set; } = "";
    public string StudentId { get; set; } = "";
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
