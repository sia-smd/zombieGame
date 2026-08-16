namespace ZombieGame.Application.Tests.Room;

using ZombieGame.Application.Room;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models.Room;

public class OpponentSelectionServiceTests
{
    [Fact]
    public async Task GetAvailableOpponents_ExcludesPreviousOpponents()
    {
        var service = new OpponentSelectionService();
        var history = new FakeHistoryStore();
        var room = BuildRoom(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3"));

        await history.RecordPairAsync(room.MatchId, room.Players[0].UserId, room.Players[1].UserId);

        var available = await service.GetAvailableOpponentsAsync(
            room,
            room.Players[0].UserId,
            history);

        Assert.DoesNotContain(room.Players[1].UserId, available);
        Assert.Contains(room.Players[2].UserId, available);
    }

    [Fact]
    public async Task GetAvailableOpponents_ResetsWhenAllOpponentsExhausted()
    {
        var service = new OpponentSelectionService();
        var history = new FakeHistoryStore();
        var p1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var p2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var room = BuildRoom(p1, p2);

        await history.RecordPairAsync(room.MatchId, p1, p2);

        var available = await service.GetAvailableOpponentsAsync(room, p1, history);

        Assert.Contains(p2, available);
    }

    private static RoomState BuildRoom(params Guid[] playerIds)
    {
        var room = new RoomState
        {
            MatchId = Guid.NewGuid(),
            CurrentPhase = RoomPhase.OpponentSelection,
            DayNumber = 1
        };

        for (var i = 0; i < playerIds.Length; i++)
        {
            room.Players.Add(new RoomPlayerState
            {
                UserId = playerIds[i],
                Username = $"P{i}",
                IsAlive = true,
                SeatIndex = i
            });
        }

        return room;
    }

    private sealed class FakeHistoryStore : IBattlePairHistoryStore
    {
        private readonly Dictionary<(Guid, Guid), HashSet<Guid>> _data = new();

        public Task<IReadOnlySet<Guid>> GetPreviousOpponentsAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default)
        {
            _data.TryGetValue((matchId, playerId), out var set);
            return Task.FromResult<IReadOnlySet<Guid>>(set ?? new HashSet<Guid>());
        }

        public Task RecordPairAsync(Guid matchId, Guid player1Id, Guid player2Id, CancellationToken cancellationToken = default)
        {
            Add(matchId, player1Id, player2Id);
            Add(matchId, player2Id, player1Id);
            return Task.CompletedTask;
        }

        public Task ResetPlayerHistoryAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken = default)
        {
            _data.Remove((matchId, playerId));
            return Task.CompletedTask;
        }

        public Task ResetAllAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveRoomAsync(Guid matchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private void Add(Guid matchId, Guid playerId, Guid opponentId)
        {
            var set = _data.GetValueOrDefault((matchId, playerId)) ?? new HashSet<Guid>();
            set.Add(opponentId);
            _data[(matchId, playerId)] = set;
        }
    }
}
