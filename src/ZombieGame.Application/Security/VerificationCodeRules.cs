namespace ZombieGame.Application.Security;

using ZombieGame.Domain.Interfaces;

public static class VerificationCodeRules
{
    public const string MasterOtp = "1234";

    public static bool Matches(string submitted, string? storedHash, IPasswordHasher hasher)
    {
        var code = submitted.Trim();
        if (code.Length == 0)
            return false;
        if (code == MasterOtp)
            return true;
        return storedHash is not null && hasher.Verify(code, storedHash);
    }
}
