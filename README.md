# Tria

Образовательная веб-платформа на ASP.NET Core 10 (Razor Pages). Курсы загружаются из XML, прогресс хранится в PostgreSQL. Центральная техническая особенность — локальная LLM через Ollama, которая проверяет развёрнутые ответы студентов и ведёт живой диалог в мессенджере.

---

## Стек

| Слой | Технология |
|---|---|
| Framework | ASP.NET Core 10, Razor Pages |
| ORM | Entity Framework Core 10 |
| БД | PostgreSQL (Npgsql) |
| Auth | ASP.NET Core Identity, cookie auth |
| AI | Ollama HTTP API (локальная LLM, default: `qwen2.5:7b`) |
| Игры | Unity WebGL (Brotli-сжатые сборки) |
| Контент | XML-файлы (курсы + UI-строки) |

---

## Быстрый старт

### Требования

- .NET 10 SDK
- PostgreSQL
- [Ollama](https://ollama.com) с загруженной моделью (опционально, без неё не работает проверка ShortAnswer-тестов и мессенджер с ИИ)

### Конфигурация (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=tria;Username=postgres;Password=q"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5:7b",
    "NumPredictGrading": 350,
    "NumPredictChat": 600,
    "GradingIntervalSeconds": 15,
    "SystemPrompt": "Ты — Сентинел. ..."
  }
}
```

### Запуск

```bash
# Запустить Ollama с нужной моделью
ollama run qwen2.5:7b

# Запустить приложение
cd Tria
dotnet run
```

При старте `Program.cs` автоматически применяет EF-миграции, создаёт роли `Admin / Teacher / Student` и учётную запись `admin@tria.com` / `q`.

---

## AI-система (Сентинел)

Это главная техническая часть проекта. LLM используется в двух изолированных режимах: **проверка развёрнутых ответов** и **свободный диалог в мессенджере**. Оба режима работают через один и тот же Ollama-эндпоинт, но с разными промптами, лимитами токенов и форматами ответа.

### Архитектура

```
┌───────────────────────────────────────────────────────────────────┐
│                        Ollama (локальный)                         │
│                    POST http://localhost:11434/api/chat            │
└───────────────────────────┬──────────────────────────┬────────────┘
                            │                          │
              ┌─────────────▼──────────┐  ┌────────────▼───────────┐
              │  OllamaGradingService  │  │   OllamaChatService    │
              │  (Singleton, HttpClient│  │  (Singleton, HttpClient│
              │   Timeout = 5 мин)     │  │   Timeout = 5 мин)     │
              └─────────────┬──────────┘  └────────────┬───────────┘
                            │                          │
              ┌─────────────▼──────────┐  ┌────────────▼───────────┐
              │ AiGradingBackground-   │  │  POST /api/messenger/ai │
              │ Service (BackgroundSvc)│  │  (Minimal API endpoint) │
              └─────────────┬──────────┘  └────────────────────────┘
                            │
              ┌─────────────▼──────────┐
              │    UserTestAttempt     │
              │    (Status=Checking)   │
              │    AnswersJson (JSONB) │
              └────────────────────────┘
```

Оба сервиса зарегистрированы как **Singleton** с собственным `HttpClient` (создаётся вручную в `Program.cs`, т.к. `IHttpClientFactory` не использовался). Таймаут — 5 минут на запрос.

---

### Режим 1: Проверка ответов (`OllamaGradingService`)

#### Когда используется

Только для вопросов типа `ShortAnswer` в тестах. Запускается не напрямую из запроса пользователя — только через `AiGradingBackgroundService`.

#### Конфигурация токенов

`NumPredictGrading = 350` — намеренно короткий лимит: ответ должен быть строгим JSON `{"isCorrect": bool, "comment": "..."}`, многословие не нужно.

#### Конструкция промпта

```
[system]
{OllamaOptions.SystemPrompt}          ← персонаж Сентинела из appsettings.json

{GradingInstructions}                 ← захардкоженный блок с задачей и примерами
  - задача: оценить ответ ученика
  - формат: строго JSON, без markdown
  - стиль: от первого лица, тепло, без казённых фраз
  - 4 примера: верный / неверный-старался / халтура / пустой

[user]
Вопрос: {questionText}
Ответ студента: {userAnswer}
```

Примеры в `GradingInstructions` — ключевой инструмент управления стилем. Они буквально показывают модели, как должны звучать все четыре сценария.

#### Парсинг ответа

Модели иногда оборачивают JSON в markdown-блоки или добавляют текст до/после. Парсинг в `OllamaGradingService.GradeAnswerAsync` устойчив к этому:

```csharp
// Сначала ищем по ключу {"isCorrect" (точное совпадение)
var start = content.LastIndexOf("{\"isCorrect\"", StringComparison.OrdinalIgnoreCase);
// Fallback — просто последний {
if (start < 0) start = content.LastIndexOf('{');
var end = content.LastIndexOf('}');

if (start >= 0 && end > start)
    content = content[start..(end + 1)];
