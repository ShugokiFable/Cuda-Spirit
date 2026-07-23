using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;

namespace CudaSpirit.App.Views;

public partial class UiDecoderView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private readonly List<UiWorkflow> _all = BuildWorkflows();

    private sealed record UiWorkflow(string Title, string Category, string Path, string Summary, string Detail, string[] Blockers);

    public UiDecoderView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh() => Filter(SearchBox?.Text ?? "");

    private void OnSearch(object sender, TextChangedEventArgs e) => Filter(SearchBox.Text);

    private void Filter(string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rows = terms.Length == 0
            ? _all
            : _all.Where(x => terms.All(t => ($"{x.Title} {x.Category} {x.Path} {x.Summary} {x.Detail}")
                .Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
        ResultsList.ItemsSource = rows;
        CountText.Text = $"{rows.Count} workflows. Search ordinary language, not exact menu names.";
        if (rows.Count > 0 && ResultsList.SelectedItem is null) ResultsList.SelectedIndex = 0;
    }

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is not UiWorkflow row) return;
        SelectedTitle.Text = row.Title;
        SelectedPath.Text = row.Path;
        SelectedDetail.Text = row.Detail;
        BlockersList.ItemsSource = row.Blockers.Select(x => "• " + x);
    }

    private async void OnAskAi(object sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (query.Length == 0 && ResultsList.SelectedItem is UiWorkflow selected) query = selected.Title;
        if (query.Length == 0) query = "Explain the most important Black Desert UI screens and claim surfaces for a new player.";
        StatusText.Text = "Sending the workflow question to the advisor with your profile and current knowledge database…";
        await _hub.Conversation.SendAsync($"I am trying to do this in Black Desert: {query}. Give the exact menu path or hotkey, prerequisites, binding or expiry risks, common blockers, and the safest fallback.");
        StatusText.Text = "Answer added to the shared AI Advisor conversation.";
    }

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Refreshing official guides and notices…";
        try
        {
            var report = await _hub.KnowledgeSync.SyncAllAsync(false);
            StatusText.Text = $"Sync complete: {report.Sources.Count(x => x.Success)}/{report.Sources.Count} sources healthy.";
        }
        catch (Exception ex) { StatusText.Text = "Sync failed: " + ex.Message; }
    }

    private static List<UiWorkflow> BuildWorkflows() => new()
    {
        new("Redeem a website coupon", "Rewards", "Official website → Redeem Coupon → Web Storage → choose region/server and target character", "Redeem outside the game, then deliver from Web Storage.", "Verify the correct account, region, server, expiry, and target character before sending. Delivery to the wrong character can create binding or transfer friction.", new[]{"Expired or region-locked code", "Wrong platform/account", "Web Storage delivery not yet sent", "Character-selecting reward"}),
        new("Find all places rewards hide", "Rewards", "Web Storage + Mail (B) + Black Spirit's Safe + Challenge (Y) + Attendance/Event/Pass interfaces + guild rewards", "A full claim sweep across separate reward surfaces.", "Treat every reward surface as its own inbox. Claim expiring items first, but leave unknown class/character boxes unopened until Item Intel checks them.", new[]{"Mail/Web Storage expiry", "Inventory full", "Reward requires event page claim", "Pass reward not manually collected"}),
        new("Find a missing item", "Inventory", "Ctrl+F → Find My Item", "Search family locations before assuming the item vanished.", "Search the exact item name. Review characters, town storages, mounts, Central Market warehouse, and special inventories. Use the indicated retrieval method rather than purchasing a duplicate.", new[]{"Name translated differently", "Inside an unopened box", "Equipped on another character", "Special inventory category"}),
        new("Move an item to another character", "Inventory", "Ctrl+F or World Map → family character inventory → storage/Magnus/maid/warehouse as eligible", "Choose the transfer rail based on binding and item type.", "Character-bound items usually cannot move. For eligible items, use town storage, Magnus-linked storage, a storage maid, direct family-character retrieval, or Central Market warehouse without listing it for sale.", new[]{"Character-bound", "Trade/barter good", "Guild or special item", "Tagged-character restriction", "Maid weight/quantity limit"}),
        new("Use Family Inventory", "Inventory", "Inventory (I) → Family Inventory tab", "Shared space for eligible family-use items, not a universal shared bag.", "Only allowed categories can enter. If the slot rejects an item, use storage or another transfer rail instead of assuming the item is stuck.", new[]{"Item category not eligible", "Family Inventory quest not completed", "No free family slot"}),
        new("Use Central Market warehouse safely", "Economy", "ESC → Central Market → Manage Warehouse", "Move marketable items/silver without listing them for sale.", "The warehouse is family-wide for eligible market items. Depositing into the warehouse is not the same as registering a sale. Check tax and liquidity before listing.", new[]{"Item not marketable", "Price registration restriction", "Warehouse capacity", "Item locked or equipped"}),
        new("Check whether an item binds", "Safety", "Inventory tooltip → binding/usage lines → Item Intel", "Read binding before opening, equipping, registering, or using.", "Unknown boxes may change binding after opening. Character inventory/weight coupons, outfits, and class-selecting rewards should remain unopened until the permanent target is chosen.", new[]{"Tooltip omits post-opening behavior", "Character-selecting contents", "Expiry forces a decision", "Item is already bound"}),
        new("Decide whether to sell an item", "Safety", "Item Intel → compare NPC Exchange, crafting, Central Market, progression, rarity, and expiry", "Vendor price alone is not enough.", "Search the exact name and inspect exchange uses before selling. Quarantine unfamiliar event, treasure, enhancement, season, and coupon items in a Review storage.", new[]{"NPC Exchange use", "Time-gated replacement", "Future guaranteed gear path", "Low market liquidity", "Tax reduces proceeds"}),
        new("Open Season Pass and graduation checks", "Progression", "Season icon/pass widget + Black Spirit + current official season guide", "Claim every reward and resolve Tuvala/exchanges before graduation.", "Keep a written graduation checklist. Do not graduate merely because the button appears. Confirm pass completion, premium-pass claims, Tuvala targets, season materials, exchanges, and post-season gear choice.", new[]{"Unclaimed pass reward", "Unused season exchange", "Tuvala not finalized", "Character-bound reward on wrong class"}),
        new("Open quest lists that matter", "Progression", "O → Main / Suggested / Recurring tabs; Black Spirit (/) for related objectives", "Suggested and recurring quests contain account unlocks the main quest does not surface clearly.", "Use the current goal to filter. Prioritize inventory, pets, family systems, Magnus, season, journals, and prerequisite unlocks instead of accepting every icon on the map.", new[]{"Quest type hidden in UI settings", "Prerequisite not completed", "Wrong character", "Level or region requirement"}),
        new("Manage workers and nodes", "Life Skills", "World Map (M) → city/node → worker and contribution interfaces", "Workers need lodging, stamina/food, connected nodes, and storage space.", "Choose a production chain first. Connect only the nodes needed for that chain, feed workers, and keep destination storage clear. Avoid spending contribution on a spaghetti empire with no output plan.", new[]{"No lodging", "Node not connected", "Worker out of stamina", "Storage full", "Insufficient contribution"}),
        new("Configure pets", "Account", "ESC → Adventure/Function menu → Pets", "Pets provide loot pickup and talents; groups prevent constant manual swapping.", "Build pet groups for grinding, life skills, and utility. Check hunger, tier, special skills, and active group before assuming loot pickup is broken.", new[]{"Pet not checked out", "Hunger depleted", "Pickup interval too slow", "Wrong pet group"}),
        new("Configure fairy and consumables", "Account", "ESC → Adventure/Function menu → Fairy", "Fairy skills can automate allowed consumable use inside the game's own system.", "Use only in-game fairy features. Set potion thresholds and confirm the correct potion family. This is legitimate game functionality, not external automation.", new[]{"Skill not learned", "Wrong potion selected", "Potion unavailable", "Cooldown or threshold mismatch"}),
        new("Use campsite/tent", "Convenience", "Campsite icon/hotkey → shop, villa buff, repair, storage functions as owned", "Tent value depends on which premium functions you repeatedly use.", "Before buying, test the friction it solves and score the current package in Pearl Shop Guard. Do not buy a duplicate or an expensive bundle padded with consumables you would not purchase separately.", new[]{"Function requires premium tent component", "Region or buff prerequisite", "Duplicate owned utility", "Package sale ending"}),
        new("Tag characters and share gear", "Progression", "Character Tag interface → Item Copy during eligible conditions/events", "Tagging and item copy are separate from ordinary storage transfer.", "Read current costs and restrictions before committing. Trade items, movement, special regions, and copied gear can have rules that affect both characters.", new[]{"Characters in different towns", "Trade item held", "Tag restriction active", "Item copy event ended"}),
        new("Edit the in-game UI", "Interface", "ESC → Settings → Edit UI", "Hide unused panels, save presets, and reduce screen noise.", "Create a clean grinding preset and a management preset. Keep critical warnings, buffs, cooldowns, minimap, party, and loot information visible. Reset only the broken element instead of the whole UI.", new[]{"Preset not saved", "Resolution/UI scale changed", "Panel hidden behind another", "Combat/non-combat layout differs"}),
        new("Cancel or review a Pearl purchase", "Pearl Shop", "Pearl Shop → Purchase History / cancellation interface", "Check cancellation before opening, using, equipping, or registering contents.", "Preserve screenshots of the offer, contents, price, sale period, and binding. Opening or consuming contents can remove cancellation eligibility.", new[]{"Item already opened/used", "Partial contents consumed", "Cancellation window passed", "Gifted or special product restrictions"}),
        new("Know what not to buy", "Pearl Shop", "Pearl Shop Guard → enter exact offer and contents", "Avoid RNG, duplicates, character-bound convenience on an uncertain main, and consumable padding.", "A large discount is not value when most contents are unwanted. Prefer permanent family-wide utility that fixes repeated friction, then compare against free/event alternatives and the monthly budget.", new[]{"Random contents", "Jackpot-focused marketing", "Duplicate utility", "Character-bound purchase", "Free alternative available"})
    };
}
