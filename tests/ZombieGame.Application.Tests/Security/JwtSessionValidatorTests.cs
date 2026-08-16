namespace ZombieGame.Application.Tests.Security;

using System.Security.Claims;
using ZombieGame.Application.Security;
using ZombieGame.Domain.Entities;

public class JwtSessionValidatorTests
{
    [Fact]
    public void TryReadSessionClaims_RejectsMissingSidJtiOrSub()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("jti", Guid.NewGuid().ToString())
        ], "test"));

        Assert.False(JwtSessionValidator.TryReadSessionClaims(principal, out _, out _, out _));
        Assert.False(JwtSessionValidator.TryReadSessionClaims(null, out _, out _, out _));
    }

    [Fact]
    public void TryReadSessionClaims_ReadsRequiredClaims()
    {
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtSessionValidator.SessionIdClaim, sessionId.ToString()),
            new Claim("sub", playerId.ToString()),
            new Claim("jti", jti)
        ], "test"));

        Assert.True(JwtSessionValidator.TryReadSessionClaims(principal, out var parsedSession, out var parsedPlayer, out var parsedJti));
        Assert.Equal(sessionId, parsedSession);
        Assert.Equal(playerId, parsedPlayer);
        Assert.Equal(jti, parsedJti);
    }

    [Fact]
    public void IsActiveSession_RejectsMismatchedJtiPlayerOrExpiredSession()
    {
        var playerId = Guid.NewGuid();
        var jti = "current-jti";
        var now = DateTime.UtcNow;
        var session = new PlayerSession
        {
            PlayerId = playerId,
            AccessTokenJti = jti,
            IsActive = true,
            ExpireDate = now.AddMinutes(5)
        };

        Assert.True(JwtSessionValidator.IsActiveSession(session, playerId, jti, now));
        Assert.False(JwtSessionValidator.IsActiveSession(session, playerId, "old-jti", now));
        Assert.False(JwtSessionValidator.IsActiveSession(session, Guid.NewGuid(), jti, now));
        Assert.False(JwtSessionValidator.IsActiveSession(session, playerId, jti, now.AddMinutes(6)));
        session.IsActive = false;
        Assert.False(JwtSessionValidator.IsActiveSession(session, playerId, jti, now));
    }
}
