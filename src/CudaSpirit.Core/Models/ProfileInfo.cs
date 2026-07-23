namespace CudaSpirit.Core.Models;

/// <summary>
/// Public adventurer-profile data pulled from the official Black Desert website
/// (the same source Garmoth uses). This is public web data the player opted into showing -
/// it is NOT read from the game client, its memory, or its traffic.
/// Only a combined Max Gear Score is published (not a separate AP/DP split).
/// </summary>
public sealed class ProfileInfo
{
    public string FamilyName { get; set; } = "";
    public Region Region { get; set; } = Region.NA;

    public string MainClass { get; set; } = "";
    public string MainCharacterName { get; set; } = "";
    public int MainLevel { get; set; }

    /// <summary>Combined "Max Gear Score" (max of main-hand and awakening AP+DP). 0 if hidden.</summary>
    public int GearScore { get; set; }
    public int ContributionPoints { get; set; }
    public int Energy { get; set; }
    public string? Guild { get; set; }

    /// <summary>The opaque profileTarget token this profile was fetched with (for re-fetching).</summary>
    public string ProfileToken { get; set; } = "";

    public DateTimeOffset Retrieved { get; set; } = DateTimeOffset.UtcNow;
}
