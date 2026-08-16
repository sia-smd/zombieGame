namespace ZombieGame.Domain.Enums;

/// <summary>
/// Public room tension shown to all players (role-blind). Derived from eliminations,
/// infections, and how many people are still alive — never from hidden roles.
/// </summary>
public enum RoomMood
{
    Safe = 0,
    Suspicious = 1,
    Danger = 2,
    Critical = 3
}
