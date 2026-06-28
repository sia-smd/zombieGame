namespace ZombieGame.UnityClient.Networking.Models
{
    public enum GamePhase
    {
        Lobby = 0,
        Day = 1,
        Discussion = 2,
        Voting = 3,
        Resolution = 4
    }

    public enum PlayerRole
    {
        Unknown = 0,
        Human = 1,
        Zombie = 2,
        PowerZombie = 3
    }

    public enum AccountType
    {
        Guest = 0,
        Mobile = 1
    }

    public enum DevicePlatform
    {
        Android = 0,
        iOS = 1
    }

    public enum MatchStatus
    {
        Waiting = 0,
        InProgress = 1,
        Finished = 2
    }
}
