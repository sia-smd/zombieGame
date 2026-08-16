namespace ZombieGame.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZombieGame.Domain.Entities;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL");
        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.PendingPhoneNumber).HasMaxLength(20);
        builder.Property(u => u.Email).HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.MobileVerificationCodeHash).HasMaxLength(500);
        builder.Property(u => u.AccountType).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(u => u.Profile)
            .WithOne(p => p.Player)
            .HasForeignKey<PlayerProfile>(p => p.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.SessionToken).IsUnique();
        builder.Property(m => m.SessionToken).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(40);
        builder.Property(m => m.FillWithBots).HasDefaultValue(true);
    }
}

public class MatchPlayerConfiguration : IEntityTypeConfiguration<MatchPlayer>
{
    public void Configure(EntityTypeBuilder<MatchPlayer> builder)
    {
        builder.HasKey(mp => mp.Id);
        builder.HasIndex(mp => new { mp.MatchId, mp.UserId }).IsUnique();
        builder.HasIndex(mp => new { mp.MatchId, mp.SeatIndex }).IsUnique();

        builder.HasOne(mp => mp.Match)
            .WithMany(m => m.Players)
            .HasForeignKey(mp => mp.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mp => mp.User)
            .WithMany(u => u.MatchPlayers)
            .HasForeignKey(mp => mp.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Description).HasMaxLength(200).IsRequired();

        builder.HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GameActionLogConfiguration : IEntityTypeConfiguration<GameActionLog>
{
    public void Configure(EntityTypeBuilder<GameActionLog> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasIndex(l => new { l.MatchId, l.IdempotencyKey }).IsUnique();
        builder.Property(l => l.ActionType).HasMaxLength(50).IsRequired();
        builder.Property(l => l.PayloadJson).IsRequired();
        builder.Property(l => l.IdempotencyKey).HasMaxLength(100);
        builder.Property(l => l.StateSnapshotJson);

        builder.HasOne(l => l.Match)
            .WithMany(m => m.ActionLogs)
            .HasForeignKey(l => l.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.User)
            .WithMany(u => u.GameActionLogs)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CardDefinitionConfiguration : IEntityTypeConfiguration<CardDefinition>
{
    public void Configure(EntityTypeBuilder<CardDefinition> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500).IsRequired();
        builder.Property(c => c.EffectKey).HasMaxLength(100).IsRequired();
    }
}
