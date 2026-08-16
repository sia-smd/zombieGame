namespace ZombieGame.Application.Tests.Bots;

using ZombieGame.Application.Bots.Cognition;
using ZombieGame.Application.GameRules.Cards;
using ZombieGame.Application.Simulation;
using ZombieGame.Application.Tests.Support;
using ZombieGame.Domain.Enums;
using ZombieGame.Domain.Models;

public class BotCognitionTests
{
    [Fact]
    public void InitializeBots_AssignsPersonalityAndBeliefsPerBot()
    {
        var botId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((botId, PlayerRole.Human, true), (otherId, PlayerRole.Zombie, true));
        state.Players[0].IsBot = true;

        BotObservationRecorder.InitializeBots(state, new Random(42));

        Assert.True(state.BotCognition.ContainsKey(botId));
        var p = state.BotCognition[botId].Personality;
        Assert.InRange(p.Aggression, 0, 100);
        Assert.InRange(p.Confidence, 0, 100);
        Assert.Equal(0, BotBeliefService.GetBelief(state, botId, botId));
        Assert.InRange(BotBeliefService.GetBelief(state, botId, otherId), 20, 30);
    }

    [Fact]
    public void DecisionEngine_Shotgun_DoesNotTargetLowBeliefUnrevealedPlayer()
    {
        var humanBot = Guid.NewGuid();
        var hiddenZombie = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanBot, PlayerRole.Human, true),
            (hiddenZombie, PlayerRole.Zombie, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        state.BotCognition[humanBot].Personality = new BotPersonality
        {
            Aggression = 30,
            RiskTolerance = 20,
            Patience = 80,
            Trust = 50,
            Confidence = 40
        };
        GameTestBuilder.AddCardsToHand(state, humanBot, TestCards.Shotgun);

        var engine = new BotDecisionEngine(new FakeCardRegistry(), new CardPlayValidator());
        var play = engine.TryFindCardPlay(state, humanBot, new Random(1));

        Assert.Null(play);
    }

    [Fact]
    public void DecisionEngine_Shotgun_TargetsHighBeliefUnrevealedSuspect()
    {
        var humanBot = Guid.NewGuid();
        var hiddenZombie = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanBot, PlayerRole.Human, true),
            (hiddenZombie, PlayerRole.Zombie, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        BotBeliefService.SetBelief(state, humanBot, hiddenZombie, 85);
        state.BotCognition[humanBot].Personality = new BotPersonality
        {
            Aggression = 85,
            RiskTolerance = 90,
            Patience = 10,
            Trust = 40,
            Confidence = 80
        };
        GameTestBuilder.AddCardsToHand(state, humanBot, TestCards.Shotgun);

        var engine = new BotDecisionEngine(new FakeCardRegistry(), new CardPlayValidator());
        var play = engine.TryFindCardPlay(state, humanBot, new Random(7));

        Assert.NotNull(play);
        Assert.Equal(hiddenZombie, play!.TargetUserId);
    }

    [Fact]
    public void DecisionEngine_Shotgun_TargetsPubliclyRevealedInfected()
    {
        var humanBot = Guid.NewGuid();
        var revealedZombie = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanBot, PlayerRole.Human, true),
            (revealedZombie, PlayerRole.Zombie, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        state.BotCognition[humanBot].PubliclyKnownInfected.Add(revealedZombie);
        state.BotCognition[humanBot].Personality = new BotPersonality
        {
            Aggression = 80,
            RiskTolerance = 90,
            Patience = 10,
            Trust = 50,
            Confidence = 80
        };
        GameTestBuilder.AddCardsToHand(state, humanBot, TestCards.Shotgun);

        var engine = new BotDecisionEngine(new FakeCardRegistry(), new CardPlayValidator());
        var play = engine.TryFindCardPlay(state, humanBot, Random.Shared);

        Assert.NotNull(play);
        Assert.Equal(revealedZombie, play!.TargetUserId);
    }

    [Fact]
    public void SuspicionProbabilities_HealDominatesAtSixtyPercent()
    {
        var personality = new BotPersonality
        {
            Aggression = 50,
            RiskTolerance = 50,
            Patience = 30,
            Trust = 50,
            Confidence = 50
        };

        var healAt60 = BotDecisionEngine.GetHealPlayProbability(60, personality);
        var shootAt60 = BotDecisionEngine.GetShootPlayProbability(60, personality);

        Assert.True(healAt60 >= 0.85);
        Assert.True(shootAt60 < healAt60);
    }

