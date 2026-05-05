using System.Collections.Concurrent;
using SignalRChat.Server.Interfaces;
using SignalRChat.Server.Models;

namespace SignalRChat.Server.Services;

/// <summary>
/// Thread-safe in-memory implementation of IUserPresenceService.
/// SRP: only manages presence state.
/// </summary>
public class UserPresenceService : IUserPresenceService
{
    private readonly ConcurrentDictionary<string, ConnectedUser> _users = new();

    public void AddUser(ConnectedUser user)
    {
        _users[user.ConnectionId] = user;
    }

    public ConnectedUser? RemoveUser(string connectionId)
    {
        _users.TryRemove(connectionId, out var user);
        return user;
    }

    public ConnectedUser? GetUser(string connectionId)
    {
        _users.TryGetValue(connectionId, out var user);
        return user;
    }

    public IReadOnlyCollection<ConnectedUser> GetUsersInRoom(string room)
    {
        return _users.Values
            .Where(u => string.Equals(u.Room, room, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public int GetTotalOnlineCount() => _users.Count;

    public IReadOnlyCollection<string> GetActiveRooms()
    {
        return _users.Values
            .Select(u => u.Room)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }
}
