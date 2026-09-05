namespace ZombieGame.Api.Security;

using ZombieGame.Application.Security;

public static class AuthCookies
{
    public static void Write(
        HttpContext http,
        string accessToken,
        string refreshToken,
        DateTime accessExpiresAt,
        DateTime refreshExpiresAt)
    {
        var secure = http.Request.IsHttps;
        http.Response.Cookies.Append(
            AuthCookieNames.AccessToken,
            accessToken,
            CreateOptions(accessExpiresAt, secure, "/"));
        http.Response.Cookies.Append(
            AuthCookieNames.RefreshToken,
            refreshToken,
            CreateOptions(refreshExpiresAt, secure, "/api/account"));
    }

    public static void Clear(HttpContext http)
    {
        var secure = http.Request.IsHttps;
        var expired = DateTimeOffset.UnixEpoch;
        http.Response.Cookies.Append(
            AuthCookieNames.AccessToken,
            string.Empty,
            CreateOptions(expired, secure, "/"));
        http.Response.Cookies.Append(
            AuthCookieNames.RefreshToken,
            string.Empty,
            CreateOptions(expired, secure, "/api/account"));
    }

    public static string? ReadRefreshToken(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var value)
            ? value
            : null;

    private static CookieOptions CreateOptions(DateTimeOffset expires, bool secure, string path) =>
        new()
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Expires = expires,
            Path = path,
            IsEssential = true
        };
}
