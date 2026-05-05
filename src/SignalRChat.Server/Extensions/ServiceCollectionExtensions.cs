using SignalRChat.Server.Interfaces;
using SignalRChat.Server.Services;

namespace SignalRChat.Server.Extensions;

/// <summary>
/// Extension methods to keep Program.cs clean.
/// Adding a new service = one line here, not a change to Program.cs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddSingleton<IUserPresenceService, UserPresenceService>();
        services.AddSingleton<IAvatarService, AvatarService>();
        services.AddSingleton<IMessageHistoryService, MessageHistoryService>();
        return services;
    }
}
