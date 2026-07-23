using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Guidance;

public sealed class ProgressionPlanner
{
    private readonly AppDatabase _db;
    private readonly SettingsService _settings;

    public ProgressionPlanner(AppDatabase db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public IReadOnlyList<CompanionTask> Generate(bool replaceOpen = false)
    {
        var s = _settings.Current;
        var tasks = BaseTasks(s).Concat(StageTasks(s)).Concat(FocusTasks(s)).ToList();
        var existing = _db.GetTasks(false, 500);
        var titles = existing.Select(x => x.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var task in tasks.Where(t => !titles.Contains(t.Title)))
        {
            task.Id = _db.UpsertTask(task);
        }
        return _db.GetTasks(false, 100);
    }

    private static IEnumerable<CompanionTask> BaseTasks(AppSettings s)
    {
        yield return Task("Claim rewards without binding mistakes", "Check Web Storage, Mail (B), Black Spirit's Safe, Challenge rewards, attendance, event/pass tabs, and expiration. Open class/character-selecting boxes only on the intended character.", "rewards", 100);
        yield return Task("Run Find My Item before buying or assuming something is lost", "Press Ctrl+F and search family inventories, storages, mounts, Central Market warehouse, and characters. Retrieve with the correct maid when eligible.", "inventory", 95);
        yield return Task("Protect rare and unfamiliar items", "Move uncertain [Event], exchange, outfit, enhancement, treasure, season, and coupon items into a named review storage instead of selling or opening them.", "safety", 98);
        yield return Task("Sync current notices and market data", "Open Live Data Center and sync official updates, active events, Pearl Shop notices, known issues, market data, and configured local exports.", "data", 90, "daily");
        yield return Task("Check expiring rewards", "Review coupon/Web Storage expiry, mail expiry, event end dates, Pearl Shop coupons, and season/pass claim deadlines.", "rewards", 94, "daily");
    }

    private static IEnumerable<CompanionTask> StageTasks(AppSettings s)
    {
        switch (s.AdventurerStage)
        {
            case AdventurerStage.BrandNew:
                yield return Task("Create or confirm a season character", "Use season progression unless a current official guide explicitly recommends otherwise. Avoid spending character-bound coupons until the class is confirmed.", "progression", 100);
                yield return Task("Follow the main quest and season pass together", "Advance both systems so inventory, pets, Naru/Tuvala materials, and account unlocks are not missed.", "progression", 96);
                yield return Task("Finish the Family Inventory unlock when available", "Family Inventory reduces consumable shuffling, but only eligible item categories can be placed there.", "account", 74);
                break;
            case AdventurerStage.SeasonEarly:
                yield return Task("Upgrade Naru into the current Tuvala path", "Use the official season gear guide and do not spend premium enhancement materials on low-tier season upgrades without a reason.", "progression", 98);
                yield return Task("Complete region and season pass milestones", "Claim each reward before graduation and keep season materials until every exchange is resolved.", "progression", 92);
                break;
            case AdventurerStage.SeasonLate:
                yield return Task("Audit graduation readiness", "Confirm season pass completion, claimed rewards, PEN Tuvala targets, remaining exchanges, and post-graduation conversion choices before graduating.", "progression", 100);
                break;
            case AdventurerStage.Graduated:
                yield return Task("Build one written gear roadmap", "Choose a current post-season path before spending Caphras, Crons, hammers, exchange coupons, or large silver reserves.", "progression", 98);
                yield return Task("Unlock Magnus storage if unfinished", "Linked storage access dramatically reduces inventory friction and improves maid utility.", "account", 86);
                break;
            case AdventurerStage.Returning:
                yield return Task("Do a returning-player inventory quarantine", "Do not sell legacy items. Put unfamiliar gear, crystals, coupons, event items, and enhancement materials into review storage, then decode them one by one.", "safety", 100);
                yield return Task("Read current patch and known-issues summaries", "Major systems and gear paths may have changed. Sync before following an old guide or spending rare resources.", "data", 98);
                break;
            case AdventurerStage.LifeSkillFocused:
                yield return Task("Pick one primary life-skill loop", "Optimize mastery gear, storage, workers, energy, tools, and processing around one profit loop before spreading resources across every life skill.", "lifeskill", 90);
                break;
            case AdventurerStage.Midgame:
                yield return Task("Prioritize the next account-wide breakpoint", "Compare AP/DP brackets, journals, crystals, artifacts/lightstones, cups, hearts, and guaranteed paths before raw enhancement gambling.", "progression", 94);
                break;
            case AdventurerStage.Endgame:
                yield return Task("Require expected-value checks for every enhancement", "Record stack, cron cost, replacement cost, pity progress, and market alternative before tapping.", "enhancement", 92);
                break;
        }
    }

    private static IEnumerable<CompanionTask> FocusTasks(AppSettings s)
    {
        if (s.PlayFocus == PlayFocus.PvE)
            yield return Task("Choose grind spots by total net value, not headline silver", "Include travel, buffs, Agris, loot-scroll use, trash price, rare-drop variance, survivability, and market liquidity.", "pve", 82);
        if (s.PlayFocus == PlayFocus.LifeSkills)
            yield return Task("Centralize materials by production chain", "Create named storages for raw materials, intermediate goods, imperial turn-ins, and tools to reduce UI archaeology.", "lifeskill", 80);
        if (s.PlayFocus == PlayFocus.Collecting)
            yield return Task("Create protected collections storage", "Separate treasure pieces, event collectibles, outfit boxes, furniture, titles/coupons, and one-time rewards from normal sale inventory.", "collection", 86);
        if (s.SpendingStyle is SpendingStyle.LowSpender or SpendingStyle.ValueBuyer)
            yield return Task("Use the Pearl evaluator before every purchase", "Prefer permanent family-wide utility, real discounts, and gaps you actually feel. Avoid RNG boxes, duplicate convenience, and consumables unless the value is explicitly justified.", "pearl", 92);
    }

    private static CompanionTask Task(string title, string detail, string category, int priority, string cadence = "once") => new()
    {
        Title = title,
        Detail = detail,
        Category = category,
        Priority = priority,
        Cadence = cadence,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
