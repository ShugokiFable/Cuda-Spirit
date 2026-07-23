using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Grind;

/// <summary>A single, ranked "do this next" suggestion for gear progression.</summary>
public sealed record ProgressionStep(string Title, string Detail, int Priority);

/// <summary>
/// Offline heuristics for "what should I upgrade next" and AP/DP bracket awareness. Deliberately
/// rule-based (not the LLM) so it works without an API key; the AI advisor can elaborate on top.
/// </summary>
public sealed class ProgressionHelper
{
    /// <summary>Well-known combined-AP breakpoints where damage caps step up.</summary>
    public static readonly int[] ApBrackets =
        { 245, 249, 261, 269, 274, 280, 286, 301, 305, 310 };

    public static readonly int[] DpBrackets =
        { 340, 360, 380, 401, 420, 450, 480, 500 };

    public int NextApBracket(int ap) => ApBrackets.FirstOrDefault(b => b > ap, ap);
    public int NextDpBracket(int dp) => DpBrackets.FirstOrDefault(b => b > dp, dp);

    /// <summary>Distance to the next AP bracket (0 if already on one).</summary>
    public int ApToNextBracket(int ap)
    {
        var next = NextApBracket(ap);
        return next > ap ? next - ap : 0;
    }

    /// <summary>
    /// Produce a prioritized next-steps list from the player's current state. Higher priority first.
    /// </summary>
    public IReadOnlyList<ProgressionStep> Suggest(PlayerState s)
    {
        var steps = new List<ProgressionStep>();
        int combinedAp = Math.Max(s.Ap, s.Awakening);

        // 1. Chase the nearest AP bracket if it's close.
        int toAp = ApToNextBracket(combinedAp);
        if (combinedAp > 0 && toAp is > 0 and <= 8)
            steps.Add(new ProgressionStep(
                $"Push to {NextApBracket(combinedAp)} AP",
                $"You're only {toAp} AP under the next damage bracket - a single accessory tap or Caphras level likely gets you there.",
                100));

        // 2. Balance AP vs DP (avoid glass-cannon / tanky-noodle).
        if (s.Dp > 0 && combinedAp - s.Dp > 60)
            steps.Add(new ProgressionStep(
                "Shore up DP",
                $"AP {combinedAp} vs DP {s.Dp} is lopsided; higher-tier spots gate on survivability. Consider a defensive accessory or armor tap.",
                80));

        // 3. Caphras on the cheapest slot.
        var lowCaphras = s.Gear.Where(g => g.Equipped && g.Caphras < 10)
                               .OrderBy(g => g.Caphras).FirstOrDefault();
        if (lowCaphras is not null)
            steps.Add(new ProgressionStep(
                $"Caphras your {lowCaphras.Slot}",
                $"{lowCaphras.Name} is at Caphras {lowCaphras.Caphras}. Caphras is guaranteed AP/DP with no downgrade risk - steady, safe gains.",
                60));

        // 4. Softcap accessories to TRI before boss gear TET.
        var softAcc = s.Gear.FirstOrDefault(g => g.Equipped && g.Kind == EnhanceKind.Accessory && g.Grade < EnhanceGrade.TRI);
        if (softAcc is not null)
            steps.Add(new ProgressionStep(
                $"Bring {softAcc.Slot} to TRI",
                $"{softAcc.Name} at {softAcc.Grade}: accessory TRIs are usually the best AP-per-silver before chasing TET boss gear.",
                50));

        if (steps.Count == 0)
            steps.Add(new ProgressionStep(
                "Log your gear",
                "Add your equipped items in the Gear tab so the helper can rank upgrades and the AI advisor can see your build.",
                10));

        return steps.OrderByDescending(x => x.Priority).ToList();
    }
}
