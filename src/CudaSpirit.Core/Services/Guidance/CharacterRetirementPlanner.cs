using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Guidance;

/// <summary>
/// Fail-closed pre-deletion checklist. The app cannot read the game client, so every item is a
/// player-confirmed verification and all critical rows must be complete before the verdict changes.
/// </summary>
public sealed class CharacterRetirementPlanner
{
    public IReadOnlyList<RetirementCheckItem> CreateChecklist() => new List<RetirementCheckItem>
    {
        Critical("inventory", "Inventory", "Empty normal inventory safely", "Move useful and uncertain items to reviewed storage. Resolve trash only after Item Intel or exact tooltip checks.", "Open inventory, sort every tab, then use Ctrl+F to confirm no important item remains."),
        Critical("pearl", "Paid value", "Clear Pearl inventory and character-bound purchases", "Review outfits, underwear, weight, inventory expansions, flute/horn, coupons, furniture, and unopened paid boxes. Some value cannot be moved after assignment.", "Open Pearl inventory and record every remaining item or permanent character upgrade."),
        Critical("gear", "Equipment", "Remove all gear and costumes", "Unequip weapons, armor, accessories, tools, life-skill gear, costumes, and special slots. Locked or copied equipment needs separate handling.", "Switch every equipment preset and verify all visible slots are empty."),
        Critical("crystals", "Combat systems", "Resolve crystals, artifacts, and lightstones", "Check active and saved presets, extracted pieces, artifact slots, lightstone combinations, and expensive components.", "Open the crystal and artifact interfaces, inspect every preset, and screenshot the final state."),
        Critical("tag", "Character systems", "Remove tag and Item Copy dependencies", "Confirm whether the character is tagged, holds copied gear, or is needed to preserve another character's copy state. Read current untag and copy rules before acting.", "Open Character Tag/Item Copy and verify no active relationship or unresolved copied item remains."),
        Critical("mount", "Mounts", "Recover mounts and mount inventory", "Check horse, wagon, ship, guild mount, remote collection, mount gear, cargo, trade items, and any mount registered or parked with this character.", "Open mount information and stable/wharf lists; empty inventories and confirm registration location."),
        Critical("storage", "Family storage", "Search the character through Find My Item", "Use Ctrl+F for rare resources, coupons, outfits, quest items, enhancement materials, treasure pieces, crystals, tools, and currencies associated with this character.", "Search several known item names and inspect the character result directly."),
        Critical("quests", "Progression", "Resolve character-specific quests and rewards", "Check completed-but-unclaimed rewards, active quest items, one-time class quests, succession/awakening, Magnus/region progress, season state, and graduation eligibility.", "Open Main, Suggested, Recurring, and season/pass interfaces; claim or document everything relevant."),
        Critical("season", "Season", "Exit season state correctly", "Do not delete an active season character as a shortcut. Confirm current deletion, transfer, graduation, and season-pass consequences in the live client and official notice.", "Open season UI and current official season guide; verify the account will retain every intended reward."),
        Critical("mail", "Rewards", "Clear character-targeted mail and safes", "Claim or deliberately leave only rewards proven to be family-safe. Check website Web Storage target, Mail (B), Black Spirit's Safe, Challenge rewards, attendance, and event/pass tabs.", "Review expiry and target-character text for each unclaimed reward."),
        Critical("locks", "Deletion blockers", "Remove locks, trade goods, and restricted state", "Resolve locked items, active trade/barter goods, guild items, rented items, copied items, special-region state, party/platoon state, and anything the client reports as a deletion blocker.", "Attempt the in-game deletion flow only after backups; stop at the final confirmation and record every blocker shown."),
        Optional("presets", "Quality of life", "Export or document presets", "Save skill presets, UI presets, quick slots, crystal presets, add-ons, appearance screenshots, keybind notes, and a short combo reference if you may recreate the class.", "Capture screenshots or write a note before removing the character."),
        Optional("identity", "Identity", "Preserve names and character history", "Record character name, level, creation date if visible, life skills, energy, titles, screenshots, and sentimental items. Confirm name-release rules separately.", "Take final profile and inventory screenshots."),
        Optional("life", "Life skills", "Move life-skill tools and production dependencies", "Check equipped tools, mastery accessories, processing stones, manos gear, workers, farms, residences, imperial items, and character energy plans.", "Open life-skill profile and every production inventory used by the character."),
        Optional("support", "Recovery proof", "Create a deletion evidence pack", "Keep screenshots of all inventories, Pearl inventory, gear, presets, mounts, tag state, quest state, and the final confirmation screen.", "Store screenshots outside the game folder with date and character name."),
        Critical("cooldown", "Final gate", "Wait through a deliberate cooldown", "After every critical check is green, wait at least one full day unless there is a genuine expiring reason. Re-open the checklist once before final confirmation.", "Confirm the plan still makes sense after the cooldown and that a character slot coupon is not the safer option.")
    };

    public RetirementAssessment Assess(IEnumerable<RetirementCheckItem> items)
    {
        var list = items.ToList();
        var critical = list.Where(x => x.Critical && !x.IsChecked).ToList();
        var completed = list.Count(x => x.IsChecked);
        var percent = list.Count == 0 ? 0 : (int)Math.Round(completed * 100d / list.Count);
        return new RetirementAssessment
        {
            Completed = completed,
            Total = list.Count,
            CriticalRemaining = critical.Count,
            ReadinessPercent = percent,
            SafeToDelete = critical.Count == 0 && list.Count > 0,
            Verdict = critical.Count == 0
                ? "Critical checklist complete. Re-read the final client warning before confirming deletion."
                : $"HARD STOP: {critical.Count} critical check(s) remain. Do not delete this character.",
            Blockers = critical.Select(x => x.Title).ToList()
        };
    }

    private static RetirementCheckItem Critical(string key, string category, string title, string detail, string verify) =>
        new() { Key = key, Category = category, Title = title, Detail = detail, Verification = verify, Critical = true };

    private static RetirementCheckItem Optional(string key, string category, string title, string detail, string verify) =>
        new() { Key = key, Category = category, Title = title, Detail = detail, Verification = verify, Critical = false };
}
