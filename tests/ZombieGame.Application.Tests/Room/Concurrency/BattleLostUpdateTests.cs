namespace ZombieGame.Application.Tests.Room.Concurrency;

using ZombieGame.Application.Common;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models.Room;

/// <summary>
/// Proves BattleStore last-write-wins when BattleService is called without IRoomLock.
/// Production must only call BattleService while holding the room lock.
/// </summary>
[CollectionDefinition("Concurrency", DisableParallelization = true)]
public sealed class ConcurrencyCollection { }

[Collection("Concurrency")]
public class BattleLostUpdateTests
{
    [Fact]
    public async Task OverlappingGetThenSave_LosesTheFirstPlayersCard()
    {
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var inner = new CloningBattleStore();
        var store = new GatedBattleStore(inner, participants: 2);
        var (room, battle) = ConcurrencyHarness.CreateInProgressBattle(playerA, playerB);
        GameTestBuilder.AddCardsToHand(room.Session, playerA, TestCards.Shotgun, TestCards.Heal);
        await inner.SaveAsync(battle);

        var service = ConcurrencyHarness.CreateBattleService(store);

        var task1 = service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.Shotgun.Id, playerB, 0);
        var task2 = service.PlayCardAsync(room, battle.BattleId, playerA, TestCards.Heal.Id, playerB, 1);
        await Task.WhenAll(task1, task2);

        var saved = await inner.GetAsync(room.MatchId, battle.BattleId);
        Assert.NotNull(saved);
        Assert.True(
            saved!.CardsPlayed.Count < 2,
            "Without a room lock, overlapping PlayCard Get/Save loses a queued card (Redis last-write-wins).");
    }
}
