namespace ZombieGame.Domain.Interfaces;

public interface ITokenService
{
    TokenPairResult CreateTokenPair(Guid userId, string username, Guid sessionId);

    string GenerateRefreshToken();

    string HashToken(string token);
}

public sealed record TokenPairResult(string AccessToken, string Jti, DateTime AccessExpiresAt);

