namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Models;

public interface IVotingService
{
    void CastVote(GameSessionState state, Guid voterId, Guid targetId);
    bool AllAlivePlayersVoted(GameSessionState state);
    Guid? ResolveElimination(GameSessionState state);
    void ClearVotes(GameSessionState state);
}

public sealed class VotingService : IVotingService
{
    public void CastVote(GameSessionState state, Guid voterId, Guid targetId)
    {
        var voter = state.GetPlayer(voterId)
            ?? throw new InvalidOperationException("Voter not found.");
        var target = state.GetPlayer(targetId)
            ?? throw new InvalidOperationException("Target not found.");

        if (!voter.IsAlive)
            throw new InvalidOperationException("Dead players cannot vote.");
        if (!target.IsAlive)
            throw new InvalidOperationException("Cannot vote for dead player.");

        state.Votes[voterId] = targetId;
    }

    public bool AllAlivePlayersVoted(GameSessionState state)
    {
        var alive = state.AlivePlayers.Select(p => p.UserId).ToHashSet();
        return alive.Count > 0 && alive.All(id => state.Votes.ContainsKey(id));
    }

    public Guid? ResolveElimination(GameSessionState state)
    {
        if (state.Votes.Count == 0) return null;

        var aliveVoterIds = state.AlivePlayers.Select(p => p.UserId).ToHashSet();
        var validVotes = state.Votes
            .Where(v => aliveVoterIds.Contains(v.Key))
            .GroupBy(v => v.Value)
            .Select(g => new { TargetId = g.Key, Count = g.Count() })
            .OrderByDescending(v => v.Count)
            .ToList();

        if (validVotes.Count == 0) return null;

        var top = validVotes[0];
        if (validVotes.Count > 1 && validVotes[1].Count == top.Count)
            return null;

        var required = (aliveVoterIds.Count / 2) + 1;
        return top.Count >= required ? top.TargetId : null;
    }

    public void ClearVotes(GameSessionState state) => state.Votes.Clear();
}
