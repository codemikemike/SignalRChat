namespace SignalRChat.Server.Models;

/// <summary>
/// Represents a chat message exchanged between users.
/// Immutable record - SRP: only carries message data.
/// </summary>
public record ChatMessage(
    string Id,
    string User,
    string Avatar,
    string Text,
    string Room,
    DateTime Timestamp,
    MessageType Type = MessageType.User);

public enum MessageType
{
    User,
    System,
    Private
}
