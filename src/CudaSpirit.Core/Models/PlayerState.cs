namespace CudaSpirit.Core.Models;

/// <summary>
/// The complete live picture of the account that the AI advisor is given before every call.
/// Populated from ToS-safe sources (manual entry, official APIs, or a user-triggered screenshot),
/// never from reading the game's memory or its network traffic.
/// </summary>
public sealed class PlayerState
{
    public string CharacterName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string? Guild { get; set; }
    public int Level { get; set; }
    public Region Region { get; set; } = Region.NA;

    // Combat stats
    public int Ap { get; set; }
    public int Awakening { get; set; }
    public int Dp { get; set; }

    /// <summary>
    /// Combined gear score reported by the official adventurer profile, when synced. The profile
    /// only publishes this single number (not an AP/DP split), so it takes precedence for display
    /// and for the AI context. AP/DP below are optional manual refinements for bracket math.
    /// </summary>
    public int? ReportedGearScore { get; set; }

    /// <summary>max(AP, Awakening) + DP, or the profile-reported score when AP/DP haven't been entered.</summary>
    public int GearScore
    {
        get
        {
            int computed = Math.Max(Ap, Awakening) + Dp;
            return computed > 0 ? computed : (ReportedGearScore ?? 0);
        }
    }

    // Economy
    public long Silver { get; set; }
    public int ContributionPoints { get; set; }
    public int EnergyCurrent { get; set; }
    public int EnergyMax { get; set; }

    // Session vitals (only if a live provider supplies them; otherwise 0)
    public int HpPercent { get; set; }
    public int MpPercent { get; set; }
    public int WeightPercent { get; set; }

    public List<string> ActiveBuffs { get; set; } = new();
    public List<GearItem> Gear { get; set; } = new();

    /// <summary>Name of the current grind spot, if known/selected.</summary>
    public string? GrindZone { get; set; }

    /// <summary>How this snapshot was produced - shown to the AI so it can weigh confidence.</summary>
    public string Source { get; set; } = "manual";

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
