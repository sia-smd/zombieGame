namespace ZombieGame.Application.Tests.Room;

using ZombieGame.Application.Room;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public class InvitationCoordinatorTests
{
    [Fact]
    public void OnInvitationAccepted_CancelsConflictingInvitations()
    {
        var coordinator = new InvitationCoordinator();
        var room = new RoomState { MatchId = Guid.NewGuid(), DayNumber = 1, CurrentPhase = RoomPhase.OpponentSelection };

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        room.Players.AddRange([
            new RoomPlayerState { UserId = a, Username = "A", IsAlive = true, HasSentInvitationToday = true },
            new RoomPlayerState { UserId = b, Username = "B", IsAlive = true },
            new RoomPlayerState { UserId = c, Username = "C", IsAlive = true, HasSentInvitationToday = true }
        ]);

        var ab = new BattleInvitation { Id = Guid.NewGuid(), FromUserId = a, ToUserId = b };
        var cb = new BattleInvitation { Id = Guid.NewGuid(), FromUserId = c, ToUserId = b };
        room.PendingInvitations.AddRange([ab, cb]);

        var result = coordinator.OnInvitationAccepted(room, ab);

        Assert.Equal(BattleInvitationStatus.Accepted, result.Accepted.Status);
        Assert.Single(result.CancelledInvitations);
        Assert.Equal(cb.Id, result.CancelledInvitations[0].Id);
        Assert.False(room.GetPlayer(c)!.HasSentInvitationToday);
    }
}
