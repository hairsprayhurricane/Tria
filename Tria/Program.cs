using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tria.Data;
using Tria.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddXmlFile("Resources/Content/course.ru.xml", optional: true, reloadOnChange: true)
    .AddXmlFile("Resources/Content/course.en.xml", optional: true, reloadOnChange: true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
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

// Guest cookie scheme (24h)
builder.Services.AddAuthentication()
    .AddCookie("Guest", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = false;
        options.Cookie.Name = ".Tria.Guest";
    });

// Localization — cookie-first, reads ui.{lang}.xml from Content/
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "ru", "en" };
    options.SetDefaultCulture("en");
    options.AddSupportedCultures(cultures);
    options.AddSupportedUICultures(cultures);
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// UI strings service (reads Content/ui.en.xml, Content/ui.ru.xml)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUiLocalizer, XmlUiLocalizer>();

builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
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
app.Run();