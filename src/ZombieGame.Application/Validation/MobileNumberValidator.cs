namespace ZombieGame.Application.Validation;

using System.Text.RegularExpressions;

public static partial class MobileNumberValidator
{
    private static readonly Regex CanonicalPattern = CanonicalRegex();

    public static bool IsValid(string? mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
            return false;
        return CanonicalPattern.IsMatch(Normalize(mobileNumber));
    }

    /// <summary>Canonical Iranian mobile: 09xxxxxxxxx</summary>
    public static string Normalize(string mobileNumber)
    {
        var digits = new string(mobileNumber.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12)
            return "0" + digits[2..];
        if (digits.Length == 10 && digits.StartsWith('9'))
            return "0" + digits;
        return digits;
    }

    public static IReadOnlyList<string> LookupKeys(string mobileNumber)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var trimmed = mobileNumber.Trim();
        if (trimmed.Length > 0)
            keys.Add(trimmed);

        var normalized = Normalize(mobileNumber);
        if (normalized.Length > 0)
            keys.Add(normalized);

        if (normalized.Length == 11 && normalized.StartsWith("09", StringComparison.Ordinal))
        {
            keys.Add("+98" + normalized[1..]);
            keys.Add("98" + normalized[1..]);
            keys.Add(normalized[1..]);
        }

        return keys.ToList();
    }

    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex CanonicalRegex();
}
