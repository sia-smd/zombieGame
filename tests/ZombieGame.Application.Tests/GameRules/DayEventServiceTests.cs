namespace ZombieGame.Application.Tests.GameRules;

using Microsoft.Extensions.Options;
using ZombieGame.Application.GameRules.Events;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Enums;

public class DayEventServiceTests
{
    [Theory]
    [InlineData(0, DayEventType.NormalDay)]
    [InlineData(49, DayEventType.NormalDay)]
    [InlineData(50, DayEventType.SunnyDay)]
    [InlineData(79, DayEventType.SunnyDay)]
    [InlineData(80, DayEventType.Storm)]
    [InlineData(99, DayEventType.Storm)]
    public void PickRandomEvent_UsesWeightedBands(int roll, DayEventType expected)
    {
        var options = Options.Create(new DayEventOptions
        {
            NormalWeight = 50,
            SunnyWeight = 30,
            StormWeight = 20
        });
        var service = new DayEventService(options, new ScriptedRandom(roll));

        Assert.Equal(expected, service.PickRandomEvent());
    }

    [Theory]
    [InlineData(0, DayEventType.NormalDay)]
    [InlineData(9, DayEventType.NormalDay)]
    [InlineData(10, DayEventType.SunnyDay)]
    [InlineData(29, DayEventType.SunnyDay)]
    [InlineData(30, DayEventType.Storm)]
    [InlineData(59, DayEventType.Storm)]
    public void PickRandomEvent_UsesConfiguredWeightsThatDoNotSumTo100(int roll, DayEventType expected)
    {
        var options = Options.Create(new DayEventOptions
        {
            NormalWeight = 10,
            SunnyWeight = 20,
            StormWeight = 30
        });
        var service = new DayEventService(options, new ScriptedRandom(roll));

        Assert.Equal(expected, service.PickRandomEvent());
    }

    [Fact]
    public void Weights_TotalWeightIsSumOfConfiguredBands()
    {
        var options = new DayEventOptions
        {
            NormalWeight = 50,
            SunnyWeight = 30,
            StormWeight = 20
        };

        Assert.Equal(100, options.TotalWeight);
        Assert.Equal(options.NormalWeight + options.SunnyWeight + options.StormWeight, options.TotalWeight);
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _values;

        public ScriptedRandom(params int[] values) => _values = new Queue<int>(values);

        public override int Next() => _values.Dequeue();

        public override int Next(int maxValue) => _values.Dequeue();

        public override int Next(int minValue, int maxValue) => _values.Dequeue();
    }
}
