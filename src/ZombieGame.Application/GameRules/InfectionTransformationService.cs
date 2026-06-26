namespace ZombieGame.Application.GameRules;

using ZombieGame.Domain.Interfaces;
using ZombieGame.Domain.Models;

public sealed class InfectionTransformResult
{
    public int DisabledShotguns { get; init; }
    public int DisabledHeals { get; init; }
    public bool HasRemainingShieldCard { get; init; }
}

public sealed class InfectionTransformationService
{
    private readonly ICardRegistry _cards;

    public InfectionTransformationService(ICardRegistry cards) => _cards = cards;

    public InfectionTransformResult ApplyHumanToZombie(GameSessionState state, Guid playerId)
    {
        var hand = state.PlayerHands.FirstOrDefault(h => h.UserId == playerId)
            ?? throw new InvalidOperationException("Player hand not found.");

        var disabledShotguns = 0;
        var disabledHeals = 0;
        var hasShield = false;

        foreach (var cardId in hand.CardIds)
        {
            var card = _cards.GetById(cardId);
            if (card is null) continue;

            switch (card.EffectKey.ToLowerInvariant())
            {
                case "shoot":
                    hand.DisabledCardIds.Add(cardId);
                    disabledShotguns++;
                    break;
                case "heal":
                    hand.DisabledCardIds.Add(cardId);
                    disabledHeals++;
                    break;
                case "shield":
                    hasShield = true;
                    break;
            }
        }

        return new InfectionTransformResult
        {
            DisabledShotguns = disabledShotguns,
            DisabledHeals = disabledHeals,
            HasRemainingShieldCard = hasShield
        };
    }

    public void RevertZombieToHuman(PlayerCardState hand) => hand.DisabledCardIds.Clear();
}
