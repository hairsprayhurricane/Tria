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
    private readonly SentinelLogger _sentinel;

    public OllamaGradingService(HttpClient http, IOptions<OllamaOptions> opts, ILogger<OllamaGradingService> logger, SentinelLogger sentinel)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
        _sentinel = sentinel;
    }

    public async Task<(bool IsCorrect, string Comment)> GradeAnswerAsync(string questionText, string userAnswer)
    {
        var userMessage = $"Вопрос: {questionText}\n\nОтвет студента: {userAnswer}";
        _sentinel.Log($"ЗАПРОС | Вопрос: {questionText} | Ответ: {userAnswer}");

        var payload = new
        {
            model = _opts.Model,
            stream = false,
            options = new { num_predict = 120 },
            messages = new[]
            {
                new { role = "system", content = _opts.SystemPrompt },
                new { role = "user",   content = userMessage }
            }
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/api/chat", payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _sentinel.LogError("HTTP запрос к Ollama", ex);
            throw;
        }

        var raw = await response.Content.ReadAsStringAsync();
        _sentinel.Log($"RAW ОТВЕТ: {raw}");

        try
        {
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

            _sentinel.Log($"РЕЗУЛЬТАТ | isCorrect={isCorrect} | comment={comment}");
            return (isCorrect, comment);
        }
        catch (Exception ex)
        {
            _sentinel.LogError("Парсинг ответа Ollama", ex);
            throw;
        }
    }
}
