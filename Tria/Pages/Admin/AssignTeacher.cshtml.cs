using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Models;

namespace Tria.Pages.Admin;

[Authorize(Roles = "Admin")]
public class AssignTeacherModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;

    public List<UserOption> Teachers { get; set; } = new();
    public List<UserOption> Students { get; set; } = new();
    public List<AssignmentRow> Assignments { get; set; } = new();
    public string? SuccessMessage { get; set; }

    public record UserOption(string Id, string Email);
    public record AssignmentRow(int Id, string TeacherId, string StudentId, string TeacherEmail, string StudentEmail);

    public AssignTeacherModel(UserManager<IdentityUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task OnGetAsync() => await LoadDataAsync();

    public async Task<IActionResult> OnPostAssignAsync(string teacherId, string studentId)
    {
        if (!string.IsNullOrEmpty(teacherId) && !string.IsNullOrEmpty(studentId))
        {
            var exists = await _db.TeacherStudentAssignments
                .AnyAsync(a => a.TeacherId == teacherId && a.StudentId == studentId);
            if (!exists)
            {
                _db.TeacherStudentAssignments.Add(new TeacherStudentAssignment
                {
                    TeacherId = teacherId,
                    StudentId = studentId,
                    AssignedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                SuccessMessage = "Ученик назначен преподавателю.";
            }
        }
        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int assignmentId)
    {
        var a = await _db.TeacherStudentAssignments.FindAsync(assignmentId);
        if (a != null)
        {
            _db.TeacherStudentAssignments.Remove(a);
            await _db.SaveChangesAsync();
        }
        await LoadDataAsync();
        return Page();
    }

    private async Task LoadDataAsync()
    {
        var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
        var students = await _userManager.GetUsersInRoleAsync("Student");

        var teacherDict = teachers.ToDictionary(u => u.Id, u => u.Email ?? u.UserName ?? "—");
        var studentDict = students.ToDictionary(u => u.Id, u => u.Email ?? u.UserName ?? "—");

        Teachers = teachers.Select(u => new UserOption(u.Id, u.Email ?? u.UserName ?? "—")).OrderBy(u => u.Email).ToList();
        Students = students.Select(u => new UserOption(u.Id, u.Email ?? u.UserName ?? "—")).OrderBy(u => u.Email).ToList();

        var assignments = await _db.TeacherStudentAssignments.ToListAsync();
        Assignments = assignments.Select(a => new AssignmentRow(
            a.Id,
            a.TeacherId,
            a.StudentId,
            teacherDict.GetValueOrDefault(a.TeacherId, "—"),
            studentDict.GetValueOrDefault(a.StudentId, "—")
        )).OrderBy(a => a.TeacherEmail).ThenBy(a => a.StudentEmail).ToList();
    }
}
