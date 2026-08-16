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

    [Fact]
    public void CastVote_Abstain_CountsAsVoted_AndDoesNotEliminate()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true),
            (p3, PlayerRole.Zombie, true));

        _voting.CastVote(state, p1, Guid.Empty);
        _voting.CastVote(state, p2, p3);
        _voting.CastVote(state, p3, p3);

        Assert.True(_voting.AllAlivePlayersVoted(state));
        Assert.Equal(p3, _voting.ResolveElimination(state));
    }

    [Fact]
    public void ResolveElimination_AllAbstain_EliminatesNobody()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (p2, PlayerRole.Human, true));

        _voting.CastVote(state, p1, Guid.Empty);
        _voting.CastVote(state, p2, Guid.Empty);

        Assert.Null(_voting.ResolveElimination(state));
    }

    [Fact]
    public void CastVote_RejectsDeadTarget()
    {
        var p1 = Guid.NewGuid();
        var dead = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (p1, PlayerRole.Human, true),
            (dead, PlayerRole.Human, false));

        Assert.Throws<InvalidOperationException>(() => _voting.CastVote(state, p1, dead));
    }
}
