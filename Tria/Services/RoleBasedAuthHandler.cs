using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Tria.Services;

public class RoleBasedAuthHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext ctx,
        AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Forbidden && ctx.User.Identity?.IsAuthenticated == true)
        {
            if (ctx.User.IsInRole("Admin"))
                ctx.Response.Redirect("/Admin");
            else if (ctx.User.IsInRole("Teacher"))
                ctx.Response.Redirect("/Teacher");
            else
                ctx.Response.Redirect("/Login");
            return;
        }

        await _default.HandleAsync(next, ctx, policy, result);
    }
}
