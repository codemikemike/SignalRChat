using Microsoft.AspNetCore.SignalR.Client;
using SignalRChat.Blazor.Models;

namespace SignalRChat.Blazor.Services;

/// <summary>
/// Abstraction over SignalR client. Components depend on this, not on HubConnection directly.
/// </summary>
public interface IChatClientService : IAsyncDisposable
{
    HubConnectionState State { get; }

    event Action<ChatMessage>? OnMessageReceived;
    event Action<ChatMessage>? OnPrivateMessageReceived;
    event Action<List<ChatMessage>>? OnHistoryReceived;
    event Action<UserEvent>? OnUserJoined;
    event Action<UserEvent>? OnUserLeft;
    event Action<RoomPresence>? OnPresenceUpdated;
    event Action<int>? OnGlobalCountUpdated;
    event Action<JoinConfirmation>? OnJoinConfirmed;
    event Action<string>? OnConnectionStateChanged;
    event Action<string>? OnError;

    Task StartAsync();
    Task JoinRoomAsync(string userName, string room);
    Task LeaveRoomAsync();
    Task SendMessageAsync(string text);
    Task SendPrivateMessageAsync(string targetUserName, string text);
}
