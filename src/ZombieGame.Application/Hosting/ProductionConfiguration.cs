namespace ZombieGame.Application.Hosting;

using ZombieGame.Application.Options;

public static class ProductionConfiguration
{
    public static void ValidateJwtSecret(string? secret, bool isProduction)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Secret must be supplied by environment configuration and contain at least 32 characters.");

        if (!isProduction)
            return;

        if (secret.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase)
            || secret.Contains("DEVELOPMENT_ONLY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Jwt:Secret cannot use a development or placeholder value in production.");
    }

    public static string[] ResolveAllowedOrigins(string[]? configured, bool isDevelopment)
    {
        if (configured is { Length: > 0 })
            return configured;

        if (isDevelopment)
            return ["http://localhost:5173", "https://localhost:5173"];

        return [];
    }

    public static void ValidateSmsSettings(SmsSettings settings, bool isProduction)
    {
        if (!settings.UsesKavenegar)
            return;

        if (!string.IsNullOrWhiteSpace(settings.Kavenegar.ApiKey))
            return;

        if (isProduction)
            throw new InvalidOperationException(
                "Sms:Kavenegar:ApiKey must be supplied when Sms:Provider is Kavenegar.");
    }
}
