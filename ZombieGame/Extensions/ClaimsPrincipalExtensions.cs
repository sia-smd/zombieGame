namespace ZombieGame.Api.Extensions;

using System.Security.Claims;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static Guid? GetSessionId(this ClaimsPrincipal principal)
    {
        var sid = principal.FindFirstValue(Infrastructure.Security.JwtTokenService.SessionIdClaim);
        return Guid.TryParse(sid, out var id) ? id : null;
    }
}
