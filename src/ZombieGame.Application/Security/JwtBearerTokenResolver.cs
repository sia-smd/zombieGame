namespace ZombieGame.Application.Security;

public static class AuthCookieNames
{
    public const string AccessToken = "zvh_access";
    public const string RefreshToken = "zvh_refresh";
}

/// <summary>
/// Picks a JWT for SignalR/API when the Authorization header is absent.
/// Cookie is for the web client; query <c>access_token</c> remains for Unity hubs.
/// </summary>
public static class JwtBearerTokenResolver
{
    public static string? Resolve(string? cookieAccessToken, string? queryAccessToken, string requestPath)
    {
        if (!string.IsNullOrWhiteSpace(cookieAccessToken))
            return cookieAccessToken.Trim();

        if (string.IsNullOrWhiteSpace(queryAccessToken))
            return null;

        if (requestPath.StartsWith("/hubs/game", StringComparison.OrdinalIgnoreCase) ||
            requestPath.StartsWith("/hubs/room", StringComparison.OrdinalIgnoreCase))
            return queryAccessToken.Trim();

        return null;
    }
}
