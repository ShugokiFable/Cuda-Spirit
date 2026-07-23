namespace CudaSpirit.Core.Models;

/// <summary>A central-market item snapshot.</summary>
public sealed class MarketItem
{
    public long ItemId { get; set; }
    public int Sid { get; set; }               // enhancement sub-id (0 = base)
    public string Name { get; set; } = "";
    public int Grade { get; set; }             // 0..4 rarity color
    public long BasePrice { get; set; }        // last-sold unit price in silver
    public long CurrentStock { get; set; }
    public long TotalTrades { get; set; }
    public DateTimeOffset Retrieved { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One point on a price history curve.</summary>
public readonly record struct PricePoint(DateTimeOffset Date, long Price);

/// <summary>A user-configured price watch. When conditions match, the app raises an alert -
/// it never buys automatically (that would be botting).</summary>
public sealed class PriceAlert
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public int Sid { get; set; }
    public string ItemName { get; set; } = "";

    /// <summary>Fire when the market price is at or below this value.</summary>
    public long TargetPrice { get; set; }

    public bool NotifyOnRestock { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastTriggered { get; set; }
}
