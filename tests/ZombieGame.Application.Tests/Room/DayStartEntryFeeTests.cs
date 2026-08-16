namespace ZombieGame.Application.Tests.Room;

using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Phases;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public class DayStartEntryFeeTests
{
    [Fact]
    public async Task DayOne_ChargesHumansAfterRoles_NotBots()
    {
        var humanId = Guid.NewGuid();
        var botId = Guid.NewGuid();
        var coins = new FakeCoinService();
        var handler = CreateHandler(coins);
        var context = CreateContext(dayNumber: 1, humanId, botId);

        await handler.OnEnterAsync(context);

        Assert.Contains(coins.EntryFees, f => f.UserId == humanId && f.MatchId == context.Room.MatchId);
        Assert.DoesNotContain(coins.EntryFees, f => f.UserId == botId);
        Assert.Single(coins.EntryFees);
    }

    [Fact]
    public async Task LaterDays_DoNotChargeAgain()
    {
        var coins = new FakeCoinService();
        var handler = CreateHandler(coins);
        var context = CreateContext(dayNumber: 2, Guid.NewGuid(), Guid.NewGuid());

        await handler.OnEnterAsync(context);

        Assert.Empty(coins.EntryFees);
    }

    private static DayStartPhaseHandler CreateHandler(FakeCoinService coins) =>
        new(
            new NoOpRoles(),
            new NoOpDealing(),
            GameTestBuilder.CreateDayEventService(),
            new WinConditionService(),
            coins,
            Options.Create(new GameSettings()));

    private static RoomContext CreateContext(int dayNumber, Guid humanId, Guid botId)
    {
        var session = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (botId, PlayerRole.Human, true));
        session.Players.First(p => p.UserId == botId).IsBot = true;

        var room = new RoomState
        {
            MatchId = session.MatchId,
            DayNumber = dayNumber,
            CurrentPhase = RoomPhase.DayStart,
            Session = session,
            Players =
            [
                new RoomPlayerState { UserId = humanId, IsBot = false, IsAlive = true },
                new RoomPlayerState { UserId = botId, IsBot = true, IsAlive = true }
            ]
        };

        return new RoomContext
        {
            Room = room,
            Settings = new RoomSettings { DayStartSeconds = 1 },
            History = new NoOpHistoryStore(),
            Chat = new NoOpChatStore()
        };
    }

    private sealed class NoOpRoles : IRoleAssignmentService
    {
        public void AssignRoles(GameSessionState state, int playerCount, RoleComposition? composition = null)
        {
        }
    }

    private sealed class NoOpDealing : ICardDealingService
    {
        public void InitializeHands(GameSessionState state)
        {
        }

        public void ReplenishInventory(GameSessionState state)
        {
        }
    }
}
