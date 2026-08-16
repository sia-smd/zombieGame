namespace ZombieGame.Application.Tests.Room;

using ZombieGame.Application.Room.Bots;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

public class RoomBotSchedulerTests
{
    [Fact]
    public void ScheduleInvitationAccept_SetsFutureRespondTime()
    {
        var invitation = new BattleInvitation { Id = Guid.NewGuid() };
        var settings = new Application.Options.RoomSettings
        {
            BotAcceptMinDelaySeconds = 2,
            BotAcceptMaxDelaySeconds = 2
        };

        var before = DateTime.UtcNow;
        RoomBotScheduler.ScheduleInvitationAccept(invitation, settings);

        Assert.NotNull(invitation.BotRespondAt);
        Assert.True(invitation.BotRespondAt >= before.AddSeconds(2));
        Assert.True(invitation.BotRespondAt <= before.AddSeconds(3));
    }

    [Fact]
    public void ScheduleOpponentSelectionBots_SchedulesUnpairedBotsOnly()
    {
        var bot = Guid.NewGuid();
        var human = Guid.NewGuid();
        var room = new RoomState
        {
            MatchId = Guid.NewGuid(),
            CurrentPhase = RoomPhase.OpponentSelection,
            Players =
            [
                new RoomPlayerState { UserId = bot, IsBot = true, IsAlive = true },
                new RoomPlayerState { UserId = human, IsBot = false, IsAlive = true }
            ],
            BattlePairs =
            [
                new BattlePair { Player1Id = human, Player2Id = Guid.NewGuid(), Status = BattlePairStatus.Pending }
            ]
        };

        RoomBotScheduler.ScheduleOpponentSelectionBots(room, new Application.Options.RoomSettings());

        Assert.NotNull(room.GetPlayer(bot)!.BotNextActionAt);
        Assert.Null(room.GetPlayer(human)!.BotNextActionAt);
    }

    [Fact]
    public void IsBotFinishedInPair_UsesBattleSessionFlags()
    {
        var bot = Guid.NewGuid();
        var pair = new BattlePair
        {
            Player1Id = bot,
            Player2Id = Guid.NewGuid(),
            BattleSession = new PairBattleSession { Player1Finished = true }
        };

        Assert.True(RoomBotScheduler.IsBotFinishedInPair(pair, bot));
    }
}
