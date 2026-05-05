namespace SignalRChat.Blazor.Models;

public record ChatMessage(
    string Id,
    string User,
    string Avatar,
    string Text,
    string Room,
    DateTime Timestamp,
    int Type);

public record ConnectedUserDto(string UserName, string Avatar);

public record RoomPresence(string Room, List<ConnectedUserDto> Users, int Count);

public record JoinConfirmation(string UserName, string Avatar, string Room, string ConnectionId);

public record UserEvent(string UserName, string Avatar, DateTime Timestamp);
