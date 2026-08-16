using System;
using System.Collections.Generic;

namespace ZombieGame.UnityClient.Networking.Models
{
    /// <summary>Authoritative state from SignalR "SyncState" (camelCase JSON from server).</summary>
    [Serializable]
    public sealed class GameStateDto
    {
        public Guid MatchId { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public GamePhase CurrentPhase { get; set; }
        public int TurnNumber { get; set; }
        public DateTime? PhaseEndsAt { get; set; }
        public List<GamePlayerStateDto> Players { get; set; } = new();
        public List<PlayerCardStateDto> PlayerHands { get; set; } = new();
    }

    [Serializable]
    public sealed class GamePlayerStateDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public PlayerRole Role { get; set; }
        public bool IsBot { get; set; }
        public bool IsAlive { get; set; } = true;
        public int SeatIndex { get; set; }
        public bool HasShield { get; set; }
        public int RemainingHealth { get; set; } = 1;
        public int ShotgunHitCount { get; set; }
        public int ActionsUsedThisTurn { get; set; }
        public int ActionsPerTurn { get; set; } = 2;
        public int RemainingActions { get; set; }
        public bool HasRevealedThisDay { get; set; }
        public bool InactiveForNextDealing { get; set; }
        public int PassesUsedThisDay { get; set; }
        public bool IsInfectedTeam { get; set; }
    }

    [Serializable]
    public sealed class PlayerCardStateDto
    {
        public Guid UserId { get; set; }
        public Guid RoleCardId { get; set; }
        public Guid? InventorySlot1 { get; set; }
        public Guid? InventorySlot2 { get; set; }
        public List<Guid> DisabledCardIds { get; set; } = new();
    }

    [Serializable]
    public sealed class GameEventDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed record PlayCardRequest(Guid CardId, Guid? TargetUserId, string IdempotencyKey);
    public sealed record PassActionRequest(string IdempotencyKey);
    public sealed record EndTurnRequest(string IdempotencyKey);
    public sealed record VotePlayerRequest(Guid TargetUserId, string IdempotencyKey);
}
