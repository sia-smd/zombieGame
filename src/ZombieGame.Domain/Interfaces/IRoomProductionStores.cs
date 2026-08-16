namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Models.Room;

public interface IBattleStore
{
    Task<Battle?> GetAsync(Guid matchId, Guid battleId, CancellationToken cancellationToken = default);
    Task SaveAsync(Battle battle, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Battle>> GetByMatchAndDayAsync(Guid matchId, int dayNumber, CancellationToken cancellationToken = default);
    Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public interface IPlayerActiveMatchStore
{
    Task<PlayerActiveMatch?> GetAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task SetAsync(PlayerActiveMatch entry, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task TouchAsync(Guid playerId, CancellationToken cancellationToken = default);
}

public interface IRoomSnapshotStore
{
    Task AppendAsync(RoomSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<RoomSnapshot?> GetLatestAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomSnapshot>> GetAllAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public interface IMatchEventLogStore
{
    Task AppendAsync(MatchEventEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchEventEntry>> GetAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task RemoveMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public interface IMatchSummaryStore
{
    Task SaveAsync(MatchSummary summary, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetAsync(Guid matchId, CancellationToken cancellationToken = default);
}
