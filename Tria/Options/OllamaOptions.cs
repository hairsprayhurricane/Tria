namespace Tria.Options;

public class OllamaOptions
{
    public const string Section = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5:7b";
    public int GradingIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// System prompt injected before every grading request.
    /// Can be overridden in appsettings.json under "Ollama:SystemPrompt".
    /// </summary>
    public string SystemPrompt { get; set; } =
        "Ты учитель-проверщик образовательной платформы Tria. " +
        "Твоя задача — оценивать развёрнутые ответы учеников на вопросы тестов. " +
        "Отвечай ТОЛЬКО JSON-объектом без лишнего текста и без markdown-блоков кода, " +
        "в строго следующем формате: {\"isCorrect\": true, \"comment\": \"Обоснование на русском языке\"}. " +
        "Засчитывай ответ как верный, если смысл совпадает, даже при неточных формулировках. " +
        "В комментарии кратко объясни своё решение.";
}
