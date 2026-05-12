# Tria

Образовательная веб-платформа на ASP.NET Core 10 (Razor Pages). Курсы загружаются из XML, прогресс хранится в PostgreSQL, развёрнутые ответы на тесты проверяются локальной LLM через Ollama.

---

## Стек

| Слой | Технология |
|---|---|
| Framework | ASP.NET Core 10, Razor Pages |
| ORM | Entity Framework Core 10 |
| БД | PostgreSQL (Npgsql) |
| Auth | ASP.NET Core Identity, cookie auth |
| AI | Ollama HTTP API (локальная LLM) |
| Игры | Unity WebGL (Brotli-сжатые сборки) |
| Контент | XML-файлы (курсы + UI-строки) |

---

## Запуск

### Требования

- .NET 10 SDK
- PostgreSQL
- Ollama (опционально)

### Конфигурация (`appsettings.json`)

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=tria;Username=...;Password=..."
},
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Model": "qwen2.5:7b",
  "GradingIntervalSeconds": 15,
  "SystemPrompt": "Ты — Сентинел. ..."
}
```

### Запуск

```bash
cd Tria
dotnet run
```

При старте `Program.cs` автоматически:
1. Применяет EF-миграции (`db.Database.Migrate()`)
2. Создаёт роли `Admin`, `Teacher`, `Student` если их нет
3. Создаёт учётную запись `admin@tria.com` / `q` если её нет

---

## Структура проекта

```
Tria/
├── Data/
│   └── ApplicationDbContext.cs      # EF DbContext
├── Migrations/                      # EF-миграции
├── Models/
│   ├── LearningModels.cs            # Course, CourseModule, Lesson, LessonTest, TestQuestion, LessonMaterial
│   ├── UserLessonProgress.cs        # Прогресс по уроку + XP
│   ├── UserTestAttempt.cs           # Попытка теста, UserAnswer, TestStatus enum
│   ├── CourseReview.cs              # Отзыв студента о курсе
│   ├── ChatMessage.cs               # Сообщение в мессенджере (student↔teacher или student↔AI)
│   ├── UserNotification.cs          # Уведомление
│   └── TeacherStudentAssignment.cs  # Связь преподаватель–студент
├── Options/
│   └── OllamaOptions.cs             # BaseUrl, Model, GradingIntervalSeconds, SystemPrompt
├── Pages/
│   ├── Welcome, Login, Register     # Публичные страницы
│   ├── Dashboard, Progress, Achievements  # Студент
│   ├── Courses/, Lessons/, Modules/ # Учебный контент
│   ├── Messenger/                   # Чат
│   ├── Teacher/                     # Редактор курсов, проверка заданий
│   ├── Admin/                       # Управление пользователями, SQL-консоль
│   └── Shared/_Layout.cshtml        # Общий layout с навигацией
├── Services/
│   ├── ILearningService / LearningService        # Загрузка курсов из XML (in-memory кеш)
│   ├── IProgressService / ProgressService        # Прогресс, XP, попытки тестов
│   ├── INotificationService / NotificationService
│   ├── IMessengerService / MessengerService
│   ├── IOllamaGradingService / OllamaGradingService   # AI-проверка ShortAnswer
│   ├── IOllamaChatService / OllamaChatService         # AI-чат в мессенджере
│   ├── AiGradingBackgroundService                     # BackgroundService, опрос очереди
│   ├── XmlUiLocalizer / IUiLocalizer                  # Локализация UI из XML
│   └── SentinelLogger                                 # Файловый лог в SentinelLog.txt
├── Resources/
│   ├── Content/ui.ru.xml            # UI-строки
│   └── Courses/RU/*.xml             # Описания курсов
└── wwwroot/
    ├── Resources/GameContent/       # Unity WebGL сборки (.data.br, .wasm.br, .framework.js.br)
    ├── CourseContent/Documents/RU/  # PDF-материалы
    ├── img/logo.png                 # Логотип
    └── css/, js/, lib/              # Стили, скрипты, Bootstrap, jQuery
```

---

## Контентная система

### Курсы (XML)

Курсы хранятся в `Resources/Courses/RU/*.xml` и загружаются `LearningService` при старте в in-memory кеш. В БД курсовые данные **не хранятся** — только прогресс пользователей.

Иерархия: `Course → CourseModule → Lesson → LessonMaterial | LessonTest`

**Типы материалов** (`LessonMaterial.Type`):
- `Video` — YouTube ID в поле `YoutubeId`
- `PDF` — путь к файлу в `wwwroot` в поле `FilePath`
- `Image` — аналогично PDF

**Типы вопросов** (`TestQuestion.Type`):
- `MultipleChoice` — проверяется немедленно по `CorrectOptionIndex`
- `ShortAnswer` — ставится в очередь для AI-проверки (`TestStatus.Checking`)

Пример структуры XML:
```xml
<Course Id="1" Key="cybersec" Access="public">
  <Module Id="10" Order="1" Difficulty="Easy" HasGame="false">
    <Lesson Id="100" Order="1" Difficulty="Medium">
      <Materials>
        <Material Type="Video" YoutubeId="dQw4w9WgXcQ" />
        <Material Type="PDF" FilePath="/CourseContent/Documents/RU/..." />
      </Materials>
      <Test PassScore="80">
        <Question Type="MultipleChoice" Text="...">
          <Options>...</Options>
          <CorrectOptionIndex>2</CorrectOptionIndex>
        </Question>
        <Question Type="ShortAnswer" Text="Объясните, что такое XSS." />
      </Test>
    </Lesson>
  </Module>
</Course>
```

**`Course.Access`** — зарезервировано для будущего разграничения доступа. Сейчас `"public"` = доступно всем аутентифицированным пользователям.

### Доступ к курсам

Сейчас курсы назначаются через `UserCourseAssignment` (Admin-панель). Поле `Course.Access = "public"` зарезервировано для будущей логики — не реализовано.

---

## Система прогресса и XP

Прогресс хранится в `UserLessonProgress`:

| Поле | Описание |
|---|---|
| `MaterialsCompleted` | True после клика «Материалы пройдены» |
| `TestPassed` | True если тест сдан (score% ≥ PassScore) |
| `IsCompleted` | Вычисляемое: `MaterialsCompleted && TestPassed` |
| `XpEarned` | Начисляется при `CompleteMaterialsAsync` |

**XP за урок** вычисляется из `Lesson.XpReward`:
```csharp
DifficultyLevel.Easy   => 10 XP
DifficultyLevel.Medium => 25 XP
DifficultyLevel.Hard   => 45 XP
```

XP начисляется ровно один раз (при первом завершении материалов) — повторное прохождение не даёт XP.

`IProgressService.GetTotalXpAsync(userId)` — сумма всех `XpEarned` по пользователю.

---

## Достижения

Вычисляются в `AchievementsModel.OnGetAsync` на лету из прогресса, в БД не хранятся.

**Глобальные:**
- `bronze` — первый урок завершён
- `silver` — набрано ≥ 100 XP

**Для каждого курса** (на основе % завершённых уроков, где урок = `MaterialsCompleted && TestPassed`):
- `bronze` — ≥ 33%
- `silver` — ≥ 66%
- `gold`   — 100%

Тир (`bronze` / `silver` / `gold`) используется только для отображения на странице — никакого влияния на другую логику нет.

---

## AI-система (Сентинел)

Два независимых сервиса, оба бьют в `POST /api/chat` Ollama.

### OllamaGradingService — проверка ответов

Вызывается только из `AiGradingBackgroundService`. Принимает текст вопроса и ответ студента.

**System prompt** = `OllamaOptions.SystemPrompt` + захардкоженный `GradingInstructions` с персонажем и примерами.

Ожидаемый ответ модели — строгий JSON:
```json
{"isCorrect": true, "comment": "..."}
```

Парсинг: ищет первый `{` и последний `}` в ответе (защита от markdown-обёртки). Если парсинг упал — бросает исключение, попытка остаётся в `Checking` до следующего тика.

**Лимит токенов**: `num_predict = 350`

### OllamaChatService — мессенджер

Свободный разговор. В system prompt добавляется контекст с ролью пользователя (`Teacher` / `Student`) и email. История берётся из последних 30 сообщений (`GetAiConversationAsync`).

**Лимит токенов**: `num_predict = 600`

### AiGradingBackgroundService

`BackgroundService`, запускается вместе с приложением.

Цикл:
1. `SELECT * FROM UserTestAttempts WHERE Status = 'Checking' ORDER BY StartedAt`
2. Для каждой попытки — проходит по `answers` где `QuestionType = "ShortAnswer"` и `AiCheckedAt IS NULL`
3. Вызывает `GradeAnswerAsync(question.Text, answer.TextAnswer)`
4. Записывает `IsCorrect`, `PointsEarned`, `AiComment`, `AiCheckedAt`
5. Если все ShortAnswer-вопросы проверены — пересчитывает итоговый `Score`, устанавливает `Status = Passed/Failed`, создаёт уведомление пользователю

Интервал опроса: `OllamaOptions.GradingIntervalSeconds` (default: 15 сек).

Если Ollama недоступна — исключение логируется в `SentinelLog.txt`, попытка остаётся в `Checking`, будет обработана на следующем тике.

### Персонаж Сентинела

Характер задаётся через `OllamaOptions.SystemPrompt` в `appsettings.json`. Для проверки ответов поверх него накладывается `GradingInstructions` с 4 примерами реакций:
- Ответ верный и осознанный
- Ответ неверный, но студент думал
- Ответ халтурный
- Ответ пустой / сдача

Стиль — тёплый, личный, от первого лица, без казённых фраз.

---

## Мессенджер

Маршруты API в `Program.cs`:

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/messenger/conversations` | Список диалогов (Teacher ↔ Students, + AI) |
| `GET` | `/api/messenger/messages/{contactId}` | История сообщений |
| `POST` | `/api/messenger/messages/{receiverId}` | Отправить сообщение человеку |
| `POST` | `/api/messenger/ai` | Отправить сообщение Сентинелу |

`contactId = "ai"` — зарезервированный ID для диалога с Сентинелом. Доступно только ролям `Teacher` и `Student`.

---

## Аутентификация и роли

Cookie auth, 7 дней, sliding expiration.

| Роль | Доступ |
|---|---|
| `Admin` | Полное управление, SQL-консоль (`/Admin/SqlConsole`) |
| `Teacher` | Редактор курсов, проверка заданий, мессенджер |
| `Student` | Курсы, тесты, мессенджер, достижения |

Middleware `RoleBasedAuthHandler` переопределяет стандартное поведение `IAuthorizationMiddlewareResultHandler` для кастомных редиректов по роли.

---

## Локализация

Только русский язык. UI-строки в `Resources/Content/ui.ru.xml`.

`XmlUiLocalizer` читает XML при первом обращении и кеширует в `ConcurrentDictionary<string, Dictionary<string, string>>`. Инвалидации нет — перезапуск требуется для применения изменений в XML.

Использование в Razor:
```csharp
@inject IUiLocalizer T
<h1>@T["Login_Heading"]</h1>
```

---

## Unity WebGL

Сборки лежат в `wwwroot/Resources/GameContent/{GameKey}/Build/`. Файлы сжаты Brotli (`.data.br`, `.wasm.br`, `.framework.js.br`).

В `Program.cs` настроен `StaticFileOptions` с кастомным `OnPrepareResponse`, который выставляет `Content-Encoding: br` и корректные MIME-типы (`application/wasm`, `application/javascript`).

Модуль со связанной игрой: `CourseModule.HasGame = true`, `CourseModule.GameKey = "communications_Game"`.

---

## Логирование

`SentinelLogger` — простой файловый логгер, пишет в `SentinelLog.txt` в корне проекта. Используется для трассировки AI-запросов: что пришло, что Ollama ответила, результат парсинга.

```
==================== НОВЫЙ ЗАПУСК ====================
ЗАПРОС | Вопрос: ... | Ответ: ...
RAW ОТВЕТ: {"message": ...}
РЕЗУЛЬТАТ | isCorrect=true | comment=...
```

Стандартный `ILogger` используется параллельно для прочей диагностики.
