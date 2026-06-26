namespace ZombieGame.Application.GameRules.Cards;

public interface ICardEffectHandler
{
    string EffectKey { get; }
    bool CanPlay(CardEffectContext context);
    CardEffectResult Apply(CardEffectContext context);
}

public interface ICardEffectResolver
{
    ICardEffectHandler Resolve(string effectKey);
    CardEffectResult Play(CardEffectContext context);
}
