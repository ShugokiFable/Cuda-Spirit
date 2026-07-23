namespace CudaSpirit.Core.Models;

/// <summary>A single equipped or owned item. Values are user-entered, imported, or vision-parsed.</summary>
public sealed class GearItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public GearSlot Slot { get; set; } = GearSlot.Other;
    public EnhanceKind Kind { get; set; } = EnhanceKind.Weapon;
    public EnhanceGrade Grade { get; set; } = EnhanceGrade.Base;

    /// <summary>Caphras level 0..20 (0 = none).</summary>
    public int Caphras { get; set; }

    /// <summary>Contribution to combined AP (weapons) - informational.</summary>
    public int Ap { get; set; }

    /// <summary>Contribution to DP (armor/accessory).</summary>
    public int Dp { get; set; }

    /// <summary>Optional central-market item id for price lookups.</summary>
    public long? MarketItemId { get; set; }

    /// <summary>Cached market unit value in silver (0 if unknown / untradeable).</summary>
    public long MarketValue { get; set; }

    public bool Equipped { get; set; }

    public override string ToString() => $"{Grade} {Name}".Trim();
}
