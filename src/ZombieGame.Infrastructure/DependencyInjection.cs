namespace ZombieGame.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure.Cards;
using ZombieGame.Infrastructure.Persistence;
using ZombieGame.Infrastructure.Repositories;
using ZombieGame.Infrastructure.Security;

public static partial class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IPlayerSessionRepository, PlayerSessionRepository>();
        services.AddScoped<IPlayerDeviceRepository, PlayerDeviceRepository>();
        services.AddScoped<IPlayerLoginLogRepository, PlayerLoginLogRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IGameActionLogRepository, GameActionLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICardRegistry, CardRegistry>();

        services.AddGameInfrastructure(configuration);

        return services;
    }
}
