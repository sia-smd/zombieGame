namespace ZombieGame.Domain.Enums;

/// <summary>Per-player activity badge shown in the room player list (Room Flow V2).</summary>
public enum RoomPlayerActivity
{
    Available = 0,
    /// <summary>Sent an invitation and is waiting for an answer.</summary>
    Inviting = 1,
    /// <summary>Received an invitation and has not answered yet.</summary>
    Waiting = 2,
    InBattle = 3,
    Resting = 4,
    Disconnected = 5,
    Eliminated = 6
}
