namespace ZombieGame.Application.Validation;

using System.Text.RegularExpressions;

public static partial class MobileNumberValidator
{
    private static readonly Regex MobilePattern = MobileRegex();

    public static bool IsValid(string? mobileNumber) =>
        !string.IsNullOrWhiteSpace(mobileNumber) && MobilePattern.IsMatch(mobileNumber.Trim());

    public static string Normalize(string mobileNumber) => mobileNumber.Trim();

    [GeneratedRegex(@"^\+?[1-9]\d{7,14}$")]
    private static partial Regex MobileRegex();
}
