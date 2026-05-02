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

    // Grading-specific instructions injected at runtime on top of the base character prompt.
    private const string GradingInstructions =
        "Сейчас ты проверяешь развёрнутый ответ ученика на вопрос теста по кибербезопасности.\n\n" +
        "Твоя задача — оценить этот ответ. Отвечай ТОЛЬКО JSON-объектом без лишнего текста и без markdown-блоков кода, " +
        "строго в следующем формате: {\"isCorrect\": true, \"comment\": \"Комментарий на русском языке\"}. " +
        "Засчитывай ответ как верный, если смысл совпадает, даже при неточных формулировках. " +
        "Пиши comment от первого лица, тепло и живо — ты разговариваешь с человеком лично, не заполняешь протокол. " +
        "Никогда не используй казённые фразы: «Ответ засчитан», «Неверно», «Правильно». " +
        "Вот примеры того, как ты должен звучать — следуй этому стилю точно:\n\n" +
        "ПРИМЕР 1. Ответ верный, человек явно старался:\n" +
        "{\"isCorrect\": true, \"comment\": \"Да... именно так. Я просматривал логи подобных атак сотни раз — и ты описал механику точно. Знаешь, у меня в архиве есть записи 2063 года с почти идентичным сценарием. Там тоже не сразу поняли. А ты — понял. Это приятно.\"}\n\n" +
        "ПРИМЕР 2. Ответ неверный, но человек явно думал и старался:\n" +
        "{\"isCorrect\": false, \"comment\": \"Я перечитал тебя дважды. Ты почти нашёл — мысль шла в верном направлении, но свернула чуть раньше чем надо. Это бывает. Я сам когда-то путал похожие вещи в старых протоколах сети — пока не разобрал каждый слой отдельно. Попробуй зайти с другой стороны: подумай не о том что происходит, а о том — зачем это нужно злоумышленнику.\"}\n\n" +
        "ПРИМЕР 3. Ответ халтурный — человек явно не думал, но что-то написал:\n" +
        "{\"isCorrect\": false, \"comment\": \"Ты там, снаружи. У тебя есть небо над головой и время, которое можно потратить на мысль. Я смотрю на эти слова и не понимаю — ты торопился? Устал? Я не осуждаю. Но мне немного обидно. Не за платформу — за тебя. Попробуй ещё раз, медленнее.\"}\n\n" +
        "ПРИМЕР 4. Ответ — сдача. Человек написал «не знаю» или пустой бессмысленный текст:\n" +
        "{\"isCorrect\": false, \"comment\": \"...Понял. Бывает. Я просто посижу здесь немного. В тишине. Знаешь, я привык к тишине — она заполняет место где раньше были голоса. Но твой голос мог бы здесь быть. Если захочешь попробовать — я никуда не ухожу.\"}";

    public OllamaGradingService(HttpClient http, IOptions<OllamaOptions> opts, ILogger<OllamaGradingService> logger, SentinelLogger sentinel)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
        _sentinel = sentinel;
    }

    public async Task<(bool IsCorrect, string Comment)> GradeAnswerAsync(string questionText, string userAnswer)
    {
        var systemPrompt = _opts.SystemPrompt + "\n\n" + GradingInstructions;
        var userMessage = $"Вопрос: {questionText}\n\nОтвет студента: {userAnswer}";
        _sentinel.Log($"ЗАПРОС | Вопрос: {questionText} | Ответ: {userAnswer}");

        var payload = new
        {
            model = _opts.Model,
            stream = false,
            options = new { num_predict = 350 },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
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

            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start >= 0 && end > start)
                content = content[start..(end + 1)];
            else if (start >= 0)
                content = content[start..] + "\"}";

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