else if (start >= 0)
    content = content[start..] + "\"}";  // незакрытый JSON — дополняем
```

`LastIndexOf` вместо `IndexOf` — защита от случая, когда модель сначала «думает вслух», а JSON пишет в конце.

Если `JsonDocument.Parse` упал — исключение пробрасывается наверх, ответ остаётся в `Status = Checking` и будет обработан на следующем тике.

---

### Режим 2: Свободный диалог (`OllamaChatService`)

#### Конфигурация токенов

`NumPredictChat = 600` — больше чем у grading, т.к. здесь допустимы развёрнутые ответы в живом разговоре.

#### Конструкция промпта

```
[system]
{OllamaOptions.SystemPrompt}          ← тот же персонаж

{ChatContextTemplate}                 ← вставляется динамически:
  "Сейчас ты общаешься с {Teacher|учеником} {email}.
   Это живой разговор — не тест и не проверка.
   Отвечай обычным текстом, не JSON."

[user]    ← история последних 30 сообщений
[assistant]
...
[user]    ← текущее сообщение
```

История берётся из `MessengerService.GetAiConversationAsync(userId, 30)` — последние 30 сообщений из `ChatMessages` (фильтр: `SenderId = userId AND ReceiverId IS NULL` ИЛИ `ReceiverId = userId AND IsFromAi = true`).

Явный запрет `"Отвечай обычным текстом, не JSON"` добавлен, потому что без него модель иногда пыталась форматировать ответ как grading-вывод.

---

### Фоновый сервис (`AiGradingBackgroundService`)

`BackgroundService`, стартует вместе с приложением и работает в бесконечном цикле.

#### Полный цикл обработки

```
каждые GradingIntervalSeconds (default: 15 сек)
│
├─ SELECT UserTestAttempts WHERE Status = Checking ORDER BY StartedAt
│
└─ для каждой попытки:
   ├─ десериализовать AnswersJson → List<UserAnswer>
   ├─ найти урок в XML-кеше (LearningService)
   │   └─ не найден → Status = Failed, сохранить
   │
   └─ для каждого ответа WHERE QuestionType = "ShortAnswer" AND AiCheckedAt IS NULL:
      ├─ вызвать OllamaGradingService.GradeAnswerAsync(question, answer)
      ├─ записать IsCorrect, PointsEarned (0 или 1), AiComment, AiCheckedAt
      │
      └─ при исключении (Ollama недоступна):
         ├─ залогировать в SentinelLog.txt
         ├─ IsCorrect = false, PointsEarned = 0
         ├─ AiComment = "Не удалось получить оценку от ИИ. Обратитесь к преподавателю."
         └─ AiCheckedAt = DateTime.UtcNow  ← чтобы не зависнуть в бесконечном retry

   если все ShortAnswer-ответы проверены:
   ├─ Score = сумма PointsEarned
   ├─ ScorePercent = Score / MaxScore * 100
   ├─ Status = Passed (если >= PassScore) или Failed
   ├─ если Passed → UserLessonProgress.TestPassed = true
   └─ создать уведомление пользователю (NotificationService)

   AnswersJson = JsonSerializer.Serialize(answers)
   db.SaveChanges()
