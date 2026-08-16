namespace ZombieGame.Domain.Interfaces;

using ZombieGame.Domain.Models.Room;

public interface IRoomStateStore
{
    Task<RoomState?> GetAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task SetAsync(RoomState state, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<bool> TryAddAsync(Guid matchId, RoomState state, CancellationToken cancellationToken = default);
}

/// <summary>Tracks prior opponents per player while the room is active.</summary>
public interface IBattlePairHistoryStore
{
    Task<IReadOnlySet<Guid>> GetPreviousOpponentsAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default);
    Task RecordPairAsync(Guid matchId, Guid player1Id, Guid player2Id, CancellationToken cancellationToken = default);
    Task ResetPlayerHistoryAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default);
    Task ResetAllAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public interface IDiscussionChatStore
{
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task AddMessageAsync(Guid matchId, ChatMessage message, CancellationToken cancellationToken = default);
    Task ClearAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public interface IRoomLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, TimeSpan ttl, CancellationToken cancellationToken = default);
}
