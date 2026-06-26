namespace ZombieGame.Application.Game;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZombieGame.Application.DTOs.Game;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Interfaces;

public sealed class GameBotExecutor : IGameBotExecutor
{
    private readonly IGameService _gameService;
    private readonly IBotService _botService;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<GameBotExecutor> _logger;

    public GameBotExecutor(
        IGameService gameService,
        IBotService botService,
        IMatchRepository matchRepository,
        ILogger<GameBotExecutor> logger)
    {
        _gameService = gameService;
        _botService = botService;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    public async Task<GameActionResult?> ExecuteNextBotActionAsync(
        Guid matchId,
        Guid botUserId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _botService.DecideNextActionAsync(matchId, botUserId, cancellationToken);
        if (pending is null)
            return null;

        var match = await _matchRepository.GetByIdAsync(matchId, cancellationToken);
        if (match is null)
            return null;

        try
        {
            return pending.ActionType switch
            {
                "PlayCard" => await ExecutePlayCardAsync(matchId, match.SessionToken, botUserId, pending, cancellationToken),
                "Pass" => await _gameService.PassActionAsync(
                    botUserId,
                    matchId,
                    match.SessionToken,
                    new PassActionRequest(pending.IdempotencyKey),
                    cancellationToken),
                "VotePlayer" => await ExecuteVoteAsync(matchId, match.SessionToken, botUserId, pending, cancellationToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bot action failed for match {MatchId}, bot {BotUserId}", matchId, botUserId);
            return null;
        }
    }

    private async Task<GameActionResult> ExecutePlayCardAsync(
        Guid matchId,
        string sessionToken,
        Guid botUserId,
        PendingBotAction pending,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(pending.PayloadJson);
        var root = doc.RootElement;
        var cardId = root.GetProperty("CardId").GetGuid();
        Guid? targetUserId = root.TryGetProperty("TargetUserId", out var targetEl) && targetEl.ValueKind != JsonValueKind.Null
            ? targetEl.GetGuid()
            : null;

        return await _gameService.PlayCardAsync(
            botUserId,
            matchId,
            sessionToken,
            new PlayCardRequest(cardId, targetUserId, pending.IdempotencyKey),
            cancellationToken);
    }

    private async Task<GameActionResult> ExecuteVoteAsync(
        Guid matchId,
        string sessionToken,
        Guid botUserId,
        PendingBotAction pending,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(pending.PayloadJson);
        var targetUserId = doc.RootElement.GetProperty("TargetUserId").GetGuid();

        return await _gameService.VotePlayerAsync(
            botUserId,
            matchId,
            sessionToken,
            new VotePlayerRequest(targetUserId, pending.IdempotencyKey),
            cancellationToken);
    }
}
