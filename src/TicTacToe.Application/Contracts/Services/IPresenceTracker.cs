namespace TicTacToe.Application.Contracts.Services;

/// <summary>Who is connected right now (single-node, in-memory; multi-tab connections count once per user).</summary>
public interface IPresenceTracker
{
    void TrackConnection(Guid userId, string connectionId);

    /// <summary>Removes one connection; true when the user's last connection went away.</summary>
    bool RemoveConnection(Guid userId, string connectionId);

    IReadOnlyList<Guid> GetOnlineUserIds();
}
