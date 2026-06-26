namespace ZombieGame.Application.Interfaces;

public interface IAvatarCatalogService
{
    bool IsAllowed(string imageId);
    IReadOnlyList<string> GetAllowedAvatarIds();
}
