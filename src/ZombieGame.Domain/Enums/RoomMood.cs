namespace ZombieGame.Domain.Enums;

/// <summary>
/// Public room tension shown to all players. Derived from how many players are still
/// alive and the current human vs infected split — stable across days until someone
/// dies or converts.
/// </summary>
public enum RoomMood
{
    Safe = 0,
    Suspicious = 1,
    Danger = 2,
    Critical = 3
}
