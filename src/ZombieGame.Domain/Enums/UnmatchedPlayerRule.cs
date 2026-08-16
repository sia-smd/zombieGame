namespace ZombieGame.Domain.Enums;

/// <summary>What to do when an odd number of alive players leaves one unmatched.</summary>
public enum UnmatchedPlayerRule
{
    Skip = 0,
    Bot = 1,
    RandomAssignment = 2
}
