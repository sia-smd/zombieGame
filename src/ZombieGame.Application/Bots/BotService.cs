namespace ZombieGame.Application.Bots;

using System.Text.Json;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.Bots.Discussion;
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
    private readonly BotDecisionEngine _decisions;
    private readonly BotDiscussionEngine _discussion = new();
    private readonly IGameSessionStore _sessionStore;
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
        _decisions = new BotDecisionEngine(cardRegistry, cardValidator);
        _sessionStore = sessionStore;
        _settings = settings.Value;
    }

    public async Task FillMatchWithBotsAsync(Guid matchId, int targetCount, CancellationToken cancellationToken = default)
    {
        var match = await _matchRepository.GetWithPlayersAsync(matchId, cancellationToken)
            ?? throw new InvalidOperationException("Match not found.");

        var botsNeeded = targetCount - match.Players.Count;
        if (botsNeeded <= 0)
            return;

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in match.Players)
        {
            var user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(user?.Username))
                reserved.Add(user.Username);
        }

        var nextSeat = match.Players.Count;
        for (var i = 0; i < botsNeeded; i++)
        {
            var botUserId = Guid.NewGuid();
            var botUsername = await PickBotUsernameAsync(reserved, cancellationToken);

            await _userRepository.AddAsync(new Domain.Entities.User
            {
                Id = botUserId,
                Username = botUsername,
                PhoneNumber = null,
                PasswordHash = "BOT",
                AccountType = AccountType.Guest,
                Coins = 0,
                CreatedAt = DateTime.UtcNow,
                Profile = new Domain.Entities.PlayerProfile
                {
                    PlayerId = botUserId,
                    ImageId = "avatar_default_01",
                    Name = botUsername,
                    Level = 1,
                }
            }, cancellationToken);

            await _matchRepository.AddPlayerAsync(new Domain.Entities.MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = botUserId,
                IsBot = true,
                SeatIndex = nextSeat + i,
                JoinedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> PickBotUsernameAsync(ISet<string> reserved, CancellationToken cancellationToken)
    {
        var shuffled = BotNameCatalog.Names
            .Where(n => !reserved.Contains(n))
            .OrderBy(_ => _random.Next())
            .ToList();

        foreach (var name in shuffled)
        {
            if (await _userRepository.GetByUsernameAsync(name, cancellationToken) is not null)
                continue;

            reserved.Add(name);
            return name;
        }

        var fallback = $"Bot_{Guid.NewGuid().ToString("N")[..8]}";
        reserved.Add(fallback);
        return fallback;
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

    public BotCardPlayDecision? TryDecideCardPlay(GameSessionState state, Guid botUserId, Guid? battleOpponentId = null) =>
        _decisions.TryFindCardPlay(state, botUserId, _random, battleOpponentId);

    public Guid TryDecideVoteTarget(GameSessionState state, Guid botUserId) =>
        _decisions.DecideVoteTarget(state, botUserId, _random);

    public bool ShouldSpeakInDiscussion(GameSessionState state, Guid botUserId) =>
        _discussion.ShouldSpeak(state, botUserId, _random);

    public StructuredDiscussionMessage? TryDecideDiscussionMessage(GameSessionState state, Guid botUserId) =>
        _discussion.DecideMessage(state, botUserId, _random);

    public void RecordDiscussionSpoke(GameSessionState state, Guid botUserId) =>
        _discussion.RecordSpoke(state, botUserId);

    private PendingBotAction? DecideDayAction(GameSessionState state, GamePlayerState bot)
    {
        if (bot.RemainingActions <= 0)
            return null;

        var play = TryDecideCardPlay(state, bot.UserId);
        if (play is not null)
        {
            return new PendingBotAction(
                "PlayCard",
                JsonSerializer.Serialize(new { CardId = play.CardId, TargetUserId = play.TargetUserId }),
                $"bot-{bot.UserId}-play-{Guid.NewGuid():N}");
        }

        return new PendingBotAction(
            "Pass",
            """{"action":"pass"}""",
            $"bot-{bot.UserId}-pass-{Guid.NewGuid():N}");
    }

    private PendingBotAction? DecideVoteAction(GameSessionState state, GamePlayerState bot)
    {
        var target = TryDecideVoteTarget(state, bot.UserId);
        return new PendingBotAction(
            "VotePlayer",
            JsonSerializer.Serialize(new { TargetUserId = target }),
            $"bot-{bot.UserId}-vote-{Guid.NewGuid():N}");
    }
}
