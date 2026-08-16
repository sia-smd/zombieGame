namespace ZombieGame.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZombieGame.Domain.Entities;

public class PlayerStatConfiguration : IEntityTypeConfiguration<PlayerStat>
{
    public void Configure(EntityTypeBuilder<PlayerStat> builder)
    {
        builder.HasKey(s => new { s.PlayerId, s.StatKey });
        builder.Property(s => s.StatKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.StatKey);

        builder.HasOne(s => s.Player)
            .WithMany(u => u.Stats)
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.Code).IsUnique();
        builder.Property(a => a.Code).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Title).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(400).IsRequired();
        builder.Property(a => a.StatKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(a => new { a.StatKey, a.IsActive });
    }
}

public class PlayerAchievementConfiguration : IEntityTypeConfiguration<PlayerAchievement>
{
    public void Configure(EntityTypeBuilder<PlayerAchievement> builder)
    {
        builder.HasKey(p => new { p.PlayerId, p.AchievementId });

        builder.HasOne(p => p.Player)
            .WithMany(u => u.Achievements)
            .HasForeignKey(p => p.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Achievement)
            .WithMany()
            .HasForeignKey(p => p.AchievementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
