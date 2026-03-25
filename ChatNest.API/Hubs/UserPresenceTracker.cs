using System.Collections.Concurrent;

namespace ChatNest.API.Hubs;

public interface IUserPresenceTracker
{
    bool AddConnection(string userId, string connectionId);
    bool RemoveConnection(string userId, string connectionId);
    bool IsOnline(string userId);
}

public sealed class UserPresenceTracker : IUserPresenceTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connections = new();

    public bool AddConnection(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
        {
            return false;
        }

        var userConnections = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        userConnections[connectionId] = 0;
        return userConnections.Count == 1;
    }

    public bool RemoveConnection(string userId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(connectionId))
        {
            return false;
        }

        if (!_connections.TryGetValue(userId, out var userConnections))
        {
            return false;
        }

        userConnections.TryRemove(connectionId, out _);
        if (!userConnections.IsEmpty)
        {
            return false;
        }

        _connections.TryRemove(userId, out _);
        return true;
    }

    public bool IsOnline(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId)
               && _connections.TryGetValue(userId, out var connections)
               && !connections.IsEmpty;
    }
}
