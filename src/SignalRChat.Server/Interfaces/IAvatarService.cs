namespace SignalRChat.Server.Interfaces;

/// <summary>
/// Generates avatar URLs for users.
/// Single responsibility: avatar logic only.
/// </summary>
public interface IAvatarService
{
    string GetAvatarUrl(string userName);
}
