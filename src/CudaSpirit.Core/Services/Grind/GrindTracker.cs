using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Grind;

/// <summary>
/// Tracks a live grind session: start it when you arrive at a spot, tick trash counts (or let the
/// overlay increment them), and it computes live silver/hour. Persists finished sessions to the log.
/// Trash counts are entered by the user or estimated from zone rates - never scraped from the game.
/// </summary>
public sealed class GrindTracker
{
    private readonly AppDatabase _db;
    private readonly double _taxRate;

    public string? Zone { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public int TrashCount { get; private set; }
    public long TrashUnitValue { get; set; }
    public long RareSilver { get; private set; }
    public bool IsRunning => StartedAt is not null;

    public GrindTracker(AppDatabase db, double taxRate = 0.845)
    {
        _db = db;
        _taxRate = taxRate;
    }

    public void Start(string zone, long trashUnitValue)
    {
        Zone = zone;
        TrashUnitValue = trashUnitValue;
        StartedAt = DateTimeOffset.UtcNow;
        TrashCount = 0;
        RareSilver = 0;
    }

    public void AddTrash(int n = 1) => TrashCount += Math.Max(0, n);
    public void AddRareSilver(long silver) => RareSilver += Math.Max(0, silver);

    public TimeSpan Elapsed => StartedAt is { } s ? DateTimeOffset.UtcNow - s : TimeSpan.Zero;

    /// <summary>Gross silver so far (trash * unit value + rares), before tax.</summary>
    public long GrossSilver => (long)TrashCount * TrashUnitValue + RareSilver;

    /// <summary>Net silver after marketplace tax on tradeable loot.</summary>
    public long NetSilver => (long)(GrossSilver * _taxRate);

    public long SilverPerHour => Elapsed.TotalHours > 0.001
        ? (long)(NetSilver / Elapsed.TotalHours)
        : 0;

    /// <summary>Stop the session and persist it. Returns the saved log.</summary>
    public GrindLog Stop()
    {
        var log = new GrindLog
        {
            Zone = Zone ?? "Unknown",
            StartedAt = StartedAt ?? DateTimeOffset.UtcNow,
            EndedAt = DateTimeOffset.UtcNow,
            TrashCount = TrashCount,
            SilverEarned = NetSilver
        };
        _db.AddGrindLog(log);
        StartedAt = null;
        Zone = null;
        return log;
    }
}
