namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public interface ICardDealingService
{
    void DealInitialHands(GameSessionState state);
    void DealDailyCards(GameSessionState state);
}

public sealed class CardDealingService : ICardDealingService
{
    private readonly ICardRegistry _cardRegistry;
    private readonly Options.GameSettings _settings;

    public CardDealingService(ICardRegistry cardRegistry, Microsoft.Extensions.Options.IOptions<Options.GameSettings> settings)
    {
        _cardRegistry = cardRegistry;
        _settings = settings.Value;
    }

    public void DealInitialHands(GameSessionState state) =>
        DealCards(state, _settings.InitialHandSize);

    public void DealDailyCards(GameSessionState state) =>
        DealCards(state, _settings.CardsDealtPerDay, respectInactivePenalty: true);

    private void DealCards(GameSessionState state, int count, bool respectInactivePenalty = false)
    {
        foreach (var player in state.AlivePlayers)
        {
            var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == player.UserId);
            if (hand is null) continue;

            if (respectInactivePenalty && player.InactiveForNextDealing)
            {
                player.InactiveForNextDealing = false;
                continue;
            }

            var pool = GetCardPoolForRole(player.Role);
            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var card = pool[Random.Shared.Next(pool.Count)];
                hand.CardIds.Add(card.Id);
            }
        }
    }

    private void DealCards(GameSessionState state, int count) =>
        DealCards(state, count, respectInactivePenalty: false);

    private List<Domain.Entities.CardDefinition> GetCardPoolForRole(PlayerRole role)
    {
        var all = _cardRegistry.GetAll();
        return role switch
        {
            PlayerRole.Human => all.Where(c => c.EffectKey is "shoot" or "heal" or "shield").ToList(),
            PlayerRole.Zombie => all.Where(c => c.EffectKey == "infect").ToList(),
            PlayerRole.PowerZombie => all.Where(c => c.EffectKey == "power_zombie").ToList(),
            _ => all.Where(c => c.Type != Domain.Enums.CardType.EventCard).ToList()
        };
    }
}