    [Fact]
    public void SuspicionProbabilities_ShootRisesAtEightyPercent()
    {
        var personality = new BotPersonality
        {
            Aggression = 70,
            RiskTolerance = 60,
            Patience = 20,
            Trust = 50,
            Confidence = 60
        };

        var shootAt80 = BotDecisionEngine.GetShootPlayProbability(80, personality);
        var shootAt50 = BotDecisionEngine.GetShootPlayProbability(50, personality);

        Assert.True(shootAt80 >= 0.70);
        Assert.True(shootAt80 > shootAt50 * 2);
    }

    [Fact]
    public void DecisionEngine_Heal_TargetsLikelyInfectedPlayer()
    {
        var humanBot = Guid.NewGuid();
        var suspect = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (humanBot, PlayerRole.Human, true),
            (suspect, PlayerRole.Zombie, true));
        state.Player(suspect).HasRevealedThisDay = true;
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        BotBeliefService.SetBelief(state, humanBot, suspect, 72);
        state.BotCognition[humanBot].Personality = new BotPersonality
        {
            Aggression = 50,
            RiskTolerance = 75,
            Patience = 20,
            Trust = 50,
            Confidence = 60
        };
        GameTestBuilder.AddCardsToHand(state, humanBot, TestCards.Heal);

        var engine = new BotDecisionEngine(new FakeCardRegistry(), new CardPlayValidator());
        var play = engine.TryFindCardPlay(state, humanBot, new Random(3));

