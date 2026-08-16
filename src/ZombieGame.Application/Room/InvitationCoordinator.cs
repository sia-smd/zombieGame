namespace ZombieGame.Application.Room;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

/// <summary>Atomic invitation conflict resolution under room lock.</summary>
public sealed class InvitationCoordinator
{
    public InvitationAcceptResult OnInvitationAccepted(RoomState room, BattleInvitation accepted)
    {
        if (room.IsPaired(accepted.FromUserId) || room.IsPaired(accepted.ToUserId))
            throw new InvalidOperationException("A player is already paired.");

        accepted.Status = BattleInvitationStatus.Accepted;

        var cancelled = new List<BattleInvitation>();
        foreach (var invitation in room.PendingInvitations.Where(i => i.Status == BattleInvitationStatus.Pending).ToList())
        {
            if (invitation.Id == accepted.Id)
                continue;

            var involvesAcceptedPlayers =
                invitation.FromUserId == accepted.FromUserId ||
                invitation.FromUserId == accepted.ToUserId ||
                invitation.ToUserId == accepted.FromUserId ||
                invitation.ToUserId == accepted.ToUserId;

            if (!involvesAcceptedPlayers)
                continue;

            invitation.Status = BattleInvitationStatus.Expired;
            cancelled.Add(invitation);

            var sender = room.GetPlayer(invitation.FromUserId);
            if (sender is not null)
                sender.HasSentInvitationToday = false;
        }

        return new InvitationAcceptResult(accepted, cancelled);
    }
}

public sealed record InvitationAcceptResult(
    BattleInvitation Accepted,
    IReadOnlyList<BattleInvitation> CancelledInvitations);

public sealed record InvitationsCancelledEvent(IReadOnlyList<BattleInvitation> Cancelled) : RoomEvent;
