namespace ZombieGame.Application.Services;

using System.Text.Json;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.DTOs.Game;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Entities;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public class CoinService : ICoinService
{
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GameSettings _settings;

    public CoinService(
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IOptions<GameSettings> settings)
    {
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
    }

    public async Task<bool> CanAffordEntryFeeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is not null && user.Coins >= _settings.MatchEntryFeeCoins;
    }

    public async Task DeductEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default)
    {
        if (await _transactionRepository.ExistsAsync(userId, matchId, TransactionType.MatchEntryFee, cancellationToken))
            return;

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        if (user.Coins < _settings.MatchEntryFeeCoins)
            throw new ServiceException("Insufficient coins for entry fee.");

        user.Coins -= _settings.MatchEntryFeeCoins;
        _userRepository.Update(user);

        await _transactionRepository.AddAsync(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = matchId,
            Type = TransactionType.MatchEntryFee,
            Amount = -_settings.MatchEntryFeeCoins,
            BalanceAfter = user.Coins,
            Description = "Match entry fee",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CollectMatchEntryFeesAsync(
        Guid matchId,
        IEnumerable<Guid> humanUserIds,
        CancellationToken cancellationToken = default)
    {
        var ids = humanUserIds.Distinct().ToArray();
        foreach (var userId in ids)
        {
            if (await _transactionRepository.ExistsAsync(userId, matchId, TransactionType.MatchEntryFee, cancellationToken))
                continue;

            if (!await CanAffordEntryFeeAsync(userId, cancellationToken))
                throw new ServiceException($"Insufficient coins. Entry fee is {_settings.MatchEntryFeeCoins} coins.");
        }

        foreach (var userId in ids)
            await DeductEntryFeeAsync(userId, matchId, cancellationToken);
    }

    public async Task AwardWinRewardAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        user.Coins += _settings.MatchWinRewardCoins;
        user.Wins += 1;
        _userRepository.Update(user);

        await _transactionRepository.AddAsync(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = matchId,
            Type = TransactionType.MatchWinReward,
            Amount = _settings.MatchWinRewardCoins,
            BalanceAfter = user.Coins,
            Description = "Match win reward",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordLossAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        user.Losses += 1;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RefundEntryFeeAsync(Guid userId, Guid matchId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException("User not found.");

        user.Coins += _settings.MatchEntryFeeCoins;
        _userRepository.Update(user);

        await _transactionRepository.AddAsync(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchId = matchId,
            Type = TransactionType.MatchRefund,
            Amount = _settings.MatchEntryFeeCoins,
            BalanceAfter = user.Coins,
            Description = "Match entry refund",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
