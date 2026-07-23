namespace CudaSpirit.Core.Models;

/// <summary>A grind spot with the numbers the silver/hour estimator needs.</summary>
public sealed class GrindZone
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Territory { get; set; } = "";

    /// <summary>Recommended combined AP for comfortable clears.</summary>
    public int RecommendedAp { get; set; }
    public int RecommendedDp { get; set; }

    /// <summary>Silver per trash loot at current market (cached).</summary>
    public long TrashValue { get; set; }

    /// <summary>Typical trash loot per hour at recommended gear.</summary>
    public int TrashPerHour { get; set; }

    /// <summary>Estimated extra silver/hour from rare drops (boss gear, mats, etc.).</summary>
    public long RareSilverPerHour { get; set; }

    public string Notes { get; set; } = "";

    /// <summary>Naive expected silver/hour before agris/buffs/marketplace tax.</summary>
    public long BaseSilverPerHour => (long)TrashPerHour * TrashValue + RareSilverPerHour;
}

/// <summary>A single logged grind session, persisted for the silver/hour history.</summary>
public sealed class GrindLog
{
    public long Id { get; set; }
    public string Zone { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int TrashCount { get; set; }
    public long SilverEarned { get; set; }

    public TimeSpan Duration => EndedAt - StartedAt;
    public long SilverPerHour => Duration.TotalHours > 0.001
        ? (long)(SilverEarned / Duration.TotalHours)
        : 0;
}
