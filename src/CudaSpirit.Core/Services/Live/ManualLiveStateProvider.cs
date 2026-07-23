using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Live;

/// <summary>
/// Default <see cref="ILiveStateProvider"/>. The state is whatever the user has entered/imported,
/// enriched with owned gear from the local database. A vision parse of a gear screenshot, or a
/// future community-API import, can call <see cref="Publish"/> to refresh it.
/// </summary>
public sealed class ManualLiveStateProvider : ILiveStateProvider
{
    private readonly AppDatabase _db;
    private PlayerState _current = new();

    public ManualLiveStateProvider(AppDatabase db)
    {
        _db = db;
        _current = BuildFromDb();
    }

    public PlayerState Current => _current;
    public event EventHandler<PlayerState>? StateChanged;

    public void Publish(PlayerState state)
    {
        state.CapturedAt = DateTimeOffset.UtcNow;
        _current = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>Re-read owned gear from the DB and recompute AP/DP totals onto the current state.</summary>
    public void RefreshGear()
    {
        var next = Clone(_current);
        next.Gear = _db.GetGear().ToList();
        ApplyGearTotals(next);
        Publish(next);
    }

    private PlayerState BuildFromDb()
    {
        var s = new PlayerState { Source = "manual", Gear = _db.GetGear().ToList() };
        ApplyGearTotals(s);
        return s;
    }

    private static void ApplyGearTotals(PlayerState s)
    {
        var equipped = s.Gear.Where(g => g.Equipped).ToList();
        if (equipped.Count == 0) return;

        // Only override if the user hasn't typed explicit AP/DP.
        if (s.Ap == 0) s.Ap = equipped.Where(g => g.Slot is GearSlot.MainWeapon or GearSlot.Sub).Sum(g => g.Ap);
        if (s.Awakening == 0) s.Awakening = equipped.Where(g => g.Slot == GearSlot.Awakening).Sum(g => g.Ap);
        if (s.Dp == 0) s.Dp = equipped.Sum(g => g.Dp);
    }

    private static PlayerState Clone(PlayerState s) => new()
    {
        CharacterName = s.CharacterName,
        ClassName = s.ClassName,
        Level = s.Level,
        Region = s.Region,
        Ap = s.Ap,
        Awakening = s.Awakening,
        Dp = s.Dp,
        Silver = s.Silver,
        ContributionPoints = s.ContributionPoints,
        EnergyCurrent = s.EnergyCurrent,
        EnergyMax = s.EnergyMax,
        HpPercent = s.HpPercent,
        MpPercent = s.MpPercent,
        WeightPercent = s.WeightPercent,
        ActiveBuffs = new List<string>(s.ActiveBuffs),
        Gear = new List<GearItem>(s.Gear),
        GrindZone = s.GrindZone,
        Source = s.Source
    };
}
