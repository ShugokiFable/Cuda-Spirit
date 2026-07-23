using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Knowledge;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Guidance;

/// <summary>
/// Conservative, explainable first-pass item triage. It intentionally prefers "keep / verify"
/// over irreversible advice when the tooltip or binding state is incomplete.
/// </summary>
public sealed class ItemSafetyAdvisor
{
    private readonly AppDatabase _db;
    private readonly KnowledgeRetriever _knowledge;
    private readonly SettingsService _settings;

    private sealed record Rule(string Id, string Pattern, GuidanceVerdict Verdict, string Why, string Location, string Warning = "");

    private static readonly Rule[] Rules =
    {
        new("rng-box", "adventure box|mystery box|pouch of fortune|rarit(?:y|ies)|random|chance box|shakatu.*box", GuidanceVerdict.UseLater,
            "This is a random-reward container. Opening is irreversible and its expected value is usually lower than the headline jackpot.",
            "Black Spirit's Safe or a dedicated event-reward storage", "Check the official probability page and expiration before opening."),
        new("character-coupon", "inventory.*expansion|weight limit|weight expansion|underwear|horse calling horn|celestial.*horn|skill preset", GuidanceVerdict.Stop,
            "This commonly becomes character-specific when used or opened.", "Keep unopened in a family-accessible storage until the main character is certain",
            "Do not use this on a temporary season alt or an undecided class."),
        new("outfit", "outfit box|costume box|classic outfit|premium outfit|choose your.*outfit", GuidanceVerdict.Stop,
            "Outfit containers can lock the result to the class or character that opens them.", "Keep the unopened box in storage",
            "Open only while logged into the intended permanent character and after verifying the class list."),
        new("exchange", "weapon exchange coupon|combat.*exp.*exchange|skill.*exp.*exchange|name change coupon|appearance change coupon", GuidanceVerdict.Keep,
            "Exchange and identity coupons are rare, account-limited tools that are expensive to replace.", "Family-accessible storage or Black Spirit's Safe", "Use only with a written before/after plan."),
        new("enhancement-premium", "j.?s hammer|hammer of loyalty|origin of dark hunger|advice of valks|valks.? cry|cron stone|memory fragment|artisan.?s memory|naderr", GuidanceVerdict.Keep,
            "This is enhancement infrastructure. It often has far more value later at higher gear tiers.", "Central enhancement storage; keep failstack items organized", "Do not burn it on low-tier gear without checking the current upgrade path."),
        new("caphras", "caphras stone|ancient spirit dust", GuidanceVerdict.Keep,
            "Caphras materials feed expensive long-term progression and are easy to regret selling.", "Central Market warehouse or main storage", "Verify current gear roadmap before extracting or selling."),
        new("season", "time-filled black stone|tuvala|season.*exchange|season.*coupon|fughar|beginner black stone", GuidanceVerdict.Keep,
            "Season materials are progression-gated and may stop being useful only after the relevant season path is complete.", "Season character or season-material storage", "Do not delete or vendor before graduation and pass rewards are fully resolved."),
        new("treasure", "compass part|map piece|infinite potion|ornette|odores|archaeologist|la orzeca|nouverikant|treasure", GuidanceVerdict.Keep,
            "Treasure components can represent many hours of account progress and may have special movement restrictions.", "Locked treasure storage", "Never vendor an unidentified treasure-looking piece."),
        new("boss-heart", "garmoth.?s heart|karanda.?s heart|vell.?s heart|inverted heart|refined black stone", GuidanceVerdict.Keep,
            "Rare upgrade materials and hearts are strategic progression pieces.", "Main enhancement storage", "Check extraction and reform consequences before use."),
        new("event-expiry", "\\[event\\]|event item|login reward|coupon reward", GuidanceVerdict.UseLater,
            "Event items often have expiration, claim-window, binding, or exchange restrictions.", "Black Spirit's Safe until the tooltip is reviewed", "Check both item expiration and mail/Web Storage expiration."),
        new("trash", "trash loot|miscellaneous loot|vendors would buy|ordinary material", GuidanceVerdict.Sell,
            "The tooltip appears to identify vendor or routine loot rather than a strategic item.", "Sell to the appropriate NPC or use the item-selling UI", "Confirm there is no exchange icon, crafting purpose, or event tag."),
    };

    public ItemSafetyAdvisor(AppDatabase db, KnowledgeRetriever knowledge, SettingsService settings)
    {
        _db = db;
        _knowledge = knowledge;
        _settings = settings;
    }

