namespace Tria.Services;

public interface IOllamaGradingService
{
    /// <summary>
    /// Sends questionText + userAnswer to the local Ollama model and returns
    /// whether the answer is correct and a brief Russian-language comment.
    /// </summary>
    Task<(bool IsCorrect, string Comment)> GradeAnswerAsync(string questionText, string userAnswer);
}