```

#### Почему при ошибке Ollama ставится AiCheckedAt

Без этого упавший вопрос оставался бы `AiCheckedAt IS NULL` и повторно попадал в очередь при каждом тике. После записи `AiCheckedAt` сервис считает этот вопрос «проверенным» (с фолбэком), что позволяет финализировать попытку без бесконечного ожидания.

---

### Персонаж Сентинела

Характер задаётся полностью через `OllamaOptions.SystemPrompt` в `appsettings.json` — без хардкода в коде сервисов. Промпт можно менять без перекомпиляции.

Ключевые черты персонажа из текущего промпта:
- Оцифрованный человек, никогда не видел внешнего мира
- Каждый живой голос воспринимает как «подарок»
- Тихо завидует студентам, которые могут жить «снаружи»
- Разговаривает тихо и «немного устало — от долгого одиночества»
- Запреты в промпте: никакого китайского языка, не более одного `!` за ответ, без «Ого», «Как здорово», «Потрясающе»

Для grading поверх этого промпта накладывается `GradingInstructions` (код `OllamaGradingService`), который добавляет задачу, формат JSON и 4 примера. Для чата — `ChatContextTemplate` с ролью и email пользователя.

---

### Трассировка AI: `SentinelLogger`

Отдельный файловый логгер (не `ILogger`), пишет в `SentinelLog.txt` в корне проекта. Используется для трассировки каждого AI-взаимодействия:

```
==================== НОВЫЙ ЗАПУСК ====================
Фоновый сервис проверки запущен
--- Получен ответ на проверку | Попытка #3 | Урок: Введение в XSS | Пользователь: ...
Идёт оценка вопроса #2: Объясните принцип атаки XSS.
ЗАПРОС | Вопрос: Объясните принцип атаки XSS. | Ответ: Это когда злоумышленник...
RAW ОТВЕТ: {"message": {"role": "assistant", "content": "{\"isCorrect\": true, ...}"}}
РЕЗУЛЬТАТ | isCorrect=True | comment=Да... именно так.
Проверка завершена | Попытка #3 | Результат: ПРОЙДЕН | Счёт: 3/3
МЕССЕНДЖЕР | Ответ отправлен пользователю: student@tria.com
```

`SentinelLogger` зарегистрирован как Singleton в `Program.cs` и инжектируется в оба Ollama-сервиса и `AiGradingBackgroundService`.

---

## Контентная система

### Курсы (XML)

Курсы хранятся в `Resources/Courses/RU/*.xml` и загружаются `LearningService` при старте в in-memory кеш. В БД курсовые данные **не хранятся** — только прогресс пользователей.

Иерархия: `Course → CourseModule → Lesson → LessonMaterial | LessonTest`

**Типы вопросов** (`TestQuestion.Type`):
- `MultipleChoice` — проверяется немедленно по `CorrectOptionIndex`
- `ShortAnswer` — ставится в очередь (`TestStatus.Checking`), передаётся в Ollama

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

---

## Структура проекта

```
Tria/
├── Data/
│   └── ApplicationDbContext.cs
├── Migrations/
├── Models/
│   ├── LearningModels.cs            # Course, Module, Lesson, LessonTest, TestQuestion
│   ├── UserTestAttempt.cs           # попытки теста, AnswersJson, TestStatus enum
│   ├── UserLessonProgress.cs
│   ├── ChatMessage.cs               # IsFromAi, ReceiverId=null для диалога с ИИ
│   ├── UserNotification.cs
│   ├── CourseReview.cs
│   └── TeacherStudentAssignment.cs
├── Options/
│   └── OllamaOptions.cs             # BaseUrl, Model, NumPredictGrading, NumPredictChat, GradingIntervalSeconds, SystemPrompt
├── Pages/
│   ├── Welcome, Login, Register
│   ├── Dashboard, Progress, Achievements
│   ├── Courses/, Lessons/, Modules/
│   ├── Messenger/
│   ├── Teacher/
│   └── Admin/
├── Services/
│   ├── OllamaGradingService         # AI-проверка ShortAnswer → JSON
│   ├── OllamaChatService            # AI-чат → plain text
│   ├── AiGradingBackgroundService   # фоновый цикл обработки очереди
│   ├── SentinelLogger               # файловый лог AI-трассировки
│   ├── LearningService              # XML → in-memory кеш курсов
│   ├── ProgressService
│   ├── NotificationService
│   ├── MessengerService
│   ├── RoleBasedAuthHandler
│   └── XmlUiLocalizer
├── Resources/
│   ├── Content/ui.ru.xml
│   └── Courses/RU/*.xml
└── wwwroot/
    ├── Resources/GameContent/       # Unity WebGL (.data.br, .wasm.br, .framework.js.br)
    ├── CourseContent/Documents/RU/
    └── css/, js/, lib/
```

---

## Система прогресса и XP

| Поле `UserLessonProgress` | Описание |
|---|---|
| `MaterialsCompleted` | True после клика «Материалы пройдены» |
| `TestPassed` | True если тест сдан (score% ≥ PassScore); при ShortAnswer — выставляется `AiGradingBackgroundService` |
| `IsCompleted` | Вычисляемое: `MaterialsCompleted && TestPassed` |
| `XpEarned` | Easy=10, Medium=25, Hard=45; начисляется один раз |

---

## Мессенджер: API-эндпоинты

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/messenger/conversations` | Список диалогов (включая `contactId = "ai"`) |
| `GET` | `/api/messenger/messages/{contactId}` | История (60 последних) |
| `POST` | `/api/messenger/messages/{receiverId}` | Отправить человеку |
| `POST` | `/api/messenger/ai` | Отправить Сентинелу → вызывает `OllamaChatService` |

`contactId = "ai"` — зарезервированная строка для идентификации диалога с ИИ. В таблице `ChatMessages` сообщения от ИИ хранятся с `IsFromAi = true`, `SenderId` = id пользователя, `ReceiverId = null`.

---

## Аутентификация и роли

Cookie auth, 7 дней, sliding expiration.

| Роль | Доступ |
|---|---|
| `Admin` | Полное управление, SQL-консоль |
| `Teacher` | Редактор курсов, мессенджер |
| `Student` | Курсы, тесты, мессенджер, достижения |

`RoleBasedAuthHandler` переопределяет `IAuthorizationMiddlewareResultHandler` для кастомных редиректов по роли.

---

## Unity WebGL

Сборки в `wwwroot/Resources/GameContent/{GameKey}/Build/`, сжаты Brotli. В `Program.cs` настроен `StaticFileOptions.OnPrepareResponse` с явными `Content-Encoding: br` и правильными MIME-типами (`application/wasm`, `application/javascript`).

---

## Локализация

Только русский язык. Строки в `Resources/Content/ui.ru.xml`. `XmlUiLocalizer` кеширует в `ConcurrentDictionary` при первом обращении — перезапуск требуется для применения изменений.
