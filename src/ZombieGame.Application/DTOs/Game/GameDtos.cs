namespace ZombieGame.Application.DTOs.Game;

using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public record GameStateDto(
    Guid MatchId,
    string SessionToken,
    GamePhase CurrentPhase,
    int TurnNumber,
    DateTime? PhaseEndsAt,
    IReadOnlyList<GamePlayerState> Players,
    IReadOnlyList<PlayerCardState> PlayerHands);

public record PlayCardRequest(Guid CardId, Guid? TargetUserId, string IdempotencyKey);

public record VotePlayerRequest(Guid TargetUserId, string IdempotencyKey);

public record EndTurnRequest(string IdempotencyKey);

public record PassActionRequest(string IdempotencyKey);

public record GameActionResult(bool Success, string Message, GameStateDto? State = null);

public static class GameStateMapper
{
    public static GameStateDto ToDto(GameSessionState state, bool includeHands = false)
    {
        return new GameStateDto(
            state.MatchId,
            state.SessionToken,
            state.CurrentPhase,
            state.TurnNumber,
            state.PhaseEndsAt,
            state.Players,
            includeHands ? state.PlayerHands : state.PlayerHands.Select(h => new PlayerCardState
            {
                UserId = h.UserId,
                RoleCardId = Guid.Empty,
                InventorySlot1 = null,
                InventorySlot2 = null
            }).ToList());
    }
}
