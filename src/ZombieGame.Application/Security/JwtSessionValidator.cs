namespace ZombieGame.Application.Security;

using System.Security.Claims;
using ZombieGame.Domain.Entities;

public static class JwtSessionValidator
{
    public const string SessionIdClaim = "sid";

    public static bool TryReadSessionClaims(
        ClaimsPrincipal? principal,
        out Guid sessionId,
        out Guid playerId,
        out string jti)
    {
        sessionId = Guid.Empty;
        playerId = Guid.Empty;
        jti = string.Empty;
        if (principal is null)
            return false;

        var sid = principal.FindFirst(SessionIdClaim)?.Value;
        var subject = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tokenId = principal.FindFirst("jti")?.Value;

        if (!Guid.TryParse(sid, out sessionId)
            || !Guid.TryParse(subject, out playerId)
            || string.IsNullOrWhiteSpace(tokenId))
            return false;

        jti = tokenId;
        return true;
    }

    public static bool IsActiveSession(
        PlayerSession? session,
        Guid playerId,
        string jti,
        DateTime utcNow)
    {
        return session is not null
            && session.IsActive
            && session.ExpireDate >= utcNow
            && session.PlayerId == playerId
            && string.Equals(session.AccessTokenJti, jti, StringComparison.Ordinal);
    }
}
