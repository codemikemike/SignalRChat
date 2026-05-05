using SignalRChat.Server.Interfaces;

namespace SignalRChat.Server.Services;

/// <summary>
/// Generates deterministic avatar URLs using DiceBear's free API.
/// Same username = same avatar = persistent identity.
/// </summary>
public class AvatarService : IAvatarService
{
    private const string BaseUrl = "https://api.dicebear.com/7.x/bottts-neutral/svg";

    public string GetAvatarUrl(string userName)
    {
        var seed = Uri.EscapeDataString(userName.Trim().ToLowerInvariant());
        return $"{BaseUrl}?seed={seed}&backgroundColor=5865f2,57f287,fee75c,eb459e,ed4245";
    }
}
