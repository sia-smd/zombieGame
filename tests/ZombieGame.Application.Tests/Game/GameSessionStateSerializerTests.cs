namespace ZombieGame.Application.Tests.Game;

using ZombieGame.Application.Game;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class GameSessionStateSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesCoreState()
    {
        var playerId = Guid.NewGuid();
        var state = new GameSessionState
        {
            MatchId = Guid.NewGuid(),
            SessionToken = "token-123",
            CurrentPhase = GamePhase.Day,
            TurnNumber = 2,
            CurrentDayEvent = DayEventType.Storm,
            Players =
            [
                new GamePlayerState
                {
                    UserId = playerId,
                    Username = "Guest123",
                    Role = PlayerRole.Human,
                    IsAlive = true,
                    ActionsPerTurn = 2
                }
            ],
            PlayerHands =
            [
                new PlayerCardState
                {
                    UserId = playerId,
                    CardIds = [Guid.NewGuid()],
                    DisabledCardIds = []
                }
            ],
            Metadata = new Dictionary<string, object> { ["dayEvent"] = "Storm" }
        };

        var json = GameSessionStateSerializer.Serialize(state);
        var restored = GameSessionStateSerializer.Deserialize(json);

        Assert.Equal(state.MatchId, restored.MatchId);
        Assert.Equal(state.CurrentPhase, restored.CurrentPhase);
        Assert.Equal(PlayerRole.Human, restored.Players[0].Role);
        Assert.Equal("Storm", restored.Metadata["dayEvent"].ToString());
    }
}
