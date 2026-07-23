using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Enhancement;

/// <summary>
/// Probability model for enhancement (failstacks, cron protection, expected cost).
///
/// NOTE ON ACCURACY: Pearl Abyss does not publish exact enhancement rates. The numbers here are
/// a community-consensus approximation (base chance + per-failstack increment with a soft cap).
/// They are meant for *relative* planning ("PRI vs DUO is much safer at 30 stacks") and expected-cost
/// estimates - not a guarantee of any single tap. Rates live in a table you can tune in one place.
/// </summary>
public sealed class EnhancementSimulator
{
    /// <summary>Base success chance at 0 failstacks for a given grade+kind.</summary>
    private static double BaseChance(EnhanceKind kind, EnhanceGrade grade) => (kind, grade) switch
    {
        // Boss/main gear (weapon & armor share a curve closely enough for planning)
        (EnhanceKind.Weapon or EnhanceKind.Armor, EnhanceGrade.PRI) => 0.25,
        (EnhanceKind.Weapon or EnhanceKind.Armor, EnhanceGrade.DUO) => 0.10,
        (EnhanceKind.Weapon or EnhanceKind.Armor, EnhanceGrade.TRI) => 0.075,
        (EnhanceKind.Weapon or EnhanceKind.Armor, EnhanceGrade.TET) => 0.025,
        (EnhanceKind.Weapon or EnhanceKind.Armor, EnhanceGrade.PEN) => 0.0150,

        // Accessories - steeper drop-off
        (EnhanceKind.Accessory, EnhanceGrade.PRI) => 0.20,
        (EnhanceKind.Accessory, EnhanceGrade.DUO) => 0.075,
        (EnhanceKind.Accessory, EnhanceGrade.TRI) => 0.045,
        (EnhanceKind.Accessory, EnhanceGrade.TET) => 0.020,
        (EnhanceKind.Accessory, EnhanceGrade.PEN) => 0.0075,

        // +1..+15 stages are comparatively easy; a flat friendly value keeps the planner sane.
        _ => 0.40
    };

    /// <summary>
    /// Per-failstack increment. In BDO each stack adds roughly a tenth of the base chance until a
    /// soft cap, after which the marginal gain halves. We model that with two slopes.
    /// </summary>
    private static double SuccessChance(EnhanceKind kind, EnhanceGrade grade, int failstacks)
    {
        var basic = BaseChance(kind, grade);
        var perStack = basic * 0.10;

        // Soft cap where the increment is throttled (higher grades cap sooner).
        int softCap = grade switch
        {
            EnhanceGrade.PRI => 40,
            EnhanceGrade.DUO => 45,
            EnhanceGrade.TRI => 70,
            EnhanceGrade.TET => 110,
            EnhanceGrade.PEN => 160,
            _ => 40
        };

        double bonus;
        if (failstacks <= softCap)
            bonus = perStack * failstacks;
        else
            bonus = perStack * softCap + perStack * 0.5 * (failstacks - softCap);

        return Math.Clamp(basic + bonus, 0.0, 0.90);
    }

    public double ChanceAt(EnhanceKind kind, EnhanceGrade grade, int failstacks)
        => SuccessChance(kind, grade, failstacks);

    /// <summary>
    /// Monte-Carlo estimate of how many taps (and cron stones) it takes to go from the item's
    /// current grade to the target, holding the failstack constant. With cron the item never
    /// degrades; without cron a failure drops a grade (boss gear) or resets accessories.
    /// </summary>
    public EnhanceEstimate Estimate(
        EnhanceKind kind,
        EnhanceGrade current,
        EnhanceGrade target,
        int failstacks,
        bool useCron,
        int cronPerTap,
        int trials = 20_000)
    {
        if (target <= current)
            return new EnhanceEstimate(0, 0, 1.0, ChanceAt(kind, target, failstacks));

        var rng = new Random(12345); // deterministic so the UI is stable between opens
        long totalTaps = 0;
        long totalCrons = 0;

        for (int t = 0; t < trials; t++)
        {
            var g = current;
            while (g < target)
            {
                totalTaps++;
                if (useCron) totalCrons += cronPerTap;

                var p = SuccessChance(kind, g, failstacks);
                if (rng.NextDouble() < p)
                {
                    g = (EnhanceGrade)((int)g + 1);
                }
                else if (!useCron && g > current && IsAccessoryOrBoss(kind))
                {
                    // Without protection, a fail sets you back one grade (accessories reset to base
                    // in reality, but one-grade keeps the estimator conservative and comparable).
                    g = (EnhanceGrade)((int)g - 1);
                }
                // With cron, or at the floor grade, a fail just consumes a tap.
            }
        }

        double avgTaps = (double)totalTaps / trials;
        double avgCrons = (double)totalCrons / trials;
        double chancePerTapAtTarget = SuccessChance(kind, (EnhanceGrade)((int)target - 1), failstacks);
        return new EnhanceEstimate(avgTaps, avgCrons, 1.0 - Math.Pow(1 - chancePerTapAtTarget, avgTaps), chancePerTapAtTarget);
    }

    private static bool IsAccessoryOrBoss(EnhanceKind kind) =>
        kind is EnhanceKind.Accessory or EnhanceKind.Weapon or EnhanceKind.Armor;

    /// <summary>Suggested failstack range for a grade - where marginal cost/benefit is best.</summary>
    public (int min, int max) RecommendedStacks(EnhanceGrade grade) => grade switch
    {
        EnhanceGrade.PRI => (18, 25),
        EnhanceGrade.DUO => (25, 35),
        EnhanceGrade.TRI => (35, 50),
        EnhanceGrade.TET => (50, 75),
        EnhanceGrade.PEN => (100, 140),
        _ => (10, 20)
    };
}

/// <summary>Result of a Monte-Carlo enhancement estimate.</summary>
public readonly record struct EnhanceEstimate(
    double ExpectedTaps,
    double ExpectedCrons,
    double SuccessWithinExpectedTaps,
    double ChancePerTap);
