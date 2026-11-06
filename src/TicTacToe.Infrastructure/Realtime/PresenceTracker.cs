using System.Collections.Concurrent;
using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Infrastructure.Realtime;

/// <summary>Single-node in-memory presence (ADR-0008 shape): userId → connection ids; tabs count once.</summary>
public sealed class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    public void TrackConnection(Guid userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (set)
        {
            set.Add(connectionId);
        }
    }

    public bool RemoveConnection(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
        {
            return false;
        }

        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count > 0)
            {
                return false;
            }
        }

        _connections.TryRemove(userId, out _);
        return true;
    }

    public IReadOnlyList<Guid> GetOnlineUserIds() => [.. _connections.Keys];
}
