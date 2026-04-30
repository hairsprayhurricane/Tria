# Tria — Образовательная платформа

Веб-приложение для онлайн-обучения с курсами, тестами и ИИ-проверкой развёрнутых ответов.

---

## Стек

| Компонент | Технология |
|-----------|-----------|
| Фреймворк | ASP.NET Core 10 (Razor Pages) |
| ORM | Entity Framework Core 10 |
| БД | SQL Server (LocalDB в dev) |
| Аутентификация | ASP.NET Identity |
| ИИ-проверка | Ollama (локальный HTTP-сервер) |
| Контент | XML-файлы (курсы, UI-строки) |

---

## Быстрый старт

### 1. Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (входит в Visual Studio) **или** полный SQL Server
- [Ollama](https://ollama.com) — для ИИ-проверки развёрнутых ответов (опционально)

### 2. База данных

Строка подключения по умолчанию в `appsettings.json`:

```
Server=(localdb)\MSSQLLocalDB;Database=Tria;Trusted_Connection=True;...
```

Миграции применяются автоматически при запуске. Если нужен другой сервер — поменяй `ConnectionStrings:DefaultConnection`.

### 3. Запуск

```bash
cd Tria
dotnet run
```

Приложение доступно на `https://localhost:5001` / `http://localhost:5000`.

---

## Аккаунты по умолчанию

| Роль | Email | Пароль |
|------|-------|--------|
| Admin | admin@tria.com | `q` |

Аккаунт создаётся автоматически при первом запуске если его нет в БД.

---

## ИИ-проверка тестов (Ollama + Qwen)

Развёрнутые ответы (тип вопроса `ShortAnswer`) проверяются локальной языковой моделью через Ollama.

### Требования к модели

Рекомендуется **`qwen2.5:7b`** — оптимальный баланс скорости и качества для задач проверки.  
Минимум VRAM/RAM: ~6 GB (квантизация Q4). Также подойдут `qwen2.5:14b` (точнее, медленнее) или `qwen2.5:3b` (быстрее, слабее).

### Установка

```bash
# 1. Скачать и установить Ollama с https://ollama.com

# 2. Скачать модель (~4.7 GB)
ollama pull qwen2.5:7b

# 3. Запустить сервер (или он стартует автоматически после установки)
ollama serve
```

Ollama слушает на `http://localhost:11434` — туда Tria и шлёт запросы.

### Настройка

Все параметры в `appsettings.json` → секция `"Ollama"`:

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Model": "qwen2.5:7b",
  "GradingIntervalSeconds": 15,
  "SystemPrompt": "..."
}
```

- `GradingIntervalSeconds` — как часто фоновый сервис проверяет наличие непроверенных ответов.
- `SystemPrompt` — роль и инструкции для модели; можно менять под нужды курса.

### Как работает проверка

1. Студент сдаёт тест с вопросами `ShortAnswer`.
2. Тест сохраняется со статусом **На проверке (Checking)**.
3. Фоновый сервис (`AiGradingBackgroundService`) каждые N секунд забирает непроверенные попытки и отправляет каждый ответ в Ollama.
4. Модель возвращает JSON: `{"isCorrect": true/false, "comment": "..."}`.
5. Когда все ответы проверены — балл пересчитывается, статус меняется на **Сдан / Не сдан**.
6. Студент видит результат и комментарий модели при следующем заходе на страницу теста.

Если Ollama недоступна — ответы остаются в статусе `Checking` и будут проверены при следующем запуске.

---

## Структура проекта

```
Tria/
├── Data/                    # ApplicationDbContext (EF Core)
├── Migrations/              # EF-миграции
├── Models/                  # Доменные модели (курсы, уроки, тесты, прогресс)
├── Options/                 # Классы конфигурации (OllamaOptions)
├── Pages/                   # Razor Pages
│   ├── Admin/               # Управление пользователями, назначение курсов
│   ├── Courses/             # Детали курса и модуля
│   ├── Lessons/             # Урок, материалы, тест
│   ├── Modules/             # Мини-игры
│   ├── Dashboard.cshtml     # Главная для студента
│   ├── Progress.cshtml      # Страница прогресса
│   └── Achievements.cshtml  # Достижения
├── Resources/
│   ├── Content/             # XML-строки UI (ui.ru.xml, ui.en.xml)
│   └── Courses/             # XML-файлы курсов (RU/, EN/)
├── Services/
│   ├── ILearningService     # Загрузка курсов из XML
│   ├── IProgressService     # Прогресс, XP, попытки тестов
│   ├── IOllamaGradingService # Интерфейс ИИ-проверки
│   ├── OllamaGradingService  # Реализация через Ollama HTTP API
│   └── AiGradingBackgroundService # Фоновый опросчик БД
├── appsettings.json
└── Program.cs
```

---

## Роли

| Роль | Доступ |
|------|--------|
| **Admin** | Управление пользователями, назначение курсов, просмотр всего |
| **Teacher** | Создание и редактирование курсов через редактор |
| **Student** | Прохождение назначенных курсов, тестов |
| **Expert** | Зарезервировано |

---

## Курсы и контент

Курсы хранятся в XML-файлах в `Resources/Courses/{LANG}/`. Структура: `Course → Module → Lesson → Materials + Test`.

Типы материалов в уроке:
- `Video` — YouTube-видео (по ID)
- `PDF` — файл из `wwwroot`
- `Image` — изображение из `wwwroot`

Типы вопросов в тесте:
- `MultipleChoice` — выбор одного варианта, проверяется мгновенно
- `ShortAnswer` — развёрнутый ответ, проверяется через Ollama

Порог сдачи теста настраивается на уровне теста (`PassScore`, по умолчанию 80%).

---

## Локализация

Поддерживаются **русский** (`ru`) и **английский** (`en`) языки.  
Переключение через `/set-language?culture=ru` или `culture=en`.  
UI-строки в `Resources/Content/ui.{lang}.xml`.
