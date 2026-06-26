namespace ZombieGame.Application.Interfaces;

public interface IForbiddenWordsService
{
    bool ContainsForbiddenWord(string text);
}
