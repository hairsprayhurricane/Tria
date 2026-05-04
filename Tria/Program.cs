using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using System.Security.Claims;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tria.Data;
using Tria.Options;
using Tria.Services;
using Tria.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IMessengerService, MessengerService>();

var sentinelLogPath = Path.Combine(builder.Environment.ContentRootPath, "SentinelLog.txt");
builder.Services.AddSingleton(new SentinelLogger(sentinelLogPath));

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
        sp.GetRequiredService<ILogger<OllamaGradingService>>(),
        sp.GetRequiredService<SentinelLogger>());
});

builder.Services.AddSingleton<IOllamaChatService>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var http = new HttpClient
    {
        BaseAddress = new Uri(opts.BaseUrl),
        Timeout     = TimeSpan.FromMinutes(5)
    };
    return new OllamaChatService(http,
        sp.GetRequiredService<IOptions<OllamaOptions>>(),
        sp.GetRequiredService<ILogger<OllamaChatService>>());
});
builder.Services.AddHostedService<AiGradingBackgroundService>();

builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, RoleBasedAuthHandler>();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.Services.GetRequiredService<SentinelLogger>()
   .Log("==================== НОВЫЙ ЗАПУСК ====================");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Teacher", "Student" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

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

app.MapGet("/api/notifications/unread-count", async (HttpContext ctx, INotificationService notif) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Results.Unauthorized();
    var count = await notif.GetUnreadCountAsync(userId);
    return Results.Ok(new { count });
}).RequireAuthorization();

app.MapPost("/Logout", async (HttpContext ctx, SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/Login");
});

// ── Messenger API ─────────────────────────────────────────────────────────────

app.MapGet("/api/messenger/unread-count", async (HttpContext ctx, IMessengerService messenger) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Results.Unauthorized();
    var count = await messenger.GetUnreadCountAsync(userId);
    return Results.Ok(new { count });
}).RequireAuthorization();

app.MapGet("/api/messenger/conversations", async (
    HttpContext ctx,
    IMessengerService messenger,
    UserManager<IdentityUser> userManager,
    ApplicationDbContext db) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Results.Unauthorized();

    var isTeacher = ctx.User.IsInRole("Teacher");
    List<string> contactIds;

    if (isTeacher)
    {
        contactIds = await db.TeacherStudentAssignments
            .Where(a => a.TeacherId == userId)
            .Select(a => a.StudentId)
            .ToListAsync();
    }
    else
    {
        contactIds = await db.TeacherStudentAssignments
            .Where(a => a.StudentId == userId)
            .Select(a => a.TeacherId)
            .ToListAsync();
    }

    var result = new List<object>();

    foreach (var cid in contactIds)
    {
        var contact = await userManager.FindByIdAsync(cid);
        if (contact == null) continue;

        var lastMsg = await db.ChatMessages
            .Where(m =>
                (m.SenderId == userId && m.ReceiverId == cid) ||
                (m.SenderId == cid && m.ReceiverId == userId))
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();

        var unread = await db.ChatMessages
            .CountAsync(m => m.SenderId == cid && m.ReceiverId == userId && !m.IsRead);

        result.Add(new
        {
            contactId = cid,
            contactEmail = contact.Email ?? contact.UserName ?? "—",
            lastMessage = lastMsg?.Content,
            lastMessageAt = lastMsg?.SentAt,
            unreadCount = unread,
            isAi = false
        });
    }

    // AI conversation always present
    var aiLastMsg = await db.ChatMessages
        .Where(m =>
            (m.SenderId == userId && m.ReceiverId == null) ||
            (m.ReceiverId == userId && m.IsFromAi))
        .OrderByDescending(m => m.SentAt)
        .FirstOrDefaultAsync();

    var aiUnread = await db.ChatMessages
        .CountAsync(m => m.ReceiverId == userId && m.IsFromAi && !m.IsRead);

    result.Add(new
    {
        contactId = "ai",
        contactEmail = "Сентинел (ИИ)",
        lastMessage = aiLastMsg?.Content,
        lastMessageAt = aiLastMsg?.SentAt,
        unreadCount = aiUnread,
        isAi = true
    });

    result = result.OrderByDescending(c =>
    {
        var t = c.GetType().GetProperty("lastMessageAt")?.GetValue(c);
        return t as DateTime? ?? DateTime.MinValue;
    }).ToList();

    return Results.Ok(result);
}).RequireAuthorization(p => p.RequireRole("Teacher", "Student"));

