namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public class MatchCompletionServiceTests
{
    [Fact]
    public async Task CompleteMatch_AwardsWinnersAndRecordsLosses()
    {
        var humanId = Guid.NewGuid();
        var zombieId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        var match = new Match
        {
            Id = matchId,
            Status = MatchStatus.InProgress,
            Players =
            [
                new MatchPlayer { UserId = humanId, IsBot = false },
                new MatchPlayer { UserId = zombieId, IsBot = false }
            ]
        };

        var state = GameTestBuilder.CreateSession(
            (humanId, PlayerRole.Human, true),
            (zombieId, PlayerRole.Zombie, true));

        var coinService = new FakeCoinService();
        var service = new MatchCompletionService(coinService, new FakeMatchRepository(match), new FakeUnitOfWork());
        await service.CompleteMatchAsync(match, state, WinTeam.Humans);

        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.NotNull(match.FinishedAt);
        Assert.Equal(WinTeam.Humans, state.WinTeam);
        Assert.Contains(humanId, coinService.Winners);
        Assert.Contains(zombieId, coinService.Losers);
    }
}
