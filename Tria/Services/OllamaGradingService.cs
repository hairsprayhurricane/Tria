using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tria.Options;

namespace Tria.Services;

public class OllamaGradingService : IOllamaGradingService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _opts;
    private readonly ILogger<OllamaGradingService> _logger;

    public OllamaGradingService(HttpClient http, IOptions<OllamaOptions> opts, ILogger<OllamaGradingService> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<(bool IsCorrect, string Comment)> GradeAnswerAsync(string questionText, string userAnswer)
    {
        var userMessage = $"Вопрос: {questionText}\n\nОтвет студента: {userAnswer}";

        var payload = new
        {
            model = _opts.Model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = _opts.SystemPrompt },
                new { role = "user",   content = userMessage }
            }
        };

        var response = await _http.PostAsJsonAsync("/api/chat", payload);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        // Strip potential markdown code fences the model may wrap around JSON
        var start = content.IndexOf('{');
        var end   = content.LastIndexOf('}');
        if (start >= 0 && end > start)
            content = content[start..(end + 1)];

        using var result = JsonDocument.Parse(content);
        var isCorrect = result.RootElement.GetProperty("isCorrect").GetBoolean();
        var comment   = result.RootElement.GetProperty("comment").GetString() ?? "";

        return (isCorrect, comment);
    }
}
