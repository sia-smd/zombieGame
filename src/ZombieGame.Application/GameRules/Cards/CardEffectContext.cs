namespace ZombieGame.Application.GameRules.Cards;

using ZombieGame.Application.Common;
using ZombieGame.Application.GameRules;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Models;

public sealed class CardEffectContext
{
    public required GameSessionState State { get; init; }
    public required Guid ActorUserId { get; init; }
    public required Guid TargetUserId { get; init; }
    public required CardDefinition Card { get; init; }

    public GamePlayerState Actor => State.GetPlayer(ActorUserId)
        ?? throw new ServiceException("Actor not found.");

    public GamePlayerState Target => State.GetPlayer(TargetUserId)
        ?? throw new ServiceException("Target not found.");
}

public sealed class CardEffectResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool FriendlyFire { get; init; }
    public bool TargetKilled { get; init; }
    public bool RoleChanged { get; init; }
    public CardEffectTelemetryKind TelemetryKind { get; init; } = CardEffectTelemetryKind.None;
    public InfectionTransformResult? InfectionTransform { get; init; }

    public static CardEffectResult Ok(string message) => new() { Success = true, Message = message };
    public static CardEffectResult Fail(string message) => new() { Success = false, Message = message };
}

public enum CardEffectTelemetryKind
{
    None,
    ZombieInfectionSucceeded,
    ZombieInfectionBlockedByShield,
    PowerZombieInfectionSucceeded,
    PowerZombieInfectionBlockedByShield,
    ZombieCured,
    PowerZombieDemoted
}
