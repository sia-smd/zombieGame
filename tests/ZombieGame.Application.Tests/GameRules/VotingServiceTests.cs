namespace ZombieGame.Application.Tests.GameRules;

using ZombieGame.Application.GameRules;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;

public class VotingServiceTests
{
    private readonly VotingService _voting = new();

    [Fact]
    public void ResolveElimination_MajorityEliminatesTarget()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var target = Guid.NewGuid();

        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true),
            (target, PlayerRole.Zombie, true));

        _voting.CastVote(state, p1, target);
        _voting.CastVote(state, p2, target);

        var eliminated = _voting.ResolveElimination(state);
        Assert.Equal(target, eliminated);
    }

    [Fact]
    public void ResolveElimination_TieEliminatesNobody()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var p4 = Guid.NewGuid();

        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true),
            (p3, PlayerRole.Human, true),
            (p4, PlayerRole.Human, true));

        _voting.CastVote(state, p1, p2);
        _voting.CastVote(state, p3, p2);
        _voting.CastVote(state, p2, p3);
        _voting.CastVote(state, p4, p3);

        var eliminated = _voting.ResolveElimination(state);
        Assert.Null(eliminated);
    }

    [Fact]
    public void AllAlivePlayersVoted_ReturnsTrueWhenComplete()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true));

        Assert.False(_voting.AllAlivePlayersVoted(state));

        _voting.CastVote(state, p1, p2);
        _voting.CastVote(state, p2, p1);

        Assert.True(_voting.AllAlivePlayersVoted(state));
    }
}
