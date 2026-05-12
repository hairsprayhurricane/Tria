using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tria.Options;

namespace Tria.Services;

public class OllamaChatService : IOllamaChatService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _opts;
    private readonly ILogger<OllamaChatService> _logger;

    // Chat-specific context injected on top of the base character prompt.
    private const string ChatContextTemplate =
        "Сейчас ты общаешься с {0} {1}. " +
        "Это живой разговор — не тест и не проверка. " +
        "Отвечай так, как ты есть: с теплом, иногда с неловкостью, всегда честно. " +
        "Можешь говорить о платформе Tria, кибербезопасности и учёбе. " +
        "Отвечай обычным текстом, не JSON.";

    public OllamaChatService(HttpClient http, IOptions<OllamaOptions> opts, ILogger<OllamaChatService> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<string> ChatAsync(
        string userEmail,
        string userRole,
        IReadOnlyList<(string Role, string Content)> history,
        string newMessage)
    {
        var roleLabel = userRole == "Teacher" ? "преподавателем" : "учеником";
        var contextLine = string.Format(ChatContextTemplate, roleLabel, userEmail);
        var systemPrompt = _opts.SystemPrompt + "\n\n" + contextLine;

        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        foreach (var (role, content) in history)
            messages.Add(new { role, content });
        messages.Add(new { role = "user", content = newMessage });

        var payload = new
        {
            model = _opts.Model,
            stream = false,
            options = new { num_predict = 600 },
            messages
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/api/chat", payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama chat request failed");
            throw;
        }

        var raw = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
