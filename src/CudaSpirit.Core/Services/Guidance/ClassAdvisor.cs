using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Guidance;

/// <summary>
/// Matches play-feel preferences rather than pretending a frozen tier list is universal. Scores are
/// intentionally broad and the result always recommends trial-character testing before paid or
/// character-bound investment.
/// </summary>
public sealed class ClassAdvisor
{
    private static readonly IReadOnlyList<ClassProfile> Profiles = BuildProfiles();

    public IReadOnlyList<ClassRecommendation> Recommend(ClassPreferenceInput input, int count = 5)
    {
        return Profiles
            .Select(profile => Score(profile, input))
            .OrderByDescending(x => x.MatchPercent)
            .ThenBy(x => x.ClassName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(count, 1, 10))
            .ToList();
    }

    public IReadOnlyList<string> ClassNames => Profiles.Select(x => x.Name).OrderBy(x => x).ToList();

    private static ClassRecommendation Score(ClassProfile p, ClassPreferenceInput input)
    {
        var score = 50d;
        var reasons = new List<string>();
        var desiredComplexity = input.Complexity.ToLowerInvariant() switch { "simple" => 1.5, "high" => 4.5, _ => 3d };
        var desiredSurvival = input.Survivability.ToLowerInvariant() switch { "high" => 4.5, "low" => 2d, _ => 3d };

        if (input.Range.Equals("Any", StringComparison.OrdinalIgnoreCase) || p.Range.Contains(input.Range, StringComparison.OrdinalIgnoreCase))
        {
            score += 12;
            if (!input.Range.Equals("Any", StringComparison.OrdinalIgnoreCase)) reasons.Add($"matches your {input.Range.ToLowerInvariant()} preference");
        }
        else score -= 10;

        if (input.Pace.Equals("Balanced", StringComparison.OrdinalIgnoreCase) || p.Pace.Equals(input.Pace, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            if (!input.Pace.Equals("Balanced", StringComparison.OrdinalIgnoreCase)) reasons.Add($"has the {input.Pace.ToLowerInvariant()} pace you selected");
        }
        else score -= 6;

        score += 12 - Math.Abs(p.Complexity - desiredComplexity) * 5;
        score += 10 - Math.Abs(p.Survivability - desiredSurvival) * 4;

        if (input.Focus.Equals("PvP", StringComparison.OrdinalIgnoreCase)) { score += p.PvP * 3; reasons.Add("fits a PvP-first test plan"); }
        else if (input.Focus.Equals("Mixed", StringComparison.OrdinalIgnoreCase)) { score += (p.PvE + p.PvP) * 1.5; reasons.Add("has mixed-content flexibility"); }
        else { score += p.PvE * 3; reasons.Add("fits a PvE-first test plan"); }

        if (input.WantsSupport) { score += p.Support * 5; if (p.Support >= 3) reasons.Add("offers meaningful group utility"); }
        else if (p.Support >= 4) score -= 2;
        if (input.WantsGrab) { score += p.HasGrab ? 10 : -8; if (p.HasGrab) reasons.Add("includes a grab"); }
        if (input.AvoidsHighApm) score += p.Complexity <= 2 ? 10 : p.Complexity >= 4 ? -12 : 2;

        score = Math.Clamp(score, 0, 100);
        if (reasons.Count == 0) reasons.Add(p.Identity);
        return new ClassRecommendation
        {
            ClassName = p.Name,
            MatchPercent = (int)Math.Round(score),
            Why = char.ToUpperInvariant(reasons[0][0]) + reasons[0][1..] + (reasons.Count > 1 ? "; " + string.Join("; ", reasons.Skip(1).Take(2)) : "") + $". {p.Identity}",
            WatchOutFor = p.Caveat,
            TrialPlan = "Create a trial character, test movement and defensive recovery, run the same short PvE rotation three times, then compare fatigue and clarity. Do not bind weight, inventory, outfits, or exchange coupons yet."
        };
    }

    private static IReadOnlyList<ClassProfile> BuildProfiles() => new List<ClassProfile>
    {
        P("Warrior", "Melee", "Balanced", 3, 5, 3, 4, 4, 1, true, "Shielded frontline with deliberate melee control.", "Awakening can be mechanically dense; succession and awakening feel very different."),
        P("Ranger", "Ranged", "Fast", 4, 2, 4, 4, 4, 1, false, "Mobile bow pressure with a fragile, active-defense profile.", "Low forgiveness when positioning or stamina management slips."),
        P("Sorceress", "Melee", "Fast", 5, 3, 4, 4, 5, 1, false, "Iframe-heavy spellblade built around timing and close control.", "High input and timing demand can become tiring."),
        P("Berserker", "Melee", "Fast", 4, 5, 4, 5, 5, 1, true, "Large, durable brawler with grabs and explosive movement.", "Camera control and advanced movement tech raise the ceiling."),
        P("Tamer", "Melee", "Fast", 5, 2, 5, 4, 4, 1, true, "Pet-assisted agile duelist with unusual movement.", "Small margin for error and a demanding control scheme."),
        P("Musa", "Melee", "Fast", 3, 3, 5, 4, 4, 0, false, "High-speed skirmisher with flowing dash mobility.", "Stamina and protection gaps punish careless movement."),
        P("Maehwa", "Melee", "Fast", 4, 3, 5, 4, 4, 0, false, "Precision spear-and-blade skirmisher with linear burst.", "Directional commitment and stamina management matter."),
        P("Valkyrie", "Melee", "Balanced", 4, 5, 3, 4, 5, 4, true, "Defensive holy frontline with heals, buffs, and control.", "Advanced movement and PvP execution can be technical."),
        P("Kunoichi", "Melee", "Fast", 5, 3, 5, 4, 5, 1, true, "Stealthy assassin with flexible cancels and mobility.", "High APM and matchup knowledge are major parts of the kit."),
        P("Ninja", "Melee", "Fast", 5, 3, 5, 4, 5, 0, true, "Explosive assassin with many tools and movement options.", "One of the highest execution and APM profiles."),
        P("Wizard", "Ranged", "Balanced", 2, 3, 2, 4, 4, 5, false, "Accessible elemental caster with strong group utility.", "Lower mobility can make positioning feel restrictive."),
        P("Witch", "Ranged", "Balanced", 2, 3, 2, 4, 4, 5, false, "Stable elemental caster with heals and group utility.", "Deliberate movement and casting pace are not for everyone."),
        P("Dark Knight", "Hybrid", "Fast", 4, 2, 4, 4, 4, 1, false, "Stylish magic melee hybrid with sweeping damage.", "Fragility and protection management require attention."),
        P("Striker", "Melee", "Balanced", 2, 5, 3, 5, 4, 1, true, "Durable martial artist with straightforward impact.", "Can feel less ranged or utility-rich than hybrid classes."),
        P("Mystic", "Melee", "Balanced", 3, 5, 4, 4, 4, 2, true, "Durable combo fighter with sustained control and recovery.", "Longer combo structure may feel repetitive to burst-focused players."),
        P("Lahn", "Melee", "Fast", 2, 3, 5, 5, 4, 1, true, "Fluid aerial mobility and approachable sustained damage.", "Some defensive choices rely on movement rather than raw durability."),
        P("Archer", "Ranged", "Fast", 3, 2, 5, 4, 4, 1, false, "Long-range mobile archer with strong kiting identity.", "Fragile when enemies close distance or terrain blocks lines."),
        P("Shai", "Hybrid", "Balanced", 2, 4, 3, 3, 3, 5, false, "Support specialist with buffs, healing, and life-skill flavor.", "Not the default choice for solo damage or conventional PvP dueling."),
        P("Guardian", "Melee", "Slow", 1, 5, 2, 5, 4, 1, true, "Heavy, forgiving bruiser with large attacks and sustain.", "Slower animation cadence can feel unresponsive to speed-focused players."),
        P("Hashashin", "Melee", "Fast", 4, 3, 5, 5, 4, 1, true, "Mobile desert assassin with flowing sand movement.", "Combo routes and movement discipline take practice."),
        P("Nova", "Hybrid", "Balanced", 3, 5, 3, 4, 4, 2, true, "Shielded commander or high-speed awakening duelist.", "Succession and awakening have radically different pace and complexity."),
        P("Sage", "Hybrid", "Balanced", 3, 3, 4, 5, 4, 1, true, "Space-time caster with large attacks and teleport mobility.", "Teleports and defensive timing can feel unusual at first."),
        P("Corsair", "Melee", "Fast", 4, 3, 5, 4, 4, 3, false, "Acrobatic pirate with flowing movement and group disruption.", "Movement chains and protections need deliberate practice."),
        P("Drakania", "Melee", "Balanced", 3, 5, 4, 4, 4, 2, true, "Dragon-themed bruiser with durable, powerful attacks.", "Forms and resource flow differ substantially between specs."),
        P("Woosa", "Ranged", "Balanced", 2, 3, 3, 5, 4, 2, false, "Elegant mid-range caster with broad, controlled damage.", "Delayed effects and positioning reward planning over frantic input."),
        P("Maegu", "Ranged", "Fast", 2, 3, 4, 5, 4, 1, false, "Mobile fox-spirit caster with accessible area damage.", "Can be fragile when movement and spacing are neglected."),
        P("Scholar", "Melee", "Balanced", 2, 4, 3, 4, 3, 2, false, "Hammer fighter with gravity tools and readable impact.", "Less extreme mobility than assassin-style classes."),
        P("Dosa", "Melee", "Balanced", 2, 4, 4, 5, 4, 2, false, "Smooth sword-and-cloud fighter with broad attacks.", "Its flowing cadence may feel less punchy to burst-combo players."),
        P("Deadeye", "Ranged", "Fast", 3, 3, 4, 5, 4, 1, false, "Gun-focused ranged damage with mobile, modern-feeling actions.", "Range management and ammunition-style mechanics may add cognitive load."),
        P("Wukong", "Melee", "Fast", 3, 4, 5, 4, 4, 1, true, "Staff fighter centered on mobility, disruption, and playful reach.", "As a newer class, current balance and optimal rotations can change quickly."),
        P("Seraph", "Hybrid", "Balanced", 3, 4, 3, 4, 4, 4, false, "Newer holy-themed hybrid with defensive and supportive potential.", "As a newer class, verify current skills, balance, and role in live official notes before investing.")
    };

    private static ClassProfile P(string name, string range, string pace, int complexity, int survival, int mobility, int pve, int pvp, int support, bool grab, string identity, string caveat) =>
        new() { Name = name, Range = range, Pace = pace, Complexity = complexity, Survivability = survival, Mobility = mobility, PvE = pve, PvP = pvp, Support = support, HasGrab = grab, Identity = identity, Caveat = caveat };
}
