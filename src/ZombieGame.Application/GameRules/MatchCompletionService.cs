namespace ZombieGame.Application.GameRules;

using ZombieGame.Application.Interfaces;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public interface IMatchCompletionService
{
    Task CompleteMatchAsync(Match match, GameSessionState state, WinTeam winningTeam, CancellationToken cancellationToken = default);
}

public sealed class MatchCompletionService : IMatchCompletionService
{
    private readonly ICoinService _coinService;
    private readonly IMatchRepository _matchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPlayerProgressService? _playerProgress;

    public MatchCompletionService(
        ICoinService coinService,
        IMatchRepository matchRepository,
        IUnitOfWork unitOfWork,
        IPlayerProgressService? playerProgress = null)
    {
        _coinService = coinService;
        _matchRepository = matchRepository;
        _unitOfWork = unitOfWork;
        _playerProgress = playerProgress;
    }

    public async Task CompleteMatchAsync(Match match, GameSessionState state, WinTeam winningTeam, CancellationToken cancellationToken = default)
    {
        if (match.Status == MatchStatus.Finished)
            return;

        if (winningTeam == WinTeam.None)
            throw new InvalidOperationException("A match cannot be completed without a winner.");

        state.WinTeam = winningTeam;
        state.CurrentPhase = GamePhase.Resolution;

        match.Status = MatchStatus.Finished;
        match.FinishedAt = DateTime.UtcNow;
        match.CurrentPhase = GamePhase.Resolution;
        match.WinnerUserId = state.Players.FirstOrDefault(p => IsOnTeam(p.Role, winningTeam))?.UserId;

        foreach (var sessionPlayer in state.Players)
        {
            var matchPlayer = match.Players.FirstOrDefault(p => p.UserId == sessionPlayer.UserId);
            if (matchPlayer is not null)
            {
                matchPlayer.Role = sessionPlayer.Role;
                matchPlayer.IsAlive = sessionPlayer.IsAlive;
            }

            if (sessionPlayer.IsBot) continue;

            if (IsOnTeam(sessionPlayer.Role, winningTeam))
                await _coinService.AwardWinRewardAsync(sessionPlayer.UserId, match.Id, cancellationToken);
            else
                await _coinService.RecordLossAsync(sessionPlayer.UserId, cancellationToken);
        }

        if (_playerProgress is not null)
            await _playerProgress.ApplyMatchDeltasAsync(state, winningTeam, cancellationToken);

        _matchRepository.Update(match);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool IsOnTeam(PlayerRole role, WinTeam team) => team switch
    {
        WinTeam.Humans => role == PlayerRole.Human,
        WinTeam.Zombies => role is PlayerRole.Zombie or PlayerRole.PowerZombie,
        _ => false
    };
}
