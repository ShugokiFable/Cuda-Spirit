using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Grind;

/// <summary>A single, ranked "do this next" suggestion for gear progression.</summary>
public sealed record ProgressionStep(string Title, string Detail, int Priority);

/// <summary>
/// Offline heuristics for "what should I upgrade next" and AP/DP bracket awareness. Deliberately
/// rule-based (not the LLM) so it works without an API key; the AI advisor can elaborate on top.
/// Bracket tables: BDFoundry AP/DP bracket guide, last updated 2026-08-13
/// (https://www.blackdesertfoundry.com/ap-and-dp-brackets-guide/). Sheet AP/DP only.
/// </summary>
public sealed class ProgressionHelper
{
    /// <summary>Sheet-AP thresholds where the bonus AP steps up (100→+5 … 449→+297).</summary>
    public static readonly int[] ApBrackets =
    {
        100, 140, 170, 184, 209, 235, 245, 249, 253, 257, 261, 265, 269, 273, 277, 281, 285, 289,
        293, 297, 301, 305, 309, 316, 321, 328, 332, 337, 342, 347, 352, 358, 364, 369, 375, 381,
        386, 392, 397, 399, 401, 403, 405, 407, 409, 411, 413, 415, 417, 419, 421, 423, 425, 427,
        429, 431, 433, 435, 437, 439, 441, 443, 445, 447, 449
    };

    /// <summary>Sheet-DP thresholds where the bonus damage-reduction rate steps up (+1% per bracket, 203→30%).</summary>
    public static readonly int[] DpBrackets =
    {
        203, 211, 218, 226, 233, 241, 248, 256, 263, 271, 278, 286, 293, 301, 308, 315, 322, 329,
        335, 341, 347, 353, 359, 365, 371, 377, 383, 389, 395, 401
    };

    /// <summary>Big-jump brackets worth naming explicitly (bonus AP jumps of 15+).</summary>
    private static readonly HashSet<int> MajorApBrackets = new() { 209, 235, 253, 257, 261, 265, 269 };

    public int NextApBracket(int ap) => ApBrackets.FirstOrDefault(b => b > ap, ap);
    public int NextDpBracket(int dp) => DpBrackets.FirstOrDefault(b => b > dp, dp);

    /// <summary>Distance to the next AP bracket (0 if above the table).</summary>
    public int ApToNextBracket(int ap)
    {
        var next = NextApBracket(ap);
        return next > ap ? next - ap : 0;
    }

    public int DpToNextBracket(int dp)
    {
        var next = NextDpBracket(dp);
        return next > dp ? next - dp : 0;
    }

