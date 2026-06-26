namespace ZombieGame.Application.Validation;

using ZombieGame.Application.Interfaces;

public sealed class ProfileNameValidator
{
    private readonly IForbiddenWordsService _forbiddenWords;
    private readonly int _maxLength;

    public ProfileNameValidator(IForbiddenWordsService forbiddenWords, int maxLength)
    {
        _forbiddenWords = forbiddenWords;
        _maxLength = maxLength;
    }

    public void Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new Common.ServiceException("Profile name is required.");

        var trimmed = name.Trim();
        if (trimmed.Length > _maxLength)
            throw new Common.ServiceException($"Profile name cannot exceed {_maxLength} characters.");

        if (_forbiddenWords.ContainsForbiddenWord(trimmed))
            throw new Common.ServiceException("Profile name contains forbidden words.");
    }
}