        Assert.NotNull(play);
        Assert.Equal(suspect, play!.TargetUserId);
    }

    [Fact]
    public void DecisionEngine_DifferentPersonalities_ProduceDifferentPassRates()
    {
        var botAggressive = Guid.NewGuid();
        var botPassive = Guid.NewGuid();
        var opponent = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (botAggressive, PlayerRole.Human, true),
            (botPassive, PlayerRole.Human, true),
            (opponent, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        state.Players[1].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        GameTestBuilder.AddCardsToHand(state, botAggressive, TestCards.Shotgun);
        GameTestBuilder.AddCardsToHand(state, botPassive, TestCards.Shotgun);
        BotBeliefService.SetBelief(state, botAggressive, opponent, 70);
        BotBeliefService.SetBelief(state, botPassive, opponent, 70);

        state.BotCognition[botAggressive].Personality = new BotPersonality
        {
            Aggression = 95, RiskTolerance = 90, Patience = 5, Trust = 40, Confidence = 80
        };
        state.BotCognition[botPassive].Personality = new BotPersonality
        {
            Aggression = 10, RiskTolerance = 15, Patience = 95, Trust = 80, Confidence = 20
        };

        var engine = new BotDecisionEngine(new FakeCardRegistry(), new CardPlayValidator());
        var aggressivePasses = 0;
        var passivePasses = 0;
        for (var i = 0; i < 40; i++)
        {
            if (engine.ShouldPassBattle(state, botAggressive, new Random(i), opponent))
                aggressivePasses++;
            if (engine.ShouldPassBattle(state, botPassive, new Random(i + 1000), opponent))
                passivePasses++;
        }

        Assert.True(aggressivePasses < passivePasses);
    }

    [Fact]
    public void Belief_UpdatesFromPublicObservation_WithoutReadingHiddenRole()
    {
        var witnessBot = Guid.NewGuid();
        var infector = Guid.NewGuid();
        var victim = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (witnessBot, PlayerRole.Human, true),
            (infector, PlayerRole.Zombie, true),
            (victim, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));

        var before = BotBeliefService.GetBelief(state, witnessBot, infector);
        BotObservationRecorder.OnCardOutcome(
            state,
            infector,
            victim,
            new CardEffectResult
            {
                TelemetryKind = CardEffectTelemetryKind.ZombieInfectionSucceeded
            },
            "infect",
            [witnessBot]);

        Assert.True(BotBeliefService.GetBelief(state, witnessBot, infector) > before);
        Assert.Contains(victim, state.BotCognition[witnessBot].PubliclyKnownInfected);
    }

    [Fact]
    public void Memory_DecaysAtDayStart()
    {
        var botId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession((botId, PlayerRole.Human, true), (targetId, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        BotObservationRecorder.InitializeBots(state, new Random(1));

        BotMemoryService.AddMemory(state, botId, targetId, BotMemoryKind.RepeatedPass, 5);
        var before = BotMemoryService.GetMemorySuspicion(state, botId, targetId);

        BotObservationRecorder.OnDayStart(state);
        var after = BotMemoryService.GetMemorySuspicion(state, botId, targetId);

        Assert.True(after < before);
    }

    [Fact]
    public void InfectProbability_PowerZombieModifiers_IncreaseAttackChance()
    {
        var zombiePersonality = new BotPersonality
        {
            Aggression = 45,
            RiskTolerance = 40,
            Patience = 35
        };
        var powerPersonality = new BotPersonality
        {
            Aggression = 45,
            RiskTolerance = 40,
            Patience = 35,
            TalkManipulation = 25
        };
        BotPersonalityFactory.ApplyPowerZombieModifiers(powerPersonality);

        var zombieProb = BotDecisionEngine.GetInfectPlayProbability(zombiePersonality, PlayerRole.Zombie);
        var powerProb = BotDecisionEngine.GetInfectPlayProbability(powerPersonality, PlayerRole.PowerZombie);

        Assert.True(powerProb > zombieProb);
        Assert.True(powerProb >= 0.75);
    }

    [Fact]
    public void DecisionEngine_PowerZombie_AttacksHumanOpponent_InBattle()
    {
        var powerBot = Guid.NewGuid();
        var human = Guid.NewGuid();
        var state = GameTestBuilder.CreateSession(
            (powerBot, PlayerRole.PowerZombie, true),
            (human, PlayerRole.Human, true));
        state.Players[0].IsBot = true;
        state.CurrentDayEvent = DayEventType.NormalDay;
        BotObservationRecorder.InitializeBots(state, new Random(1));
        state.BotCognition[powerBot].Personality = new BotPersonality
        {
            Aggression = 80,
            RiskTolerance = 75,
            Patience = 15,
            TalkManipulation = 40
        };
        BotPersonalityFactory.ApplyPowerZombieModifiers(state.BotCognition[powerBot].Personality);
        GameTestBuilder.AddCardsToHand(state, powerBot, TestCards.PowerInfection);

        var engine = new BotDecisionEngine(new FakeCardRegistry(), new CardPlayValidator());
        var attacks = 0;
        for (var i = 0; i < 30; i++)
        {
            if (engine.TryFindCardPlay(state, powerBot, new Random(i), human) is not null)
                attacks++;
        }

        Assert.True(attacks >= 15, $"Expected frequent PowerZombie attacks, got {attacks}/30.");
    }

    [Fact]
    public async Task Simulation12Bots_WithBeliefSystem_ProducesMeaningfulActivity()
    {
        var options = new MatchSimulationOptions
        {
            PlayerCount = 12,
            MaxTurnsPerMatch = 50,
            UseSuspicionBasedVoting = true,
            MaxPassActionsPerDay = 0
        };

        var finishedBeforeCap = 0;
        var totalVoteEliminations = 0;
        var totalShotgun = 0;
        var totalHeal = 0;

        for (var seed = 1; seed <= 8; seed++)
        {
            options.RandomSeed = seed;
            var simulator = new MatchSimulator(options);
            var outcome = await simulator.RunSingleMatchAsync(options, new Random(seed));
            if (!outcome.IsStalemate)
                finishedBeforeCap++;
            totalVoteEliminations += outcome.VoteEliminations;
            totalShotgun += outcome.CardPlaysByEffect.GetValueOrDefault("shoot");
            totalHeal += outcome.CardPlaysByEffect.GetValueOrDefault("heal");
        }

        Assert.True(totalShotgun > 0, "Expected shotgun usage across seeds.");
        Assert.True(totalHeal > 0, "Expected heal usage across seeds.");
        Assert.True(totalVoteEliminations > 0, "Expected vote eliminations across seeds.");
        Assert.True(finishedBeforeCap >= 3, "Expected most matches to finish before day cap.");
    }
}
