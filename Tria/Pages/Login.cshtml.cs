using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Tria.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public LoginModel(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = "";

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = "";

            public bool RememberMe { get; set; } = false;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            await HttpContext.SignOutAsync("Guest");

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return LocalRedirect(returnUrl);

            ModelState.AddModelError(string.Empty, "Неверный email или пароль.");
            return Page();
        }

        public async Task<IActionResult> OnPostGuestAsync()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "guest:" + Guid.NewGuid().ToString("N")),
                new Claim(ClaimTypes.Name, "Guest"),
                new Claim("IsGuest", "true")
            };

            var identity = new ClaimsIdentity(claims, "Guest");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                "Guest",
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                });

            return RedirectToPage("/Index");
        }
    }
}
