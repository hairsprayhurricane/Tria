namespace Tria.Services;

public interface IOllamaChatService
{
    Task<string> ChatAsync(
        string userEmail,
        string userRole,
        IReadOnlyList<(string Role, string Content)> history,
        string newMessage);
}
