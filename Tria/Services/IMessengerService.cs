using Tria.Models;

namespace Tria.Services;

public interface IMessengerService
{
    Task<List<ChatMessage>> GetConversationAsync(string userId1, string userId2, int limit = 50);
    Task<List<ChatMessage>> GetAiConversationAsync(string userId, int limit = 20);
    Task SaveMessageAsync(string senderId, string receiverId, string content);
    Task SaveAiExchangeAsync(string userId, string userMessage, string aiReply);
    Task<int> GetUnreadCountAsync(string userId);
}
