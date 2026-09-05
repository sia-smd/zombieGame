namespace ZombieGame.Application.Tests.Room;

using Microsoft.Extensions.Options;
using ZombieGame.Application.DTOs.Room;
using ZombieGame.Application.GameRules;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Application.Room;
using ZombieGame.Application.Room.Phases;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public class VoteResultPreviewTests
{
    [Fact]
    public async Task OnEnter_SetsNextDayPreview_WhenGameContinues()
    {
        var handler = CreateHandler();
        var room = BuildVotingRoom(dayNumber: 2);
        var context = CreateContext(room);

        await handler.OnEnterAsync(context);

        Assert.Equal(3, room.NextDayNumber);
        Assert.NotNull(room.NextDayEvent);
        Assert.NotNull(room.PhaseEndsAt);
    }

    [Fact]
    public void Mapper_RevealsRoles_WhenMatchFinished()
    {
        var room = BuildVotingRoom(dayNumber: 3);
        room.CurrentPhase = RoomPhase.Finished;
        room.WinTeam = WinTeam.Humans;

        var dto = RoomStateMapper.ToPublicDto(room, room.Players[0].UserId);

        Assert.All(dto.Players, p => Assert.NotNull(p.Role));
        Assert.Contains(dto.Players, p => p.Role == PlayerRole.Human);
        Assert.Contains(dto.Players, p => p.Role == PlayerRole.Zombie);
    }

    private static VoteResultPhaseHandler CreateHandler()
    {
        var dayEvents = new DayEventService(Options.Create(new DayEventOptions()));
        return new VoteResultPhaseHandler(
            new VotingService(),
            new WinConditionService(),
            dayEvents);
    }

    private static RoomContext CreateContext(RoomState room) =>
        new()
        {
            Room = room,
            Settings = new RoomSettings { VoteResultDisplaySeconds = 10 },
            History = null!,
            Chat = null!
        };

    private static RoomState BuildVotingRoom(int dayNumber)
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var room = new RoomState
        {
            MatchId = Guid.NewGuid(),
            DayNumber = dayNumber,
            CurrentPhase = RoomPhase.VoteResult,
        };

        room.Players.Add(new RoomPlayerState { UserId = humanId, Username = "Human", SeatIndex = 0, IsAlive = true });
        room.Players.Add(new RoomPlayerState { UserId = zombieId, Username = "Zombie", SeatIndex = 1, IsAlive = true });
        room.Session.Players.Add(new GamePlayerState { UserId = humanId, Username = "Human", Role = PlayerRole.Human, IsAlive = true });
        room.Session.Players.Add(new GamePlayerState { UserId = zombieId, Username = "Zombie", Role = PlayerRole.Zombie, IsAlive = true });
        room.Votes[humanId] = zombieId;
        room.Votes[zombieId] = humanId;

        return room;
    }
}
