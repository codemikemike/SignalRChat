using SignalRChat.Server.Models;

namespace SignalRChat.Server.Interfaces;

/// <summary>
/// Stores a rolling history of messages per room.
/// Allows new joiners to see recent context.
/// </summary>
public interface IMessageHistoryService
{
    void AddMessage(ChatMessage message);
    IReadOnlyCollection<ChatMessage> GetRecentMessages(string room, int count = 50);
}
