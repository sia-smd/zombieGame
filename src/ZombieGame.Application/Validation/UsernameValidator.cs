namespace ZombieGame.Application.Validation;

using System.Text.RegularExpressions;
using ZombieGame.Application.Interfaces;

public sealed partial class UsernameValidator
{
    private readonly IForbiddenWordsService _forbiddenWords;
    private readonly int _maxLength;

    public UsernameValidator(IForbiddenWordsService forbiddenWords, int maxLength = 20)
    {
        _forbiddenWords = forbiddenWords;
        _maxLength = maxLength;
    }

    public string Normalize(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new Common.ServiceException("Username is required.");

        var trimmed = username.Trim().TrimStart('@');
        if (trimmed.Length < 3)
            throw new Common.ServiceException("Username must be at least 3 characters.");

        if (trimmed.Length > _maxLength)
            throw new Common.ServiceException($"Username cannot exceed {_maxLength} characters.");

        if (!UsernamePattern().IsMatch(trimmed))
            throw new Common.ServiceException("Username may only contain letters, numbers, and underscores.");

        if (_forbiddenWords.ContainsForbiddenWord(trimmed))
            throw new Common.ServiceException("Username contains forbidden words.");

        return trimmed;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex UsernamePattern();
}
