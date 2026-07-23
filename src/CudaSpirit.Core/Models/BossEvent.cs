namespace CudaSpirit.Core.Models;

/// <summary>A scheduled world/field boss or timed event (night vendor, imperial reset, etc.).</summary>
public sealed class BossEvent
{
    public string Name { get; set; } = "";
    public BossKind Kind { get; set; } = BossKind.World;
    public Region Region { get; set; } = Region.NA;

    /// <summary>Next spawn/occurrence in the account's local time.</summary>
    public DateTimeOffset NextSpawn { get; set; }

    public TimeSpan TimeUntil => NextSpawn - DateTimeOffset.Now;
    public bool IsImminent => TimeUntil > TimeSpan.Zero && TimeUntil <= TimeSpan.FromMinutes(15);
    public bool IsLive => TimeUntil <= TimeSpan.Zero && TimeUntil > TimeSpan.FromMinutes(-15);

    public string Notes { get; set; } = "";
}
