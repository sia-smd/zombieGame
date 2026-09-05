namespace ZombieGame.Application.Tests.Room;

using ZombieGame.Application.DTOs.Room;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Models.Room;

public class RoomMoodTests
{
    [Fact]
    public void ComputeRoomMood_GameStart_HumanMajority_IsSafe()
    {
        var room = BuildRoom(RoomPhase.CardBattle, 1,
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, true));

        Assert.Equal(RoomMood.Safe, RoomStateMapper.ComputeRoomMood(room));
    }

    [Fact]
    public void ComputeRoomMood_DoesNotResetAfterDayRollover_WhenPopulationUnchanged()
    {
        var room = BuildRoom(RoomPhase.DaySummary, 3,
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, true));

        room.DaySummary.NewlyInfectedPlayerIds.Clear();
        room.DaySummary.EliminatedPlayerIds.Clear();

        Assert.Equal(RoomMood.Critical, RoomStateMapper.ComputeRoomMood(room));
    }

    [Fact]
    public void ComputeRoomMood_InfectedEqualToHumans_IsCritical()
    {
        var room = BuildRoom(RoomPhase.Voting, 2,
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, true));

        Assert.Equal(RoomMood.Critical, RoomStateMapper.ComputeRoomMood(room));
    }

    [Fact]
    public void ComputeRoomMood_ShrinkingHumanLead_IsSuspicious()
    {
        var room = BuildRoom(RoomPhase.CardBattle, 2,
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, false));

        Assert.Equal(RoomMood.Suspicious, RoomStateMapper.ComputeRoomMood(room));
    }

    [Fact]
    public void ComputeRoomMood_HeavyAttrition_IsSuspicious()
    {
        var room = BuildRoom(RoomPhase.Discussion, 4,
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, true),
            (PlayerRole.Human, false),
            (PlayerRole.Zombie, true),
            (PlayerRole.Zombie, false),
            (PlayerRole.Zombie, false));

        Assert.Equal(RoomMood.Suspicious, RoomStateMapper.ComputeRoomMood(room));
    }

    [Fact]
    public void ComputeRoomMood_Lobby_IsSafe()
    {
        var room = BuildRoom(RoomPhase.Lobby, 0,
            (PlayerRole.Human, true),
            (PlayerRole.Zombie, true));

        Assert.Equal(RoomMood.Safe, RoomStateMapper.ComputeRoomMood(room));
    }

    private static RoomState BuildRoom(
        RoomPhase phase,
        int dayNumber,
        params (PlayerRole Role, bool Alive)[] players)
    {
        var room = new RoomState
        {
            MatchId = Guid.NewGuid(),
            CurrentPhase = phase,
            DayNumber = dayNumber,
        };

        foreach (var (role, alive) in players)
        {
            var userId = Guid.NewGuid();
            room.Players.Add(new RoomPlayerState
            {
                UserId = userId,
                Username = userId.ToString()[..8],
                IsAlive = alive,
            });
            room.Session.Players.Add(new GamePlayerState
            {
                UserId = userId,
                Role = role,
                IsAlive = alive,
            });
        }

        return room;
    }
}
