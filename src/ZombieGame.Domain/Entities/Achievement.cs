namespace ZombieGame.Domain.Entities;

public class Achievement
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StatKey { get; set; } = string.Empty;
    public int Threshold { get; set; }
    public int Tier { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
