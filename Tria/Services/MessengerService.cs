using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Models;

namespace Tria.Services;

public class MessengerService : IMessengerService
{
    private readonly ApplicationDbContext _db;

    public MessengerService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ChatMessage>> GetConversationAsync(string userId1, string userId2, int limit = 50)
    {
        var unread = await _db.ChatMessages
            .Where(m => m.SenderId == userId2 && m.ReceiverId == userId1 && !m.IsRead && !m.IsFromAi)
            .ToListAsync();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync();

        // Explicit null guards ensure AI messages (which have null SenderId or ReceiverId) never leak in
        var msgs = await _db.ChatMessages
            .Where(m =>
                m.SenderId != null && m.ReceiverId != null &&
                ((m.SenderId == userId1 && m.ReceiverId == userId2) ||
                 (m.SenderId == userId2 && m.ReceiverId == userId1)))
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .ToListAsync();
        msgs.Reverse();
        return msgs;
    }

    public async Task<List<ChatMessage>> GetAiConversationAsync(string userId, int limit = 20)
    {
        var unread = await _db.ChatMessages
            .Where(m => m.ReceiverId == userId && m.IsFromAi && !m.IsRead)
            .ToListAsync();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync();

        var msgs = await _db.ChatMessages
            .Where(m =>
                (m.SenderId == userId && m.ReceiverId == null) ||
                (m.ReceiverId == userId && m.IsFromAi))
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .ToListAsync();
        msgs.Reverse();
        return msgs;
    }

    public async Task SaveMessageAsync(string senderId, string receiverId, string content)
    {
        _db.ChatMessages.Add(new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            IsFromAi = false
        });
        await _db.SaveChangesAsync();
    }

    public async Task SaveAiExchangeAsync(string userId, string userMessage, string aiReply)
    {
        _db.ChatMessages.Add(new ChatMessage
        {
            SenderId = userId,
            ReceiverId = null,
            Content = userMessage,
            SentAt = DateTime.UtcNow,
            IsRead = true,
            IsFromAi = false
        });
        _db.ChatMessages.Add(new ChatMessage
        {
            SenderId = null,
            ReceiverId = userId,
            Content = aiReply,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            IsFromAi = true
        });
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _db.ChatMessages
            .CountAsync(m => m.ReceiverId == userId && !m.IsRead);
    }
}
