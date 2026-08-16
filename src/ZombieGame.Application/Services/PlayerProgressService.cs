namespace ZombieGame.Application.Services;

using ZombieGame.Application.DTOs.Profile;
using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;
using ZombieGame.Domain.Progress;

public sealed class PlayerProgressService : IPlayerProgressService
{
    private readonly IPlayerStatRepository _stats;
    private readonly IAchievementRepository _achievements;
    private readonly IPlayerAchievementRepository _playerAchievements;
    private readonly IUnitOfWork _unitOfWork;

    public PlayerProgressService(
        IPlayerStatRepository stats,
        IAchievementRepository achievements,
        IPlayerAchievementRepository playerAchievements,
        IUnitOfWork unitOfWork)
    {
        _stats = stats;
        _achievements = achievements;
        _playerAchievements = playerAchievements;
        _unitOfWork = unitOfWork;
    }

    public async Task ApplyMatchDeltasAsync(
        GameSessionState state,
        WinTeam winningTeam,
        CancellationToken cancellationToken = default)
    {
        var deltas = new List<(Guid PlayerId, string StatKey, int Amount)>();

        foreach (var player in state.Players)
        {
            if (player.IsBot)
                continue;

            var won = IsOnTeam(player.Role, winningTeam);
            deltas.Add((player.UserId, PlayerStatKeys.Matches, 1));
            deltas.Add((player.UserId, won ? PlayerStatKeys.Wins : PlayerStatKeys.Losses, 1));

            if (state.HealsByPlayer.TryGetValue(player.UserId, out var heals) && heals > 0)
                deltas.Add((player.UserId, PlayerStatKeys.Heals, heals));

            if (state.PoisonsByPlayer.TryGetValue(player.UserId, out var poisons) && poisons > 0)
                deltas.Add((player.UserId, PlayerStatKeys.Poisons, poisons));
        }

        if (deltas.Count == 0)
            return;

        var playerIds = deltas.Select(d => d.PlayerId).Distinct().ToArray();
        var existing = await _stats.GetByPlayersAsync(playerIds, cancellationToken);
        var lookup = existing.ToDictionary(s => (s.PlayerId, s.StatKey));
        var now = DateTime.UtcNow;
        var totals = new Dictionary<(Guid PlayerId, string StatKey), int>();

        foreach (var (playerId, statKey, amount) in deltas)
        {
            if (lookup.TryGetValue((playerId, statKey), out var row))
            {
                row.Value += amount;
                row.UpdatedAt = now;
                _stats.Update(row);
            }
            else
            {
                row = new PlayerStat
                {
                    PlayerId = playerId,
                    StatKey = statKey,
                    Value = amount,
                    UpdatedAt = now
                };
                lookup[(playerId, statKey)] = row;
                await _stats.AddAsync(row, cancellationToken);
            }

            totals[(playerId, statKey)] = row.Value;
        }

        var statKeys = totals.Keys.Select(k => k.StatKey).Distinct().ToArray();
        var catalog = await _achievements.GetActiveByStatKeysAsync(statKeys, cancellationToken);
        var unlocked = (await _playerAchievements.GetUnlockedPairsAsync(playerIds, cancellationToken))
            .ToHashSet();

        foreach (var medal in catalog)
        {
            foreach (var playerId in playerIds)
            {
                if (!totals.TryGetValue((playerId, medal.StatKey), out var value) || value < medal.Threshold)
                    continue;

                if (!unlocked.Add((playerId, medal.Id)))
                    continue;

                await _playerAchievements.AddAsync(new PlayerAchievement
                {
                    PlayerId = playerId,
                    AchievementId = medal.Id,
                    UnlockedAt = now,
                    StatValueAtUnlock = value
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlayerProgressResponse> GetProgressAsync(
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var stats = await _stats.GetByPlayerAsync(playerId, cancellationToken);
        var statMap = stats.ToDictionary(s => s.StatKey, s => s.Value, StringComparer.OrdinalIgnoreCase);
        var catalog = await _achievements.GetActiveAsync(cancellationToken);
        var unlocked = (await _playerAchievements.GetByPlayerAsync(playerId, cancellationToken))
            .ToDictionary(p => p.AchievementId);

        var achievements = catalog.Select(a =>
        {
            statMap.TryGetValue(a.StatKey, out var current);
            unlocked.TryGetValue(a.Id, out var row);
            return new AchievementProgressDto(
                a.Code,
                a.Title,
                a.Description,
                a.StatKey,
                a.Threshold,
                a.Tier,
                current,
                row is not null,
                row?.UnlockedAt);
        }).ToList();

        return new PlayerProgressResponse(statMap, achievements);
    }

    private static bool IsOnTeam(PlayerRole role, WinTeam team) => team switch
    {
        WinTeam.Humans => role == PlayerRole.Human,
        WinTeam.Zombies => role is PlayerRole.Zombie or PlayerRole.PowerZombie,
        _ => false
    };
}
