namespace ZombieGame.Application.DTOs.Auth;

public record RegisterRequest(string Username, string PhoneNumber, string Password, string? Email = null);

public record LoginRequest(string PhoneNumber, string Password);

public record AuthResponse(Guid UserId, string Username, string AccessToken);

public record UserProfileResponse(
    Guid Id,
    string Username,
    string? Email,
    string PhoneNumber,
    int Coins,
    int Wins,
    int Losses,
    DateTime CreatedAt);
