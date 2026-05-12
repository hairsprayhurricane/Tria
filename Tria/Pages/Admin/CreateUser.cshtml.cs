using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Tria.Pages.Admin;

[Authorize(Roles = "Admin")]
public class CreateUserModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    public CreateUserModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Введите email")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Введите пароль")]
        public string Password { get; set; } = "";

        public string Role { get; set; } = "Student";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var existing = await _userManager.FindByEmailAsync(Input.Email);
        if (existing != null)
        {
            ErrorMessage = "Пользователь с таким email уже существует.";
            return Page();
        }

        var user = new IdentityUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        var validRoles = new[] { "Admin", "Teacher", "Student", "Expert" };
        var role = validRoles.Contains(Input.Role) ? Input.Role : "Student";
        await _userManager.AddToRoleAsync(user, role);

        return RedirectToPage("/Admin/Index");
    }
}
