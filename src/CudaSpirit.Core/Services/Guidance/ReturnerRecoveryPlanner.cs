using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Guidance;

/// <summary>
/// Produces a deterministic, low-risk recovery sequence for returning players. It intentionally
/// front-loads expiring rewards, inventory quarantine, and current-data checks before spending or
/// destructive actions.
/// </summary>
public sealed class ReturnerRecoveryPlanner
{
    private readonly AppDatabase _db;
    private readonly SettingsService _settings;

    public ReturnerRecoveryPlanner(AppDatabase db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public ReturnerPlan Build(ReturnerProfile profile, bool addToNavigator = false)
    {
        var steps = new List<RecoveryStep>();
        Add(steps, "Stabilize", "Sync the current game state", "Run Live Data Sync before following old guides, spending rare materials, or buying anything.", "Black Desert changes systems, rewards, classes, events, and offers frequently.", true, "data");
        Add(steps, "Stabilize", "Claim expiring rewards first", "Check website Web Storage and coupons, Mail (B), Black Spirit's Safe, Challenge rewards, attendance, event pages, season/pass interfaces, guild rewards, and Pearl coupon book. Send rewards only after checking target character and expiry.", "Expiry can destroy value before gear progression even begins.", true, "rewards");
        Add(steps, "Stabilize", "Freeze destructive actions", "For the first session, do not vendor, delete, open, melt, extract, register, enhance, or convert unfamiliar items. Create a REVIEW storage and place uncertain items there.", "Old items may now be exchange pieces, progression materials, rare collectibles, or character-selecting boxes.", true, "items");
        Add(steps, "Recover", "Find everything you already own", "Use Find My Item (Ctrl+F), character inventories, town storage, Central Market warehouse, mounts, Family Inventory, and transport. Search before buying replacements.", "Returning accounts often contain valuable items scattered across years of systems.", true, "items");
        Add(steps, "Recover", "Audit the current character without changing it", "Record class, level, AP/AAP/DP, equipped gear, crystals, artifacts/lightstones, journals, quest state, life skills, energy, contribution, tagged character, mounts, workers, and current currencies.", "A baseline prevents accidental downgrades and tells the advisor what is actually missing.", true, "dashboard");

        if (profile.UnsureAboutCurrentGear)
            Add(steps, "Decide", "Build a current gear roadmap", "Compare the equipped setup with current official guides and live market data. Pick one next breakpoint before using Crons, hammers, Caphras, failstacks, exchange coupons, or large silver reserves.", "Rare resources are easiest to waste when the old upgrade path is no longer current.", true, "gear");

        if (profile.UnsureAboutMainClass || profile.WantsFreshCharacter)
            Add(steps, "Decide", "Trial the next class before binding value", "Use the Class Match tool, create trial characters where available, test movement and a short PvE loop, then wait before assigning weight, inventory, outfits, exchange coupons, or other character-bound value.", "A class that feels good for thirty minutes is safer than a tier-list pick that becomes abandoned.", true, "recovery");

        if (profile.WantsFreshCharacter)
            Add(steps, "Decide", "Use the retirement checklist before deleting anything", "Run every critical check in Character Retirement. Do not delete the old character merely to free a slot until Pearl inventory, tagged/copy state, gear, crystals, mount inventory, quests, presets, and bound items are resolved.", "Character deletion is a poor inventory-management tool and can strand paid or irreplaceable value.", true, "recovery");

        if (!profile.HasSeasonCharacter)
            Add(steps, "Rebuild", "Check the current season or catch-up path", "Review the current official season and returning-player notices. Prefer account-wide catch-up systems before gambling enhancement materials or buying character-bound convenience.", "Season and catch-up systems are usually designed to compress years of obsolete progression.", false, "rewards");
        else
            Add(steps, "Rebuild", "Finish the active season deliberately", "Advance the current main quest and season pass together, claim every reward, resolve Tuvala exchanges, and run a graduation audit before graduating.", "Season rewards and exchanges can be missed when progression is rushed.", false, "navigator");

        Add(steps, "Rebuild", "Choose one seven-day objective", $"Use the next week for one outcome: {CleanGoal(profile.Goal)}. Limit side systems to actions that directly support it.", "BDO's interface creates urgency everywhere; one written objective prevents activity without progress.", false, "navigator");
        Add(steps, "Protect", "Activate the Pearl purchase firewall", "Set a monthly budget, use Pearl Shop Guard for every exact package, count bundle padding as zero, check free alternatives, and apply a 24-hour cooldown to non-expiring purchases.", "Discount labels and temporary friction can manufacture urgency without creating value.", true, "pearl");
        Add(steps, "Protect", "Save a stable weekly cockpit", "Keep only the relevant daily/weekly tasks, current event deadlines, one progression target, and one money-making loop. Hide the rest until needed.", "A smaller operating surface makes the in-game UI less capable of hijacking the session.", false, "navigator");

        for (var i = 0; i < steps.Count; i++) steps[i].Order = i + 1;

        if (addToNavigator)
            AddTasks(steps);

        _settings.Update(s =>
        {
            s.AdventurerStage = AdventurerStage.Returning;
            s.CurrentGoal = CleanGoal(profile.Goal);
        });

        return new ReturnerPlan
        {
            Headline = profile.MonthsAway >= 12 ? "Treat this as a controlled account recovery" : "Re-enter through a controlled seven-day reset",
            Summary = $"{steps.Count} ordered steps. The first session protects expiring rewards and existing value before any class, gear, or Pearl decision.",
            Steps = steps
        };
    }

    private void AddTasks(IEnumerable<RecoveryStep> steps)
    {
        var existing = _db.GetTasks(false, 500).Select(x => x.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps.Where(x => !existing.Contains(x.Title)))
        {
            _db.UpsertTask(new CompanionTask
            {
                Title = step.Title,
                Detail = step.Detail,
                Category = "returner-" + step.Phase.ToLowerInvariant(),
                Priority = step.Critical ? 100 - step.Order : 80 - Math.Min(step.Order, 20),
                Pinned = step.Critical,
                MetadataJson = $"{{\"route\":\"{step.ToolRoute}\",\"recovery\":true}}"
            });
        }
    }

    private static void Add(List<RecoveryStep> steps, string phase, string title, string detail, string why, bool critical, string route) =>
        steps.Add(new RecoveryStep { Phase = phase, Title = title, Detail = detail, Why = why, Critical = critical, ToolRoute = route });

    private static string CleanGoal(string value) => string.IsNullOrWhiteSpace(value) ? "Relearn the game safely" : value.Trim();
}
