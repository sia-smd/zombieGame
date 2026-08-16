namespace ZombieGame.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZombieGame.Domain.Entities;

public class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.HasKey(p => p.PlayerId);
        builder.Property(p => p.Name).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ImageId).HasMaxLength(100).IsRequired();
        builder.Property(p => p.CustomAvatarData).HasMaxLength(512_000);
    }
}

public class PlayerSessionConfiguration : IEntityTypeConfiguration<PlayerSession>
{
    public void Configure(EntityTypeBuilder<PlayerSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.RefreshTokenHash).IsUnique();
        builder.Property(s => s.AccessTokenJti).HasMaxLength(64).IsRequired();
        builder.Property(s => s.RefreshTokenHash).HasMaxLength(128).IsRequired();

        builder.HasOne(s => s.Player)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Device)
            .WithMany(d => d.Sessions)
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PlayerDeviceConfiguration : IEntityTypeConfiguration<PlayerDevice>
{
    public void Configure(EntityTypeBuilder<PlayerDevice> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => new { d.PlayerId, d.DeviceId }).IsUnique();
        builder.Property(d => d.DeviceId).HasMaxLength(128).IsRequired();
        builder.Property(d => d.AppVersion).HasMaxLength(32).IsRequired();
        builder.Property(d => d.Platform).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(d => d.Player)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlayerLoginLogConfiguration : IEntityTypeConfiguration<PlayerLoginLog>
{
    public void Configure(EntityTypeBuilder<PlayerLoginLog> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasIndex(l => new { l.PlayerId, l.LoginAt });
        builder.Property(l => l.DeviceId).HasMaxLength(128);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.Property(l => l.Action).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Result).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(l => l.Player)
            .WithMany(u => u.LoginLogs)
            .HasForeignKey(l => l.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountRecoveryChallengeConfiguration : IEntityTypeConfiguration<AccountRecoveryChallenge>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryChallenge> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodeHash).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(c => new { c.GuestPlayerId, c.TargetPlayerId, c.Purpose, c.ConsumedAt });

        builder.HasOne(c => c.Guest)
            .WithMany()
            .HasForeignKey(c => c.GuestPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Target)
            .WithMany()
            .HasForeignKey(c => c.TargetPlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
