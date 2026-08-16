namespace ZombieGame.Application.Interfaces;

/// <summary>Tracks users currently connected to a SignalR hub (online presence).</summary>
public interface IUserPresenceTracker
{
    void AddConnection(Guid userId, string connectionId);
    void RemoveConnection(Guid userId, string connectionId);
    bool IsOnline(Guid userId);
}
