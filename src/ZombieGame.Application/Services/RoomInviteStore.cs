namespace ZombieGame.Application.Services;

using System.Collections.Concurrent;
using ZombieGame.Application.Interfaces;

public sealed class RoomInviteStore : IRoomInviteStore
{
    private readonly ConcurrentDictionary<Guid, RoomInvite> _invites = new();

    public void Upsert(RoomInvite invite) => _invites[invite.Id] = invite;

    public RoomInvite? Get(Guid inviteId) =>
        _invites.TryGetValue(inviteId, out var invite) ? invite : null;

    public void Update(RoomInvite invite) => _invites[invite.Id] = invite;
}
