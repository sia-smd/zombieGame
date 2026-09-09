namespace ZombieGame.Application.Tests.Room.Concurrency;

using ZombieGame.Application.Common;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Phases;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;
using ZombieGame.Infrastructure.Game;

[Collection("Concurrency")]
public class RoomLockConcurrencyTests
{
    [Theory]
    [InlineData(50)]
    public async Task ConcurrentTick_OnlyOneAdvances(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var matchId = Guid.NewGuid();
            var rooms = new CloningRoomStateStore();
            var battles = new CloningBattleStore();
            var handler = new TickAdvanceHandler(RoomPhase.Discussion, RoomPhase.Voting);
            var room = new RoomState
            {
                MatchId = matchId,
                CurrentPhase = RoomPhase.Discussion,
                Session = new GameSessionState { MatchId = matchId }
            };
            await rooms.SetAsync(room);

            var machine = ConcurrencyHarness.CreateMachine(
                rooms,
                battles,
                new InMemoryRoomLock(),
                [handler, new VotingPhaseHandler(), new FinishedPhaseHandler()]);

            await Task.WhenAll(machine.TickAsync(matchId), machine.TickAsync(matchId));

            var saved = await rooms.GetAsync(matchId);
            Assert.Equal(RoomPhase.Voting, saved!.CurrentPhase);
            Assert.Equal(1, handler.Ticks);
        }
    }

    [Theory]
    [InlineData(50)]
    public async Task ConcurrentStartGame_DayStartOnce(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var humanId = Guid.NewGuid();
            var matchId = Guid.NewGuid();
            var rooms = new CloningRoomStateStore();
            var coins = new FakeCoinService();
            var active = new InMemoryActiveMatchRegistry();
            var session = GameTestBuilder.CreateSession((humanId, PlayerRole.Human, true));
            session.MatchId = matchId;
            var room = new RoomState
            {
                MatchId = matchId,
                CurrentPhase = RoomPhase.Lobby,
                DayNumber = 0,
                Session = session,
                Players = [new RoomPlayerState { UserId = humanId, IsAlive = true, IsBot = false }]
            };
            await rooms.SetAsync(room);

            var machine = ConcurrencyHarness.CreateStartGameMachine(rooms, new InMemoryRoomLock(), coins, active);

            await Task.WhenAll(machine.StartGameAsync(matchId), machine.StartGameAsync(matchId));

            var saved = await rooms.GetAsync(matchId);
            Assert.Equal(RoomPhase.DayStart, saved!.CurrentPhase);
            Assert.Equal(1, saved.DayNumber);
            Assert.Single(coins.EntryFees);
        }
    }

    [Fact]
    public async Task RemovePlayer_WithoutLock_ThrowsBusy()
    {
        var matchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rooms = new CloningRoomStateStore();
        await rooms.SetAsync(new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.Lobby,
            Players = [new RoomPlayerState { UserId = userId, IsAlive = true }]
        });

        var machine = ConcurrencyHarness.CreateMachine(
            rooms,
            new CloningBattleStore(),
            new NeverRoomLock(),
            [new FinishedPhaseHandler()],
            roomSettings: new RoomSettings { LockWaitMilliseconds = 0, LockTtlSeconds = 30 });

        var ex = await Assert.ThrowsAsync<ServiceException>(() =>
            machine.RemovePlayerFromLobbyAsync(matchId, userId));
        Assert.Contains("busy", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single((await rooms.GetAsync(matchId))!.Players);
    }

    [Fact]
    public async Task SyncPlayers_SkipsWhenLockHeld()
    {
        var matchId = Guid.NewGuid();
        var existing = Guid.NewGuid();
        var extra = Guid.NewGuid();
        var rooms = new CloningRoomStateStore();
        await rooms.SetAsync(new RoomState
        {
            MatchId = matchId,
            CurrentPhase = RoomPhase.CardBattle,
            Players = [new RoomPlayerState { UserId = existing, IsAlive = true }],
            Session = new GameSessionState
            {
                MatchId = matchId,
                Players = [new GamePlayerState { UserId = existing, IsAlive = true }],
                PlayerHands = [new PlayerCardState { UserId = existing }]
            }
        });

        var match = new Match
        {
            Id = matchId,
            Players =
            [
                new MatchPlayer { UserId = existing, SeatIndex = 0 },
                new MatchPlayer { UserId = extra, SeatIndex = 1 }
            ]
        };

        var machine = ConcurrencyHarness.CreateMachine(
            rooms,
            new CloningBattleStore(),
            new NeverRoomLock(),
            [new FinishedPhaseHandler()],
            matches: new FakeMatchRepository(match),
            roomSettings: new RoomSettings { LockWaitMilliseconds = 0, LockTtlSeconds = 30 });

        await machine.SyncPlayersFromMatchAsync(matchId);

        var saved = await rooms.GetAsync(matchId);
        Assert.DoesNotContain(saved!.Players, p => p.UserId == extra);
    }

    [Theory]
    [InlineData(50)]
    public async Task DispatchAndTick_NoLostPlay(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var fxRooms = new CloningRoomStateStore();
            var fxBattles = new CloningBattleStore();
            var playerA = Guid.NewGuid();
            var playerB = Guid.NewGuid();
            var (room, battle) = ConcurrencyHarness.CreateInProgressBattle(playerA, playerB);
            GameTestBuilder.AddCardsToHand(room.Session, playerA, TestCards.Shotgun, TestCards.Pass);
            await fxRooms.SetAsync(room);
            await fxBattles.SaveAsync(battle);
            var machine = ConcurrencyHarness.CreateCardBattleMachine(fxRooms, fxBattles, new InMemoryRoomLock());

            var play = machine.DispatchAsync(
                room.MatchId,
                new BattlePlayCardCommand(playerA, battle.BattleId, TestCards.Shotgun.Id, playerB, 0));
            var tick = machine.TickAsync(room.MatchId);
            await Task.WhenAll(play, tick);

            var saved = await fxBattles.GetAsync(room.MatchId, battle.BattleId);
            Assert.Contains(saved!.CardsPlayed, c => c.PlayerId == playerA && c.CardId == TestCards.Shotgun.Id);
        }
    }
}
