using Microsoft.AspNetCore.SignalR.Client;
using SignalRChat.Blazor.Models;

namespace SignalRChat.Blazor.Services;

public class ChatClientService : IChatClientService
{
    private readonly HubConnection _connection;

    public HubConnection Connection => _connection;
    public HubConnectionState State => _connection.State;

    public event Action<ChatMessage>? OnMessageReceived;
    public event Action<ChatMessage>? OnPrivateMessageReceived;
    public event Action<List<ChatMessage>>? OnHistoryReceived;
    public event Action<UserEvent>? OnUserJoined;
    public event Action<UserEvent>? OnUserLeft;
    public event Action<RoomPresence>? OnPresenceUpdated;
    public event Action<int>? OnGlobalCountUpdated;
    public event Action<JoinConfirmation>? OnJoinConfirmed;
    public event Action<string>? OnConnectionStateChanged;
    public event Action<string>? OnError;

    public ChatClientService(IConfiguration config)
    {
        var hubUrl = config["ChatHubUrl"] ?? "http://localhost:5050/chathub";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            })
            .Build();

        WireServerEvents();
        WireConnectionState();
    }

    private void WireServerEvents()
    {
        _connection.On<ChatMessage>("ReceiveMessage", m => OnMessageReceived?.Invoke(m));
        _connection.On<ChatMessage>("ReceivePrivateMessage", m => OnPrivateMessageReceived?.Invoke(m));
        _connection.On<List<ChatMessage>>("MessageHistory", h => OnHistoryReceived?.Invoke(h));
        _connection.On<UserEvent>("UserJoined", e => OnUserJoined?.Invoke(e));
        _connection.On<UserEvent>("UserLeft", e => OnUserLeft?.Invoke(e));
        _connection.On<RoomPresence>("RoomPresenceUpdated", p => OnPresenceUpdated?.Invoke(p));
        _connection.On<int>("GlobalOnlineCountUpdated", c => OnGlobalCountUpdated?.Invoke(c));
        _connection.On<JoinConfirmation>("JoinConfirmed", j => OnJoinConfirmed?.Invoke(j));
        _connection.On<string>("Error", msg => OnError?.Invoke(msg));
    }

    private void WireConnectionState()
    {
        _connection.Reconnecting += _ =>
        {
            OnConnectionStateChanged?.Invoke("reconnecting");
            return Task.CompletedTask;
        };
        _connection.Reconnected += _ =>
        {
            OnConnectionStateChanged?.Invoke("connected");
            return Task.CompletedTask;
        };
        _connection.Closed += _ =>
        {
            OnConnectionStateChanged?.Invoke("disconnected");
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync()
    {
        OnConnectionStateChanged?.Invoke("connecting");
        await _connection.StartAsync();
        OnConnectionStateChanged?.Invoke("connected");
    }

    public Task JoinRoomAsync(string userName, string room)
        => _connection.InvokeAsync("JoinRoom", userName, room);

    public Task LeaveRoomAsync()
        => _connection.InvokeAsync("LeaveRoom");

    public Task SendMessageAsync(string text)
        => _connection.InvokeAsync("SendMessage", text);

    public Task SendPrivateMessageAsync(string targetUserName, string text)
        => _connection.InvokeAsync("SendPrivateMessage", targetUserName, text);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
