using SignalRChat.Server.Models;

namespace SignalRChat.Server.Interfaces;

/// <summary>
/// Tracks which users are connected and which rooms they're in.
/// Dependency Inversion: hubs depend on this abstraction, not on concrete storage.
/// </summary>
public interface IUserPresenceService
{
    void AddUser(ConnectedUser user);
    ConnectedUser? RemoveUser(string connectionId);
    ConnectedUser? GetUser(string connectionId);
    IReadOnlyCollection<ConnectedUser> GetUsersInRoom(string room);
    int GetTotalOnlineCount();
    IReadOnlyCollection<string> GetActiveRooms();
}
