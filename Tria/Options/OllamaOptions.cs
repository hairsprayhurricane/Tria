namespace Tria.Options;

public class OllamaOptions
{
    public const string Section = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5:7b";
    public int GradingIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Задаётся в appsettings.json → секция "Ollama" → поле "SystemPrompt".
    /// </summary>
    public string SystemPrompt { get; set; } = "";
}
