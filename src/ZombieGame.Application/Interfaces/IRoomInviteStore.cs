namespace ZombieGame.Application.Interfaces;

public enum RoomInviteStatus
{
    Pending = 0,
    Accepted = 1,
    Denied = 2
}

public sealed class RoomInvite
{
    public Guid Id { get; init; }
    public Guid MatchId { get; init; }
    public Guid FromUserId { get; init; }
    public Guid ToUserId { get; init; }
    public DateTime ExpiresAt { get; init; }
    public RoomInviteStatus Status { get; set; } = RoomInviteStatus.Pending;
}

public interface IRoomInviteStore
{
    void Upsert(RoomInvite invite);
    RoomInvite? Get(Guid inviteId);
    void Update(RoomInvite invite);
}
