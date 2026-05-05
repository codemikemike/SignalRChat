namespace SignalRChat.Server.Models;

/// <summary>
/// Represents a connected user in the chat.
/// SRP: only carries user-presence data.
/// </summary>
public record ConnectedUser(
    string ConnectionId,
    string UserName,
    string Avatar,
    string Room,
    DateTime ConnectedAt);
