namespace ZombieGame.Application.Room;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

/// <summary>Future: Spectator/Admin read-only access. Players use IRoomService.</summary>
public interface IRoomInspectorService
{
    Task<RoomState?> GetFullRoomStateAsync(Guid matchId, MatchConnectionRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchEventEntry>> GetEventLogAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetSummaryAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public sealed class RoomInspectorService : IRoomInspectorService
{
    private readonly IRoomStateStore _roomStore;
    private readonly IMatchEventLogStore _eventLog;
    private readonly IMatchSummaryStore _summary;

    public RoomInspectorService(
        IRoomStateStore roomStore,
        IMatchEventLogStore eventLog,
        IMatchSummaryStore summary)
    {
        _roomStore = roomStore;
        _eventLog = eventLog;
        _summary = summary;
    }

    public async Task<RoomState?> GetFullRoomStateAsync(Guid matchId, MatchConnectionRole role, CancellationToken cancellationToken = default)
    {
        if (role == MatchConnectionRole.Player)
            throw new UnauthorizedAccessException("Players must use scoped room DTOs.");

        return await _roomStore.GetAsync(matchId, cancellationToken);
    }

    public Task<IReadOnlyList<MatchEventEntry>> GetEventLogAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _eventLog.GetAsync(matchId, cancellationToken);

    public Task<MatchSummary?> GetSummaryAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        _summary.GetAsync(matchId, cancellationToken);
}
