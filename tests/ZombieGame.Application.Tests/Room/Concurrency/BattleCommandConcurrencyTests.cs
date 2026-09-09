namespace ZombieGame.Application.Tests.Room.Concurrency;

using ZombieGame.Application.Common;
using ZombieGame.Application.Room;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;
using ZombieGame.Infrastructure.Game;

[Collection("Concurrency")]
public class BattleCommandConcurrencyTests
{
    [Theory]
    [InlineData(100)]
    public async Task SamePlayer_TwoPlayCard_NeverExceedsActionsPerTurn(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync();
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerA, TestCards.Shotgun, TestCards.Heal);
            await fx.Rooms.SetAsync(fx.Room);

            var results = await Task.WhenAll(
                TryPlay(fx, fx.PlayerA, TestCards.Shotgun.Id, 0),
                TryPlay(fx, fx.PlayerA, TestCards.Heal.Id, 1));

            var savedBattle = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            var actor = (await fx.Machine.GetStateAsync(fx.Room.MatchId))!.Session.GetPlayer(fx.PlayerA)!;

            Assert.True(results.Count(r => r.Ok) >= 1, $"iteration {i}: {results[0].Error} / {results[1].Error}");
            Assert.True(actor.ActionsUsedThisTurn <= actor.ActionsPerTurn);
            Assert.Equal(savedBattle!.CardsPlayed.Count(c => c.PlayerId == fx.PlayerA), actor.ActionsUsedThisTurn);
            Assert.Equal(
                savedBattle.CardsPlayed.Select(c => c.InventorySlotIndex).Distinct().Count(),
                savedBattle.CardsPlayed.Count);
        }
    }

    [Theory]
    [InlineData(100)]
    public async Task SameSlot_ExactlyOneSucceeds(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync();
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerA, TestCards.Shotgun, TestCards.Pass);
            await fx.Rooms.SetAsync(fx.Room);

            var results = await Task.WhenAll(
                TryPlay(fx, fx.PlayerA, TestCards.Shotgun.Id, 0),
                TryPlay(fx, fx.PlayerA, TestCards.Shotgun.Id, 0));

            var saved = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            Assert.Equal(1, saved!.CardsPlayed.Count(c => c.PlayerId == fx.PlayerA && c.InventorySlotIndex == 0));
            Assert.Equal(1, results.Count(r => r.Ok));
        }
    }

    [Theory]
    [InlineData(100)]
    public async Task PlayCardAndFinishTurn_NoCardAfterFinished(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync();
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerA, TestCards.Shotgun, TestCards.Heal);
            await fx.Rooms.SetAsync(fx.Room);

            await Task.WhenAll(
                TryPlay(fx, fx.PlayerA, TestCards.Shotgun.Id, 0),
                TryFinish(fx, fx.PlayerA));

            var saved = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            Assert.True(saved!.PlayerAFinished);
            foreach (var play in saved.CardsPlayed.Where(c => c.PlayerId == fx.PlayerA))
                Assert.NotEqual(BattleAggregateStatus.Pending, saved.Status);
        }
    }

    [Theory]
    [InlineData(50)]
    public async Task FinishTurnTwice_IsIdempotent(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync(PlayerRole.Human, PlayerRole.Zombie);
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerB, TestCards.ZombiePoison, TestCards.Pass);
            fx.Battle.PlayerAFinished = true;
            await fx.Battles.SaveAsync(fx.Battle);
            await fx.Rooms.SetAsync(fx.Room);

            await fx.Machine.DispatchAsync(fx.Room.MatchId, new BattlePlayCardCommand(
                fx.PlayerB, fx.Battle.BattleId, TestCards.ZombiePoison.Id, fx.PlayerA, 0));

            var first = await TryFinish(fx, fx.PlayerB);
            var second = await TryFinish(fx, fx.PlayerB);

            Assert.True(first.Ok, first.Error);
            Assert.True(second.Ok);
            var saved = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            Assert.True(saved!.PlayerBFinished);
            Assert.True(saved.IsFinished);
            Assert.Equal(PlayerRole.Zombie, (await fx.Machine.GetStateAsync(fx.Room.MatchId))!.Session.GetPlayer(fx.PlayerA)!.Role);
        }
    }

    [Theory]
    [InlineData(100)]
    public async Task TwoPlayers_BothValidCardsSurvive(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync(PlayerRole.Human, PlayerRole.Zombie);
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerA, TestCards.Shotgun, TestCards.Pass);
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerB, TestCards.ZombiePoison, TestCards.Pass);
            await fx.Rooms.SetAsync(fx.Room);

            var results = await Task.WhenAll(
                TryPlay(fx, fx.PlayerA, TestCards.Shotgun.Id, 0),
                TryPlay(fx, fx.PlayerB, TestCards.ZombiePoison.Id, 0, fx.PlayerA));

            Assert.True(results.All(r => r.Ok), $"iteration {i}: {results[0].Error} / {results[1].Error}");
            var saved = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            Assert.Contains(saved!.CardsPlayed, c => c.PlayerId == fx.PlayerA && c.CardId == TestCards.Shotgun.Id);
            Assert.Contains(saved.CardsPlayed, c => c.PlayerId == fx.PlayerB && c.CardId == TestCards.ZombiePoison.Id);
        }
    }

    [Theory]
    [InlineData(50)]
    public async Task PlayCardAndExpiredTick_SingleConsistentOutcome(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync();
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerA, TestCards.Shotgun, TestCards.Pass);
            fx.Battle.BattleEndsAt = DateTime.UtcNow.AddMilliseconds(-1);
            fx.Room.BattlePairs[0].BattleEndsAt = fx.Battle.BattleEndsAt;
            await fx.Battles.SaveAsync(fx.Battle);
            await fx.Rooms.SetAsync(fx.Room);

            var play = TryPlay(fx, fx.PlayerA, TestCards.Shotgun.Id, 0);
            var tick = fx.Machine.TickAsync(fx.Room.MatchId);
            var playResult = await play;
            await tick;

            var saved = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            Assert.NotNull(saved);
            if (saved!.PlayerAFinished && !playResult.Ok)
                Assert.DoesNotContain(saved.CardsPlayed, c => c.PlayerId == fx.PlayerA && c.CardId == TestCards.Shotgun.Id);
        }
    }

    [Theory]
    [InlineData(50)]
    public async Task ConcurrentFinishBothPlayers_ResolvesOnce(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fx = await SeedAsync(PlayerRole.Human, PlayerRole.Zombie);
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerA, TestCards.Pass);
            GameTestBuilder.AddCardsToHand(fx.Room.Session, fx.PlayerB, TestCards.ZombiePoison, TestCards.Pass);
            await fx.Rooms.SetAsync(fx.Room);

            await fx.Machine.DispatchAsync(fx.Room.MatchId, new BattlePlayCardCommand(
                fx.PlayerB, fx.Battle.BattleId, TestCards.ZombiePoison.Id, fx.PlayerA, 0));

            await Task.WhenAll(TryFinish(fx, fx.PlayerA), TryFinish(fx, fx.PlayerB));

            var saved = await fx.Battles.GetAsync(fx.Room.MatchId, fx.Battle.BattleId);
            Assert.True(saved!.IsFinished);
            Assert.Equal(PlayerRole.Zombie, (await fx.Machine.GetStateAsync(fx.Room.MatchId))!.Session.GetPlayer(fx.PlayerA)!.Role);
        }
    }

    private static async Task<Fixture> SeedAsync(
        PlayerRole roleA = PlayerRole.Human,
        PlayerRole roleB = PlayerRole.Human)
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var rooms = new CloningRoomStateStore();
        var battles = new CloningBattleStore();
        var (room, battle) = ConcurrencyHarness.CreateInProgressBattle(playerA, playerB, roleA, roleB);
        await rooms.SetAsync(room);
        await battles.SaveAsync(battle);
        var machine = ConcurrencyHarness.CreateCardBattleMachine(rooms, battles, new InMemoryRoomLock());
        return new Fixture(machine, rooms, battles, room, battle, playerA, playerB);
    }

    private static async Task<(bool Ok, string? Error)> TryPlay(
        Fixture fx,
        Guid userId,
        Guid cardId,
        int slot,
        Guid? target = null)
    {
        try
        {
            var opponent = userId == fx.PlayerA ? fx.PlayerB : fx.PlayerA;
            await fx.Machine.DispatchAsync(
                fx.Room.MatchId,
                new BattlePlayCardCommand(userId, fx.Battle.BattleId, cardId, target ?? opponent, slot));
            return (true, null);
        }
        catch (ServiceException ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task<(bool Ok, string? Error)> TryFinish(Fixture fx, Guid userId)
    {
        try
        {
            await fx.Machine.DispatchAsync(fx.Room.MatchId, new BattleFinishTurnCommand(userId, fx.Battle.BattleId));
            return (true, null);
        }
        catch (ServiceException ex)
        {
            return (false, ex.Message);
        }
    }

    private sealed record Fixture(
        RoomStateMachine Machine,
        CloningRoomStateStore Rooms,
        CloningBattleStore Battles,
        RoomState Room,
        Battle Battle,
        Guid PlayerA,
        Guid PlayerB);
}
