using System.Collections.Concurrent;
using SignalRChat.Server.Interfaces;
using SignalRChat.Server.Models;

namespace SignalRChat.Server.Services;

/// <summary>
/// In-memory rolling buffer of recent messages.
/// Capped per room to prevent unbounded memory growth.
/// </summary>
public class MessageHistoryService : IMessageHistoryService
{
    private const int MaxMessagesPerRoom = 100;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _history = new();
    private readonly object _lock = new();

    public void AddMessage(ChatMessage message)
    {
        lock (_lock)
        {
            var messages = _history.GetOrAdd(message.Room, _ => new List<ChatMessage>());
            messages.Add(message);
            if (messages.Count > MaxMessagesPerRoom)
            {
                messages.RemoveAt(0);
            }
        }
    }

    public IReadOnlyCollection<ChatMessage> GetRecentMessages(string room, int count = 50)
    {
        lock (_lock)
        {
            if (!_history.TryGetValue(room, out var messages))
            {
                return Array.Empty<ChatMessage>();
            }

            return messages
                .TakeLast(count)
                .ToList()
                .AsReadOnly();
        }
    }
}
