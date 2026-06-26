namespace ZombieGame.Application.Bots;

using System.Text.Json;
using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public class BotPlayer
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public int SeatIndex { get; init; }
}

public class BotService : IBotService
{
    private readonly IMatchRepository _matchRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICardRegistry _cardRegistry;
    private readonly IGameSessionStore _sessionStore;
    private readonly ICardPlayValidator _cardValidator;
    private readonly GameSettings _settings;
    private readonly Random _random = new();

    public BotService(
        IMatchRepository matchRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICardRegistry cardRegistry,
        IGameSessionStore sessionStore,
        ICardPlayValidator cardValidator,
        IOptions<GameSettings> settings)
    {
        _matchRepository = matchRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _cardRegistry = cardRegistry;
        _sessionStore = sessionStore;
        _cardValidator = cardValidator;
        _settings = settings.Value;
    }

    public async Task FillMatchWithBotsAsync(Guid matchId, int targetCount, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)
            ?? throw new InvalidOperationException("Match not found.");

        var botsNeeded = targetCount - match.Players.Count;
        for (var i = 0; i < botsNeeded; i++)
        {
            var botUserId = Guid.NewGuid();
            var botUsername = $"Bot_{botUserId.ToString()[..8]}";

            await _userRepository.AddAsync(new Domain.Entities.User
            {
                Id = botUserId,
                Username = botUsername,
                PhoneNumber = $"bot-{botUserId:N}",
                PasswordHash = "BOT",
                AccountType = AccountType.Guest,
                Coins = 0,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            match.Players.Add(new Domain.Entities.MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = botUserId,
                IsBot = true,
                SeatIndex = match.Players.Count,
                JoinedAt = DateTime.UtcNow
            });
        }

        _matchRepository.Update(match);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PendingBotAction?> DecideNextActionAsync(Guid matchId, Guid botUserId, CancellationToken cancellationToken = default)
    {
        var state = await _sessionStore.GetAsync(matchId, cancellationToken);
        if (state is null) return null;

        var bot = state.GetPlayer(botUserId);
        if (bot is null || !bot.IsAlive) return null;

        return state.CurrentPhase switch
        {
            GamePhase.Day => DecideDayAction(state, bot),
            GamePhase.Voting => DecideVoteAction(state, bot),
            _ => null
        };
    }

    private PendingBotAction? DecideDayAction(GameSessionState state, GamePlayerState bot)
    {
        if (bot.RemainingActions <= 0)
            return null;

        var playCard = TryFindCardPlay(state, bot);
        if (playCard is not null)
            return playCard;

        return new PendingBotAction(
            "Pass",
            """{"action":"pass"}""",
            $"bot-{bot.UserId}-pass-{Guid.NewGuid():N}");
    }

    private PendingBotAction? TryFindCardPlay(GameSessionState state, GamePlayerState bot)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == bot.UserId);
        if (hand is null || hand.CardIds.Count == 0)
            return null;

        var shuffledCards = hand.CardIds.OrderBy(_ => _random.Next()).ToList();
        foreach (var cardId in shuffledCards)
        {
            var card = _cardRegistry.GetById(cardId);
            if (card is null) continue;

            try
            {
                _cardValidator.ValidateRoleCanPlayCard(bot.Role, card.EffectKey);
                _cardValidator.ValidateCardNotDisabled(hand, cardId);
            }
            catch
            {
                continue;
            }

            var targetId = PickTarget(state, bot, card.EffectKey);
            if (targetId is null) continue;

            return new PendingBotAction(
                "PlayCard",
                JsonSerializer.Serialize(new { CardId = cardId, TargetUserId = targetId }),
                $"bot-{bot.UserId}-play-{Guid.NewGuid():N}");
        }

        return null;
    }

    private PendingBotAction? DecideVoteAction(GameSessionState state, GamePlayerState bot)
    {
        SuspicionScoring.EnsureInitialized(state);
        var target = SuspicionScoring.PickHighestSuspicionTarget(state, bot.UserId);
        return Vote(target, bot.UserId);
    }

    private static PendingBotAction Vote(Guid targetUserId, Guid botUserId) =>
        new(
            "VotePlayer",
            JsonSerializer.Serialize(new { TargetUserId = targetUserId }),
            $"bot-{botUserId}-vote-{Guid.NewGuid():N}");

    private static Guid? PickTarget(GameSessionState state, GamePlayerState bot, string effectKey)
    {
        if (effectKey.Equals("shield", StringComparison.OrdinalIgnoreCase))
            return bot.UserId;

        var alive = state.AlivePlayers.Where(p => p.UserId != bot.UserId).ToList();
        if (alive.Count == 0) return null;

        return effectKey switch
        {
            "infect" or "power_zombie" => alive.FirstOrDefault(p => p.Role == PlayerRole.Human)?.UserId,
            "shoot" => alive.FirstOrDefault(p => p.IsInfectedTeam && p.HasRevealedThisDay)?.UserId,
            "heal" => alive.FirstOrDefault(p => p.Role == PlayerRole.Zombie && p.HasRevealedThisDay)?.UserId,
            _ => alive[Random.Shared.Next(alive.Count)].UserId
        };
    }
}
