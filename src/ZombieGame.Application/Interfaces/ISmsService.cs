namespace ZombieGame.Application.Interfaces;

public interface ISmsService
{
    Task SendVerificationCodeAsync(string mobileNumber, string code, CancellationToken cancellationToken = default);
}