    /// <summary>
    /// Produce a prioritized next-steps list from the player's current state. Higher priority first.
    /// </summary>
    public IReadOnlyList<ProgressionStep> Suggest(PlayerState s)
    {
        var steps = new List<ProgressionStep>();
        int combinedAp = Math.Max(s.Ap, s.Awakening);

        if (combinedAp == 0 && s.Dp == 0)
        {
            steps.Add(new ProgressionStep(
                "Log your gear",
                "Add your equipped items in the Gear tab (or sync your profile in Customize) so the helper can rank upgrades and the AI advisor can see your build.",
                10));
            return steps;
        }

        // Season characters follow the Tuvala path, not the endgame enhancement ladder.
        if (s.IsSeasonCharacter)
        {
            AddSeasonSteps(steps, s, combinedAp);
            return steps.OrderByDescending(x => x.Priority).ToList();
        }

        // 1. Chase the nearest AP bracket - brackets multiply the value of small upgrades.
        int toAp = ApToNextBracket(combinedAp);
        if (combinedAp >= 100 && toAp > 0)
        {
            var next = NextApBracket(combinedAp);
            bool major = MajorApBrackets.Contains(next);
            steps.Add(new ProgressionStep(
                $"Push to {next} AP",
                toAp <= 3
                    ? $"Only {toAp} AP from the next bracket - one Caphras level or a single tap likely lands it. Brackets multiply small upgrades."
                    : $"Next bracket at {next} AP ({toAp} away){(major ? " - this one is a big jump, plan taps/Caphras around it" : "")}. Check cheaper slots first (accessories, Caphras on armor).",
                toAp <= 3 ? 100 : major ? 90 : 70));
        }
        else if (combinedAp > 0 && combinedAp < 100)
        {
            steps.Add(new ProgressionStep(
                "Reach 100 AP for the first bracket",
                "Asula accessories from Mediah (Helms, Elric, Manes, Iron Mine) plus any main-hand upgrades get you to the first AP bracket cheaply.",
                95));
        }

        // 2. Balance AP vs DP - higher-tier spots gate on survivability, DP brackets are flat %DR.
        int toDp = DpToNextBracket(s.Dp);
        if (s.Dp > 0 && toDp > 0 && toDp <= 8 && s.Dp >= 203)
        {
            steps.Add(new ProgressionStep(
                $"Push to {NextDpBracket(s.Dp)} DP",
                $"{toDp} DP from the next damage-reduction bracket (+1% DR). Armor upgrades or a defensive crystal usually close this gap.",
                s.Dp < 301 ? 85 : 60));
        }
        if (s.Dp > 0 && combinedAp - s.Dp > 70)
        {
            steps.Add(new ProgressionStep(
                "Shore up DP",
                $"AP {combinedAp} vs DP {s.Dp} is lopsided; you'll die too much at the spots this AP unlocks. A DP bracket or two also adds flat damage reduction.",
                75));
        }

        // 3. Guaranteed-progress upgrades before gambling.
        var lowCaphras = s.Gear.Where(g => g.Equipped && g.Caphras < 10 &&
                                           (g.Kind == EnhanceKind.Armor || g.Kind == EnhanceKind.Weapon))
                               .OrderBy(g => g.Caphras).FirstOrDefault();
        if (lowCaphras is not null)
            steps.Add(new ProgressionStep(
                $"Caphras your {lowCaphras.Slot}",
                $"{lowCaphras.Name} sits at Caphras {lowCaphras.Caphras}. Caphras is guaranteed AP/DP with zero downgrade risk - and each armor level feeds your Caphras bracket.",
                60));

        // 4. Accessory TRIs before TET boss gear - best AP per silver.
        var softAcc = s.Gear.FirstOrDefault(g => g.Equipped && g.Kind == EnhanceKind.Accessory && g.Grade < EnhanceGrade.TRI);
        if (softAcc is not null)
            steps.Add(new ProgressionStep(
                $"Bring {softAcc.Slot} to TRI",
                $"{softAcc.Name} at {softAcc.Grade}: accessory TRIs are usually the best AP-per-silver before chasing TET boss gear.",
                50));

        // 5. Hammer-challenge milestones (free guaranteed taps at 340-385 sheet AP, monthly).
        if (combinedAp is >= 335 and < 385)
        {
            var nextHammer = NextApBracket(combinedAp);
            if (nextHammer is >= 340 and <= 385)
                steps.Add(new ProgressionStep(
                    $"Time a tap for the {nextHammer} hammer challenge",
                    "Hammer challenges hand out guaranteed enhancement coupons at fixed sheet-AP milestones (340-385, monthly). Don't tap past one - let the coupon carry the risk.",
                    65));
        }

        if (steps.Count == 0)
            steps.Add(new ProgressionStep(
                "Gear looks bracket-aligned",
                "No cheap bracket is in reach right now - save silver for the next accessory/boss-gear piece, or ask the Advisor for a full roadmap.",
                10));

        return steps.OrderByDescending(x => x.Priority).ToList();
    }

    private static void AddSeasonSteps(List<ProgressionStep> steps, PlayerState s, int combinedAp)
    {
        steps.Add(new ProgressionStep(
            "Season path: PEN Tuvala before anything else",
            "On season servers, every upgrade is Tuvala enhancement with Time-Filled Black Stones and refined stones - boss gear and Caphras don't apply. Push every Tuvala piece to PEN (the season conversion makes it family-bound boss/Tuvala gear at graduation).",
            100));
        if (combinedAp < 240)
            steps.Add(new ProgressionStep(
                "Grind spots for your Tuvala tier",
                "With Tuvala you can hit Titium/Naga/Bashim (~100 AP), Polly's/Fadus (~140 AP), then Mirumok/Sycraia (~200 AP) for fast combat EXP. Check the Route Planner - season spots are tagged.",
                80));
        steps.Add(new ProgressionStep(
            "Claim season pass + Fughar rewards each session",
            "Season pass milestones, Fughar's exchanges, and the weekly Black Spirit Pass are the real progression currency - miss them and PEN Tuvala takes twice as long.",
            90));
        steps.Add(new ProgressionStep(
            "Plan graduation before the season ends",
            "Before graduating: finish the season pass, use Fughar's Timepiece, exchange leftover Tuvala materials, and decide early vs natural graduation. Graduated gear converts family-bound.",
            85));
    }
}
