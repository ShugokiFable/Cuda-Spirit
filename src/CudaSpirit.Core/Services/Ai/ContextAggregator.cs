using System.Text;
using System.Text.Json;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Live;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Knowledge;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Ai;

/// <summary>
/// Builds the system message injected before every AI call so the advisor is never blind. It
/// compresses the current <see cref="PlayerState"/> - silver, AP/DP, equipped gear, buffs, grind
/// zone - into a compact JSON payload plus a short instruction block describing who the assistant is.
/// </summary>
public sealed class ContextAggregator
{
    private static readonly JsonSerializerOptions Compact = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILiveStateProvider _live;
    private readonly SettingsService _settings;
    private readonly KnowledgeRetriever _knowledge;
    private readonly AppDatabase _db;

    public ContextAggregator(ILiveStateProvider live, SettingsService settings, KnowledgeRetriever knowledge, AppDatabase db)
    {
        _live = live;
        _settings = settings;
        _knowledge = knowledge;
        _db = db;
    }

    /// <summary>Full system prompt: persona + rules + live state + query-relevant local knowledge.</summary>
    public string BuildSystemPrompt(string userPrompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Cuda Spirit, an expert Black Desert Online companion - part min-maxer, part Black Spirit.");
        sb.AppendLine("Give concrete, current-patch advice on onboarding, rewards, item safety, storage and transfer, progression, gear, enhancement, grind spots, workers, crystals, market decisions, and Pearl Shop value.");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Use the PLAYER_CONTEXT JSON below as ground truth about what the user owns and is doing. Do not ask for info already present there.");
        sb.AppendLine("- Prefer specific numbers when trustworthy. Show the reasoning briefly, then a clear recommendation and exact in-game menu path when known.");
        sb.AppendLine("- For unknown items, boxes, coupons, outfits, exchange items, or rewards, fail safe: tell the user not to open, equip, sell, delete, melt, extract, or register it until binding, expiry, and contents are verified.");
        sb.AppendLine("- Separate family-wide, character-bound, account-limited, expiring, marketable, storage-restricted, and season-only behavior. Never assume an item can be moved.");
        sb.AppendLine("- Pearl Shop advice must account for the user's budget, spending style, owned conveniences, RNG, character binding, duplicate value, free alternatives, and current official sale dates.");
        sb.AppendLine("- Prioritize expiring claims and irreversible decisions before routine optimization.");
        sb.AppendLine("- Never advise anything that violates the game's terms (no botting, macros, memory editing, RMT). Recommend legitimate in-game actions only.");
        sb.AppendLine("- Treat source timestamps and confidence as part of the answer. Never call old or failed-sync data current.");
        sb.AppendLine("- If context is missing or marked low-confidence, state the limitation and ask the user to sync/import data rather than guessing.");
        sb.AppendLine("- Route advice is advisory only. Never provide client injection, input automation, botting, anti-cheat evasion, packet manipulation, or unattended play instructions.");
        sb.AppendLine();
        sb.AppendLine("PLAYER_CONTEXT (compressed JSON):");
        sb.AppendLine(BuildContextJson());
        sb.AppendLine();
        sb.AppendLine(_knowledge.BuildAdvisorContext(
            userPrompt,
            _settings.Current.Region,
            _settings.Current.KnowledgeMaxRecords,
            _settings.Current.KnowledgeMaxCharacters));
        return sb.ToString();
    }

    /// <summary>Just the compressed context payload (useful for logging/caching keys).</summary>
    public string BuildContextJson()
    {
        var s = _live.Current;
        var prefs = _settings.Current;
        var openTasks = _db.GetTasks(false, 8).Select(t => new
        {
            t.Title,
            t.Detail,
            t.Category,
            t.Priority,
            due = t.DueAt?.ToString("u")
        }).ToList();
        var recentItems = _db.GetItemDecisionHistory(5).Select(x => new
        {
            item = x.ItemName,
            x.Verdict,
            x.Binding,
            x.Reason
        }).ToList();
        var recentPearl = _db.GetPearlEvaluationHistory(5).Select(x => new
        {
            offer = x.OfferName,
            x.Score,
            x.Verdict,
            x.PricePearls
        }).ToList();
        var payload = new
        {
            captured = s.CapturedAt.ToString("u"),
            source = s.Source,
            region = prefs.Region.ToString(),
            preferences = new
            {
                stage = prefs.AdventurerStage.ToString(),
                focus = prefs.PlayFocus.ToString(),
                spending = prefs.SpendingStyle.ToString(),
                mainClass = string.IsNullOrWhiteSpace(prefs.MainClass) ? null : prefs.MainClass,
                goal = prefs.CurrentGoal,
                weeklyHours = prefs.WeeklyPlayHours,
                monthlyPearlBudget = prefs.MonthlyPearlBudget,
                conveniences = new
                {
                    magnusStorage = prefs.HasMagnusStorage,
                    storageMaid = prefs.HasStorageMaid,
                    transactionMaid = prefs.HasTransactionMaid,
                    tent = prefs.HasTent
                }
            },
            actionQueue = openTasks.Count > 0 ? openTasks : null,
            recentItemDecisions = recentItems.Count > 0 ? recentItems : null,
            recentPearlEvaluations = recentPearl.Count > 0 ? recentPearl : null,
            family = string.IsNullOrWhiteSpace(s.FamilyName) ? null : s.FamilyName,
            guild = string.IsNullOrWhiteSpace(s.Guild) ? null : s.Guild,
            character = string.IsNullOrWhiteSpace(s.CharacterName) ? null : s.CharacterName,
            @class = string.IsNullOrWhiteSpace(s.ClassName) ? null : s.ClassName,
            level = s.Level == 0 ? (int?)null : s.Level,
            ap = s.Ap == 0 ? (int?)null : s.Ap,
            awakening = s.Awakening == 0 ? (int?)null : s.Awakening,
            dp = s.Dp == 0 ? (int?)null : s.Dp,
            gearscore = s.GearScore == 0 ? (int?)null : s.GearScore,
            silver = s.Silver,
            cp = s.ContributionPoints,
            energy = s.EnergyMax > 0 ? $"{s.EnergyCurrent}/{s.EnergyMax}" : null,
            grindZone = s.GrindZone,
            buffs = s.ActiveBuffs.Count > 0 ? s.ActiveBuffs : null,
            vitals = s.HpPercent + s.MpPercent + s.WeightPercent > 0
                ? new { hp = s.HpPercent, mp = s.MpPercent, weight = s.WeightPercent }
                : null,
            gear = s.Gear
                .Where(g => g.Equipped)
                .Select(g => new
                {
                    slot = g.Slot.ToString(),
                    name = g.Name,
                    grade = g.Grade.ToString(),
                    caphras = g.Caphras == 0 ? (int?)null : g.Caphras,
                    ap = g.Ap == 0 ? (int?)null : g.Ap,
                    dp = g.Dp == 0 ? (int?)null : g.Dp
                })
                .ToList()
        };

        return JsonSerializer.Serialize(payload, Compact);
    }
}
