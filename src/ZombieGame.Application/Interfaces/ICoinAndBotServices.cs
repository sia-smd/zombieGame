namespace ZombieGame.Application.Interfaces;

using ZombieGame.Domain.Enums;

public interface ICoinService
{
    Task<bool> CanAffordEntryFeeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeductEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task AwardWinRewardAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
    Task RecordLossAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RefundEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default);
}

public interface IBotService
{
    Task FillMatchWithBotsAsync(Guid matchId, int targetCount, CancellationToken cancellationToken = default);
    Task<PendingBotAction?> DecideNextActionAsync(Guid matchId, Guid botUserId, CancellationToken cancellationToken = default);
}

public record PendingBotAction(string ActionType, string PayloadJson, string IdempotencyKey);
