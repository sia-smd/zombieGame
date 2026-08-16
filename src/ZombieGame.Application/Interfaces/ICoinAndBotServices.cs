namespace ZombieGame.Application.Interfaces;

using ZombieGame.Domain.Models;

public interface ICoinService
{
    Task<bool> CanAffordEntryFeeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeductEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task AwardWinRewardAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task RecordLossAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RefundEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges humans once the match has actually started (roles assigned).
    /// Join/create/queue only check affordability so leaving a waiting room needs no refund.
    /// </summary>
    async Task CollectMatchEntryFeesAsync(
        Guid matchId,
        IEnumerable<Guid> humanUserIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in humanUserIds.Distinct())
            await DeductEntryFeeAsync(userId, matchId, cancellationToken);
    }
}

public interface IBotService
{
    Task FillMatchWithBotsAsync(Guid matchId, int targetCount, CancellationToken cancellationToken = default);
    Task<PendingBotAction?> DecideNextActionAsync(Guid matchId, Guid botUserId, CancellationToken cancellationToken = default);
    BotCardPlayDecision? TryDecideCardPlay(GameSessionState state, Guid botUserId, Guid? battleOpponentId = null);
    Guid TryDecideVoteTarget(GameSessionState state, Guid botUserId);
    bool ShouldSpeakInDiscussion(GameSessionState state, Guid botUserId);
    StructuredDiscussionMessage? TryDecideDiscussionMessage(GameSessionState state, Guid botUserId);
    void RecordDiscussionSpoke(GameSessionState state, Guid botUserId);
}

public record PendingBotAction(string ActionType, string PayloadJson, string IdempotencyKey);
public sealed record BotCardPlayDecision(Guid CardId, Guid? TargetUserId);
