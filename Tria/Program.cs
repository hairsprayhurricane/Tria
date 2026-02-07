using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Models;
using Tria.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
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

builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await SeedDatabaseAsync(db);
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

async Task SeedDatabaseAsync(ApplicationDbContext db)
{
    if (await db.LearningBlocks.AnyAsync()) return;

    var block1 = new LearningBlock
    {
        Id = 1,
        Title = "📧 Коммуникации",
        Description = "Фишинг, SMS, звонки",
        Color = "#4a9eff",
        Order = 1
    };

    var block2 = new LearningBlock
    {
        Id = 2,
        Title = "🖥️ Устройства",
        Description = "Защита ПК и мобильных",
        Color = "#7c3aed",
        Order = 2
    };

    var block3 = new LearningBlock
    {
        Id = 3,
        Title = "🌐 Сети",
        Description = "Wi-Fi, VPN, прокси",
        Color = "#06b6d4",
        Order = 3
    };

    await db.LearningBlocks.AddRangeAsync(block1, block2, block3);
    await db.SaveChangesAsync();

    var modules = new List<Module>
    {
        new Module { BlockId = 1, Title = "Видео: Что такое фишинг?", Type = "Video", YoutubeId = "dQw4w9WgXcQ", Order = 1 },
        new Module { BlockId = 1, Title = "Тест: Фишинг", Type = "Quiz", Order = 2, ContentJson = GetQuizJson("Тест 1: Фишинг", new[] {
            ("Фишинг - это...", new[] { "Ловля рыбы", "Кража данных через ложные письма", "Игра на компьютере", "Покупка в интернете" }, 1),
            ("Как распознать фишинг?", new[] { "Проверить адрес отправителя", "Посмотреть картинки", "Прочитать дату", "Посчитать слова" }, 0),
            ("Что делать если это фишинг?", new[] { "Ответить письмо", "Удалить письмо", "Нажать на ссылку", "Передать другу" }, 1),
            ("Фишинг опасен?", new[] { "Нет", "Может быть", "Да, очень", "Только летом" }, 2),
            ("Куда сообщить о фишинге?", new[] { "Подруге", "В спам", "В поддержку сервиса", "Никому" }, 2)
        }) },

        new Module { BlockId = 2, Title = "Видео: Защита от вирусов", Type = "Video", YoutubeId = "jNQXAC9IVRw", Order = 1 },
        new Module { BlockId = 2, Title = "Тест: Устройства", Type = "Quiz", Order = 2, ContentJson = GetQuizJson("Тест 2: Устройства", new[] {
            ("Какой пароль безопасный?", new[] { "123456", "qwerty", "Pass@123!Secure", "password" }, 2),
            ("Когда обновлять ОС?", new[] { "Никогда", "Раз в год", "Как только доступно", "Только в понедельник" }, 2),
            ("Чем опасно открытый Wi-Fi?", new[] { "Ничем", "Кража данных", "Разряд батареи", "Медленный интернет" }, 1),
            ("Что такое антивирус?", new[] { "Программа от компьютера", "Программа защиты", "Интернет браузер", "Игра" }, 1),
            ("Нужен ли VPN?", new[] { "Нет", "Может быть", "Да, для безопасности", "Только для геймеров" }, 2)
        }) },

        new Module { BlockId = 3, Title = "Видео: Безопасность сетей", Type = "Video", YoutubeId = "9bZkp7q19f0", Order = 1 },
        new Module { BlockId = 3, Title = "Тест: Сети", Type = "Quiz", Order = 2, ContentJson = GetQuizJson("Тест 3: Сети", new[] {
            ("Wi-Fi с открытым доступом безопасен?", new[] { "Да", "Очень", "Нет, данные видны", "Зависит от дня" }, 2),
            ("Что такое VPN?", new[] { "Вирусная программа", "Защитный туннель в интернете", "Игра", "Браузер" }, 1),
            ("Нужен ли пароль для Wi-Fi?", new[] { "Нет", "Иногда", "Обязательно", "Только дома" }, 2),
            ("Кто может видеть мой трафик в открытой сети?", new[] { "Никто", "Провайдер", "Любой в сети", "Только админ" }, 2),
            ("Прокси помогает скрыть IP?", new[] { "Нет", "Может быть", "Да, частично", "Только вечером" }, 2)
        }) }
    };

    await db.Modules.AddRangeAsync(modules);
    await db.SaveChangesAsync();
}

static string GetQuizJson(string title, (string question, string[] options, int correct)[] questions)
{
    var quiz = new
    {
        title,
        description = "Проверьте ваши знания",
        passScore = 80,
        questions = questions.Select((q, i) => new
        {
            id = i,
            text = q.question,
            options = q.options,
            correctOptionIndex = q.correct,
            explanation = "Правильно!"
        }).ToList()
    };
    return System.Text.Json.JsonSerializer.Serialize(quiz);
}
