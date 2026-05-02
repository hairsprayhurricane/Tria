using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tria.Pages.Messenger;

[Authorize(Roles = "Teacher,Student")]
public class IndexModel : PageModel
{
    public string UserRole { get; set; } = "";
    public string UserEmail { get; set; } = "";

    public void OnGet()
    {
        UserEmail = User.Identity?.Name ?? "";
        UserRole = User.IsInRole("Teacher") ? "Teacher" : "Student";
    }
}
