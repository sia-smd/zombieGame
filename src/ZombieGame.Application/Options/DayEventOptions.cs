namespace ZombieGame.Application.Options;

public class DayEventOptions
{
    public const string SectionName = "DayEvents";

    public int NormalWeight { get; set; } = 50;
    public int SunnyWeight { get; set; } = 30;
    public int StormWeight { get; set; } = 20;

    public int TotalWeight => NormalWeight + SunnyWeight + StormWeight;
}
