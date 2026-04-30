using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tria.Data;
using Tria.Options;
using Tria.Services;
using Tria.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 1;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequiredUniqueChars = 0;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.AccessDeniedPath = "/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Localization — cookie-first, reads ui.{lang}.xml from Content/
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "ru", "en" };
    options.SetDefaultCulture("ru");
    options.AddSupportedCultures(cultures);
    options.AddSupportedUICultures(cultures);
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// UI strings service (reads Content/ui.en.xml, Content/ui.ru.xml)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUiLocalizer, XmlUiLocalizer>();

// Learning service is scoped so XML cache lives per-request (avoids stale lang data)
builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddScoped<IProgressService, ProgressService>();

// Ollama AI grading
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.Section));
builder.Services.AddSingleton<IOllamaGradingService>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var http = new HttpClient
    {
        BaseAddress = new Uri(opts.BaseUrl),
        Timeout     = TimeSpan.FromMinutes(5)
    };
    return new OllamaGradingService(http,
        sp.GetRequiredService<IOptions<OllamaOptions>>(),
        sp.GetRequiredService<ILogger<OllamaGradingService>>());
});
builder.Services.AddHostedService<AiGradingBackgroundService>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    const string adminEmail = "admin@tria.com";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        await userManager.CreateAsync(admin, "q");
        await userManager.AddToRoleAsync(admin, "Admin");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Static files with Brotli support for Unity WebGL builds
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".br"]   = "application/octet-stream";
provider.Mappings[".wasm"] = "application/wasm";
provider.Mappings[".data"] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;

        if (path.EndsWith(".framework.js.br"))
            ctx.Context.Response.Headers["Content-Type"] = "application/javascript";
        else if (path.EndsWith(".loader.js"))
            ctx.Context.Response.Headers["Content-Type"] = "application/javascript";
        else if (path.EndsWith(".wasm.br"))
            ctx.Context.Response.Headers["Content-Type"] = "application/wasm";
        else if (path.EndsWith(".data.br"))
            ctx.Context.Response.Headers["Content-Type"] = "application/octet-stream";

        if (path.EndsWith(".br"))
            ctx.Context.Response.Headers["Content-Encoding"] = "br";
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Language switch — GET, sets culture cookie and redirects back
app.MapGet("/set-language", (HttpContext ctx, string culture, string returnUrl = "/") =>
{
    if (!new[] { "ru", "en" }.Contains(culture)) culture = "en";

    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

    return Results.LocalRedirect(returnUrl);
});

app.MapRazorPages();

app.MapPost("/Logout", async (HttpContext ctx, SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/Login");
});

app.Run();