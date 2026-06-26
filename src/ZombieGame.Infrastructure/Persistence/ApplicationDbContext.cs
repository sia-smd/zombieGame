namespace ZombieGame.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ZombieGame.Domain.Entities;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<PlayerDevice> PlayerDevices => Set<PlayerDevice>();
    public DbSet<PlayerLoginLog> PlayerLoginLogs => Set<PlayerLoginLog>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<GameActionLog> GameActionLogs => Set<GameActionLog>();
    public DbSet<CardDefinition> CardDefinitions => Set<CardDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
