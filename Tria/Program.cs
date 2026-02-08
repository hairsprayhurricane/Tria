using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tria.Data;
using Tria.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddXmlFile("Content/course.ru.xml", optional: true, reloadOnChange: true)
    .AddXmlFile("Content/course.en.xml", optional: true, reloadOnChange: true);

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

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "ru", "en" };
    options.SetDefaultCulture("en");
    options.AddSupportedCultures(cultures);
    options.AddSupportedUICultures(cultures);
});

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

app.MapRazorPages();
app.Run();
