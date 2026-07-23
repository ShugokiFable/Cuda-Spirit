using System.Text.RegularExpressions;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Guidance;

/// <summary>
/// Conservative purchase evaluator. It deliberately treats unverifiable discounts, countdowns,
/// random outcomes, consumable padding, character binding, and duplicated convenience as risk.
/// </summary>
public sealed class PearlShopAdvisor
{
    private readonly AppDatabase _db;
    private readonly SettingsService _settings;

    public PearlShopAdvisor(AppDatabase db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public PearlOfferResult Evaluate(PearlOfferInput input)
    {
        var name = input.Name.Trim();
        var searchable = $"{name}\n{input.ContentsText}".Trim();
        var settings = _settings.Current;
        var score = 45;
        var positives = new List<string>();
        var warnings = new List<string>();
        var discount = input.OriginalPricePearls > input.PricePearls && input.OriginalPricePearls > 0
            ? 1d - input.PricePearls / (double)input.OriginalPricePearls
            : 0d;

        if (input.PermanentFamilyWide)
        {
            score += 22;
            positives.Add("Permanent, family-wide utility keeps value across class changes.");
        }

        if (discount > 0)
        {
            var points = (int)Math.Round(Math.Min(18, discount * 28));
            score += points;
            positives.Add($"Entered price arithmetic shows {discount:P0} off the entered original price.");
            warnings.Add("An advertised original price is not proof of value. Count only contents you would otherwise buy.");
        }
        else if (Regex.IsMatch(searchable, @"\b\d{1,2}\s*%\s*(?:off|discount)", RegexOptions.IgnoreCase))
        {
            score -= 5;
            warnings.Add("The description claims a percentage discount, but no verifiable original price was entered.");
        }

        if (input.RandomContents)
        {
            score -= 38;
            warnings.Add("Random contents: judge probability-weighted expected value, not the jackpot image.");
        }
        if (input.CharacterBound)
        {
            score -= 18;
            warnings.Add("Character-specific value is easy to strand on an abandoned class.");
            if (string.IsNullOrWhiteSpace(settings.MainClass))
            {
                score -= 8;
                warnings.Add("No confirmed main class is saved. Keep character-bound convenience unopened.");
            }
        }
        if (input.HasFreeAlternative)
        {
            score -= 16;
            warnings.Add("A free or in-game alternative exists. Compare the exact friction saved, not merely access to the feature.");
        }
        if (input.AlreadyOwnEquivalent)
        {
            score -= 26;
            warnings.Add("You already own equivalent utility; duplicate value is sharply lower.");
        }
        if (input.MostlyConsumablesOrPadding)
        {
            score -= 22;
            warnings.Add("Most of the sticker value is consumable or bundle padding. Assign zero value to anything you would not buy alone.");
        }
        if (input.WouldBuyContentsIndividually)
        {
            score += 10;
            positives.Add("You stated that every included item has direct value to you, reducing bundle-padding risk.");
        }

        AddNameRules(searchable, settings, ref score, positives, warnings);
        AddBudgetAndCooldownRules(input, settings, ref score, positives, warnings);

        var shieldUntil = settings.PearlSpendingFreezeUntilUtc;
        var shieldActive = input.PricePearls > 0
            && shieldUntil.HasValue
            && shieldUntil.Value > DateTimeOffset.UtcNow;
        if (shieldActive)
        {
            score = Math.Min(score, 10);
            warnings.Insert(0, $"No-spend shield is active until {shieldUntil.GetValueOrDefault().ToLocalTime():f}. Save the offer and reassess after the shield expires.");
        }

        if (string.IsNullOrWhiteSpace(input.ContentsText) && Regex.IsMatch(name, "bundle|pack|box|set", RegexOptions.IgnoreCase))
        {
            score -= 8;
            warnings.Add("Bundle contents were not pasted. Do not trust the headline discount until every included item is itemized.");
        }
        if (input.PricePearls <= 0)
        {
            score -= 10;
            warnings.Add("No valid sale price was entered, so the result cannot compare cost against utility.");
        }

        score = Math.Clamp(score, 0, 100);
        var verdict = shieldActive
            ? "Blocked by active no-spend shield"
            : score switch
        {
            >= 84 => "Excellent value for your profile",
            >= 68 => "Good value if the stated use is real",
            >= 49 => "Conditional; compare alternatives and wait",
            >= 30 => "Poor value",
            _ => "Avoid"
        };

        var result = new PearlOfferResult
        {
            Score = score,
            Verdict = verdict,
            Summary = BuildSummary(name, score, discount, input.HoursUntilOfferEnds),
            Positives = positives.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
        _db.AddPearlEvaluation(new PearlEvaluationHistory
        {
            OfferName = string.IsNullOrWhiteSpace(name) ? "Unnamed offer" : name,
            PricePearls = input.PricePearls,
            OriginalPricePearls = input.OriginalPricePearls,
            Score = score,
            Verdict = verdict,
            Notes = string.Join(" ", result.Warnings)
        });
        return result;
    }

    private static void AddBudgetAndCooldownRules(
        PearlOfferInput input,
        AppSettings settings,
        ref int score,
        List<string> positives,
        List<string> warnings)
    {
        if (settings.SpendingStyle == SpendingStyle.FreeToPlay)
        {
            score -= 18;
            warnings.Add("Your profile is Free-to-Play; paid convenience must clear a very high bar.");
        }
        else if (settings.SpendingStyle == SpendingStyle.LowSpender)
        {
            score -= 7;
            warnings.Add("Your profile is Low Spender; prioritize durable family infrastructure over consumables.");
        }

        if (settings.MonthlyPearlBudget <= 0 && input.PricePearls > 0)
        {
            warnings.Add("No monthly Pearl budget is configured. Set one before treating this purchase as affordable.");
        }
        else if (settings.MonthlyPearlBudget > 0 && input.PricePearls > settings.MonthlyPearlBudget)
        {
            score -= 24;
            warnings.Add("The purchase exceeds the full monthly Pearl budget stored in your profile.");
        }
        else if (settings.MonthlyPearlBudget > 0 && input.PricePearls > settings.MonthlyPearlBudget * 0.6)
        {
            score -= 8;
            warnings.Add("This single offer consumes more than 60% of the saved monthly Pearl budget.");
        }

        var cooldown = settings.PearlPurchaseCooldownHours;
        if (cooldown <= 0 || input.PricePearls <= 0) return;

        if (input.HoursUntilOfferEnds < 0)
        {
            warnings.Add($"Apply the configured {cooldown}-hour cooling-off period. The offer expiry was not entered.");
        }
        else if (input.HoursUntilOfferEnds > cooldown)
        {
            positives.Add($"The offer has enough entered time remaining for the configured {cooldown}-hour cooling-off period.");
            warnings.Add($"Wait at least {cooldown} hours, then rescore it with the same contents and budget.");
        }
        else
        {
            score -= 4;
            warnings.Add($"The entered timer ends within {cooldown} hours. Countdown pressure is a risk signal, not evidence of value.");
        }
    }

    private static void AddNameRules(string text, AppSettings settings, ref int score, List<string> positives, List<string> warnings)
    {
        if (Regex.IsMatch(text, "tent|campsite", RegexOptions.IgnoreCase))
        {
            if (settings.HasTent)
            {
                score -= 28;
                warnings.Add("A campsite is already marked as owned. Duplicate tent functionality has almost no utility.");
            }
            else
            {
                score += 18;
                positives.Add("The premium campsite is permanent family utility and removes repeated travel friction.");
                warnings.Add("A free Old Moon campsite exists; the paid value is convenience and premium functions, not basic camping access.");
            }
        }
        if (Regex.IsMatch(text, "maid|butler", RegexOptions.IgnoreCase))
        {
            score += 7;
            positives.Add("Maids are family-wide and can reduce repeated storage or market friction.");
            warnings.Add("The game distributes some maids through events. Buy only for a measured cooldown-capacity gap and preferably in a strong bundle.");
            if (settings.HasStorageMaid || settings.HasTransactionMaid)
                warnings.Add("At least one maid type is already marked as owned. Confirm whether the offer improves the specific storage or transaction pool you actually use.");
        }
        if (Regex.IsMatch(text, "character slot", RegexOptions.IgnoreCase))
        {
            score += 12;
            positives.Add("Character slots are permanent account utility and support energy, storage, seasons, trials, and alts.");
        }
        if (Regex.IsMatch(text, "family inventory|naderr", RegexOptions.IgnoreCase))
        {
            score += 11;
            positives.Add("This improves family-wide account infrastructure rather than one temporary character.");
        }
        if (Regex.IsMatch(text, "weight|inventory expansion", RegexOptions.IgnoreCase))
        {
            score -= 7;
            warnings.Add("Weight and inventory are usually character-specific. Buy only for a confirmed long-term main after using free quest, loyalty, season, and event expansions.");
        }
        if (Regex.IsMatch(text, "outfit|costume", RegexOptions.IgnoreCase))
        {
            warnings.Add("Outfits are primarily cosmetic and often class-specific. Check Central Market availability and keep unopened boxes off an undecided class.");
        }
        if (Regex.IsMatch(text, "artisan.?s memory|cron stone|memory fragment|valks|enhancement aid", RegexOptions.IgnoreCase))
        {
            score -= 22;
            warnings.Add("Consumable enhancement spending disappears permanently and is generally weaker than durable family utility.");
        }
        if (Regex.IsMatch(text, "adventure box|mystery|pouch|rarit(?:y|ies)|random|fortune|loot box|gacha", RegexOptions.IgnoreCase))
        {
            score -= 28;
            warnings.Add("Gambling-style contents detected. Use published probabilities and expected value; a discount does not repair bad odds.");
        }
        if (Regex.IsMatch(text, "mount skill change|fairy skill|theiah|reset mount|horse skill|reroll", RegexOptions.IgnoreCase))
        {
            score -= 15;
            warnings.Add("Reroll items can become an uncapped spending sink. Set a hard attempt and money cap before buying any.");
        }
        if (Regex.IsMatch(text, "value pack|blessing of old moon|kamasylve", RegexOptions.IgnoreCase))
        {
            if (settings.WeeklyPlayHours < 5)
            {
                score -= 12;
                warnings.Add("Your saved weekly playtime is low, so a temporary subscription loses much of its theoretical value.");
            }
            else
            {
                score += 2;
            }
            warnings.Add("Subscription buffs are temporary. Compare active play days, event versions, market availability, and whether the buff solves a current bottleneck.");
        }
        if (Regex.IsMatch(text, "pet|gosphy", RegexOptions.IgnoreCase))
        {
            score += 4;
            positives.Add("Pets can improve loot pickup and family utility when filling a specific tier or special-skill gap.");
            warnings.Add("Do not buy duplicate pets without an exact exchange, tier, and special-skill plan. Many pets are distributed through events.");
        }
        if (Regex.IsMatch(text, "limited time|last chance|today only|ends soon|flash sale", RegexOptions.IgnoreCase))
        {
            score -= 5;
            warnings.Add("Urgency language detected. Re-evaluate the useful contents at zero discount before responding to the countdown.");
        }
        if (Regex.IsMatch(text, "coupon bundle|coupon pack", RegexOptions.IgnoreCase))
        {
            score -= 6;
            warnings.Add("Coupons only create value when used on a separately justified purchase. Do not count the discount twice.");
        }
    }

    private static string BuildSummary(string name, int score, double discount, int hoursUntilEnd)
    {
        var label = string.IsNullOrWhiteSpace(name) ? "This offer" : name;
        var discountText = discount > 0
            ? $" Entered price arithmetic: {discount:P0} off."
            : " No verified discount was entered.";
        var expiryText = hoursUntilEnd >= 0
            ? $" Entered time remaining: {hoursUntilEnd} hour(s)."
            : " Offer expiry is unknown.";
        return $"{label} scores {score}/100 after permanence, binding, randomness, padding, alternatives, duplication, budget, playtime, and cooling-off checks.{discountText}{expiryText}";
    }
}