app.MapGet("/api/messenger/messages/{contactId}", async (
    HttpContext ctx,
    string contactId,
    IMessengerService messenger,
    ApplicationDbContext db) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Results.Unauthorized();

    if (contactId == "ai")
    {
        var msgs = await messenger.GetAiConversationAsync(userId, 60);
        return Results.Ok(msgs.Select(m => new
        {
            id = m.Id,
            content = m.Content,
            sentAt = m.SentAt,
            isFromMe = m.SenderId == userId,
            isFromAi = m.IsFromAi
        }));
    }

    // Validate the contact is actually linked to this user
    var isTeacher = ctx.User.IsInRole("Teacher");
    bool linked = isTeacher
        ? await db.TeacherStudentAssignments.AnyAsync(a => a.TeacherId == userId && a.StudentId == contactId)
        : await db.TeacherStudentAssignments.AnyAsync(a => a.StudentId == userId && a.TeacherId == contactId);
    if (!linked) return Results.Forbid();

    var messages = await messenger.GetConversationAsync(userId, contactId, 60);
    return Results.Ok(messages.Select(m => new
    {
        id = m.Id,
        content = m.Content,
        sentAt = m.SentAt,
        isFromMe = m.SenderId == userId,
        isFromAi = false
    }));
}).RequireAuthorization(p => p.RequireRole("Teacher", "Student"));

app.MapPost("/api/messenger/messages/{receiverId}", async (
    HttpContext ctx,
    string receiverId,
    IMessengerService messenger,
    ApplicationDbContext db) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Results.Unauthorized();

    var body = await ctx.Request.ReadFromJsonAsync<SendMessageBody>();
    if (body == null || string.IsNullOrWhiteSpace(body.Content)) return Results.BadRequest();

    var isTeacher = ctx.User.IsInRole("Teacher");
    bool linked = isTeacher
        ? await db.TeacherStudentAssignments.AnyAsync(a => a.TeacherId == userId && a.StudentId == receiverId)
        : await db.TeacherStudentAssignments.AnyAsync(a => a.StudentId == userId && a.TeacherId == receiverId);
    if (!linked) return Results.Forbid();

    await messenger.SaveMessageAsync(userId, receiverId, body.Content.Trim());
    return Results.Ok(new { ok = true });
}).RequireAuthorization(p => p.RequireRole("Teacher", "Student"));

app.MapPost("/api/messenger/ai", async (
    HttpContext ctx,
    IMessengerService messenger,
    IOllamaChatService chatSvc,
    UserManager<IdentityUser> userManager) =>
{
    var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Results.Unauthorized();

    var body = await ctx.Request.ReadFromJsonAsync<SendMessageBody>();
    if (body == null || string.IsNullOrWhiteSpace(body.Content)) return Results.BadRequest();

    var user = await userManager.FindByIdAsync(userId);
    var roles = await userManager.GetRolesAsync(user!);
    var userRole = roles.FirstOrDefault() ?? "Student";
    var userEmail = user?.Email ?? "";

    var history = await messenger.GetAiConversationAsync(userId, 10);
    var historyList = history
        .Select(m => (Role: m.IsFromAi ? "assistant" : "user", Content: m.Content))
        .ToList();

    string reply;
    try
    {
        reply = await chatSvc.ChatAsync(userEmail, userRole, historyList, body.Content.Trim());
    }
    catch (Exception ex)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ollama chat failed");
        return Results.Problem("Не удалось получить ответ от ИИ. Убедитесь, что Ollama запущена.");
    }

    await messenger.SaveAiExchangeAsync(userId, body.Content.Trim(), reply);
    return Results.Ok(new { reply });
}).RequireAuthorization(p => p.RequireRole("Teacher", "Student"));

app.Run();

record SendMessageBody(string Content);