    public ItemGuidanceResult Evaluate(ItemGuidanceRequest request)
    {
        var searchable = $"{request.ItemName} {request.TooltipText}".Trim();
        var matched = Rules.Where(r => Regex.IsMatch(searchable, r.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
        var primary = matched
            .OrderBy(r => RiskRank(r.Verdict))
            .FirstOrDefault();

        var bindingWarning = request.Binding switch
        {
            ItemBinding.CharacterBound => "Character-bound: normal family transfer methods will not work.",
            ItemBinding.FamilyBound => "Family-bound: generally usable across the family only through permitted storage/maid systems.",
            ItemBinding.Expiring => "Expiring: record the expiration before deciding to save it.",
            ItemBinding.AccountLimited => "Account-limited: replacement may be impossible or costly.",
            _ => "Binding is unconfirmed. Read the exact tooltip line before opening, equipping, registering, or using it."
        };

        ItemGuidanceResult result;
        if (primary is not null)
        {
            result = new ItemGuidanceResult
            {
                Verdict = primary.Verdict,
                Headline = VerdictHeadline(primary.Verdict),
                Explanation = primary.Why,
                BestLocation = primary.Location,
                BindingWarning = string.IsNullOrWhiteSpace(primary.Warning) ? bindingWarning : $"{primary.Warning} {bindingWarning}",
                TransferAdvice = request.Binding == ItemBinding.CharacterBound
                    ? "Keep it on this character unless the tooltip or support documentation names a specific transfer item."
                    : "Use Find My Item (Ctrl+F), town storage, Central Market warehouse, Family Inventory for eligible consumables, or maids as appropriate.",
                ConfidencePercent = Math.Clamp(72 + matched.Count * 6 + (request.Binding != ItemBinding.Unknown ? 8 : 0), 0, 96),
                MatchedRules = matched.Select(x => x.Id).ToList(),
                BeforeYouAct = BuildChecklist(request, primary)
            };
        }
        else
        {
            var hits = _knowledge.Search(request.ItemName, _settings.Current.Region, 4);
            var summary = hits.FirstOrDefault()?.Record.Summary;
            result = new ItemGuidanceResult
            {
                Verdict = GuidanceVerdict.Unknown,
                Headline = "Do not make an irreversible move yet",
                Explanation = string.IsNullOrWhiteSpace(summary)
                    ? "No high-confidence safety rule matched this item. That is not evidence that it is safe to sell or open."
                    : $"The knowledge database found related material, but not enough structured evidence for an automatic sell/open decision: {summary}",
                BestLocation = "Temporary review storage",
                TransferAdvice = "Use Ctrl+F Find My Item and keep it in a named storage until its use and binding are confirmed.",
                BindingWarning = bindingWarning,
                ConfidencePercent = hits.Count > 0 ? 48 : 25,
                BeforeYouAct = new[]
                {
                    "Read the Bind on Pickup / Character Bound / Family Bound line.",
                    "Check expiration and whether opening chooses a class, item, or enhancement path.",
                    "Search the exact full item name in the Live Data Center or ask the AI Advisor.",
                    "Do not delete, vendor, melt, extract, or open it while uncertain."
                }
            };
        }

        _db.AddItemDecision(new ItemDecisionHistory
        {
            ItemName = string.IsNullOrWhiteSpace(request.ItemName) ? "Unnamed item" : request.ItemName.Trim(),
            Verdict = result.Verdict.ToString(),
            Reason = result.Explanation,
            Binding = request.Binding.ToString()
        });
        return result;
    }

    private static IReadOnlyList<string> BuildChecklist(ItemGuidanceRequest request, Rule primary)
    {
        var list = new List<string>
        {
            "Confirm the exact item name, including [Event], [Season], class, and box suffixes.",
            "Read binding, expiration, deletion, and marketplace lines in the tooltip.",
            "Check whether using or opening the item selects the current character."
        };
        if (request.IsSeasonCharacter) list.Add("Confirm the item survives season graduation or is meant to be consumed before graduation.");
        if (primary.Verdict is GuidanceVerdict.Sell) list.Add("Search NPC Exchange and Central Market before accepting vendor value.");
        if (primary.Verdict is GuidanceVerdict.Stop or GuidanceVerdict.UseLater) list.Add("Take a screenshot of the tooltip before acting.");
        return list;
    }

    private static int RiskRank(GuidanceVerdict verdict) => verdict switch
    {
        GuidanceVerdict.Stop => 0,
        GuidanceVerdict.Keep => 1,
        GuidanceVerdict.UseLater => 2,
        GuidanceVerdict.Store => 3,
        GuidanceVerdict.Transfer => 4,
        GuidanceVerdict.Conditional => 5,
        GuidanceVerdict.UseNow => 6,
        GuidanceVerdict.Sell => 7,
        _ => 8
    };

    private static string VerdictHeadline(GuidanceVerdict verdict) => verdict switch
    {
        GuidanceVerdict.Stop => "STOP: verify the target character before using this",
        GuidanceVerdict.Keep => "KEEP: this is strategically valuable",
        GuidanceVerdict.Store => "STORE: useful, but not necessarily now",
        GuidanceVerdict.Transfer => "TRANSFER: move it with the safest eligible method",
        GuidanceVerdict.UseNow => "USE: current context supports using it now",
        GuidanceVerdict.UseLater => "SAVE: use it only after checking conditions",
        GuidanceVerdict.Sell => "SELLABLE: confirm the tooltip one last time",
        GuidanceVerdict.Conditional => "CONDITIONAL: value depends on your progression path",
        _ => "UNKNOWN: do not act yet"
    };
}
