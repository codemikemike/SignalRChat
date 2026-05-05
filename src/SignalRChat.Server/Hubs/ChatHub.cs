using Microsoft.AspNetCore.SignalR;
using SignalRChat.Server.Interfaces;
using SignalRChat.Server.Models;

namespace SignalRChat.Server.Hubs;

/// <summary>
/// SignalR Hub - thin orchestration layer.
/// All business logic lives in injected services (SRP/SoC).
/// </summary>
public class ChatHub : Hub
{
    private readonly IUserPresenceService _presence;
    private readonly IAvatarService _avatars;
    private readonly IMessageHistoryService _history;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IUserPresenceService presence,
        IAvatarService avatars,
        IMessageHistoryService history,
        ILogger<ChatHub> logger)
    {
        _presence = presence;
        _avatars = avatars;
        _history = history;
        _logger = logger;
    }

    // ---------- Lifecycle ----------

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Connection opened: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = _presence.RemoveUser(Context.ConnectionId);
        if (user is not null)
        {
            await Clients.Group(user.Room).SendAsync("UserLeft", new
            {
                user.UserName,
                user.Avatar,
                Timestamp = DateTime.UtcNow
            });

            await BroadcastRoomPresence(user.Room);
            await BroadcastGlobalOnlineCount();

            _logger.LogInformation("{User} disconnected from {Room}", user.UserName, user.Room);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ---------- Room management ----------

    public async Task JoinRoom(string userName, string room)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(room))
        {
            await Clients.Caller.SendAsync("Error", "Username and room are required.");
            return;
        }

        userName = userName.Trim();
        room = room.Trim();

        // If user was already in another room, remove first
        var existing = _presence.GetUser(Context.ConnectionId);
        if (existing is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, existing.Room);
            await Clients.Group(existing.Room).SendAsync("UserLeft", new
            {
                existing.UserName,
                existing.Avatar,
                Timestamp = DateTime.UtcNow
            });
            _presence.RemoveUser(Context.ConnectionId);
            await BroadcastRoomPresence(existing.Room);
        }

        var avatar = _avatars.GetAvatarUrl(userName);
        var user = new ConnectedUser(Context.ConnectionId, userName, avatar, room, DateTime.UtcNow);
        _presence.AddUser(user);

        await Groups.AddToGroupAsync(Context.ConnectionId, room);

        // Send recent history to the joiner only
        var recent = _history.GetRecentMessages(room);
        await Clients.Caller.SendAsync("MessageHistory", recent);

        // Tell everyone in the room
        await Clients.Group(room).SendAsync("UserJoined", new
        {
            UserName = userName,
            Avatar = avatar,
            Timestamp = DateTime.UtcNow
        });

        // Caller-only confirmation with own identity
        await Clients.Caller.SendAsync("JoinConfirmed", new
        {
            UserName = userName,
            Avatar = avatar,
            Room = room,
            ConnectionId = Context.ConnectionId
        });

        await BroadcastRoomPresence(room);
        await BroadcastGlobalOnlineCount();
    }

    public async Task LeaveRoom()
    {
        var user = _presence.RemoveUser(Context.ConnectionId);
        if (user is null) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, user.Room);
        await Clients.Group(user.Room).SendAsync("UserLeft", new
        {
            user.UserName,
            user.Avatar,
            Timestamp = DateTime.UtcNow
        });

        await BroadcastRoomPresence(user.Room);
        await BroadcastGlobalOnlineCount();
    }

    // ---------- Messaging ----------

    public async Task SendMessage(string text)
    {
        var user = _presence.GetUser(Context.ConnectionId);
        if (user is null)
        {
            await Clients.Caller.SendAsync("Error", "You must join a room first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        var message = new ChatMessage(
            Id: Guid.NewGuid().ToString(),
            User: user.UserName,
            Avatar: user.Avatar,
            Text: text.Trim(),
            Room: user.Room,
            Timestamp: DateTime.UtcNow,
            Type: MessageType.User);

        _history.AddMessage(message);
        await Clients.Group(user.Room).SendAsync("ReceiveMessage", message);
    }

    public async Task SendPrivateMessage(string targetUserName, string text)
    {
        var sender = _presence.GetUser(Context.ConnectionId);
        if (sender is null || string.IsNullOrWhiteSpace(text)) return;

        // Find target across rooms
        var target = _presence.GetUsersInRoom(sender.Room)
            .FirstOrDefault(u => string.Equals(u.UserName, targetUserName, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            await Clients.Caller.SendAsync("Error", $"User '{targetUserName}' not found in this room.");
            return;
        }

        var message = new ChatMessage(
            Id: Guid.NewGuid().ToString(),
            User: sender.UserName,
            Avatar: sender.Avatar,
            Text: text.Trim(),
            Room: sender.Room,
            Timestamp: DateTime.UtcNow,
            Type: MessageType.Private);

        // Send to both target and sender so both see the conversation
        await Clients.Client(target.ConnectionId).SendAsync("ReceivePrivateMessage", message);
        await Clients.Caller.SendAsync("ReceivePrivateMessage", message);
    }

    // ---------- Typing indicator ----------

    public async Task NotifyTyping(bool isTyping)
    {
        var user = _presence.GetUser(Context.ConnectionId);
        if (user is null) return;

        await Clients.OthersInGroup(user.Room).SendAsync("UserTyping", new
        {
            user.UserName,
            user.Avatar,
            IsTyping = isTyping
        });
    }

    // ---------- Helpers ----------

    private async Task BroadcastRoomPresence(string room)
    {
        var users = _presence.GetUsersInRoom(room)
            .Select(u => new { u.UserName, u.Avatar })
            .ToList();

        await Clients.Group(room).SendAsync("RoomPresenceUpdated", new
        {
            Room = room,
            Users = users,
            Count = users.Count
        });
    }

    private async Task BroadcastGlobalOnlineCount()
    {
        await Clients.All.SendAsync("GlobalOnlineCountUpdated", _presence.GetTotalOnlineCount());
    }
}
