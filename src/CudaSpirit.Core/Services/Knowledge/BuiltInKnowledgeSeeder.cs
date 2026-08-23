using System.Text.Json;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>Versioned safety and onboarding knowledge available before the first network sync.</summary>
public sealed class BuiltInKnowledgeSeeder
{
    public const string SourceId = "cuda-curated-guide-v6";
    private readonly AppDatabase _db;

    public BuiltInKnowledgeSeeder(AppDatabase db) => _db = db;

    public int Seed()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new List<KnowledgeRecord>
        {
            Guide("returner-recovery", "Returning player recovery order",
                "Do not start by opening boxes, spending Pearls, selling old gear, or deleting characters. First sync current notices and guides, claim only rewards with known expiry, inventory every character and storage with Ctrl+F, inspect equipped gear and crystals, identify season eligibility, restore one playable character, then choose one current goal. Old advice and tier lists may be invalid after class, gear, quest, and progression changes.",
                "returning player comeback recovery current patch inventory rewards gear season goal", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=259"),
            Guide("character-retirement", "Character deletion is a controlled retirement",
                "Before deleting any character, remove or document equipped gear, artifacts, lightstones, crystals and presets; empty inventory and Pearl inventory; move marketable and family items; park or transfer mounts; resolve tags and copied gear; inspect quests, season status, life skills, energy, contribution-related assets, names, presets, cooldowns, and deletion blockers. Keep screenshots and do not proceed while any critical check is unknown.",
                "delete character retirement checklist pearl inventory gear crystal mount tag quest season cooldown", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=13"),
            Guide("season-first-character", "Default starting choice: create a season character",
                "For an eligible new or returning account, a season character is normally the safest default because the season path concentrates catch-up gear, progression objectives, and rewards. Verify the currently active season rules before creation, use a trial character to test combat feel first, and avoid assigning character-bound Pearl convenience until the class is confirmed.",
                "first character season new returning starting choice class trial", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=237"),
            Guide("class-choice", "Choose a class by play feel, not a frozen tier list",
                "Class balance and grind rankings change. Narrow choices by desired range, pace, action intensity, durability, mobility, group role, PvE/PvP/life-skill focus, and hand comfort. Create trial characters for the finalists, test both succession and awakening where available, then use a season character or tag/item-copy system rather than buying character-bound convenience immediately.",
                "choose class trial character succession awakening apm range mobility durability tag", "https://www.naeu.playblackdesert.com/en-US/GameInfo/Class"),
            Guide("returner-spend-freeze", "Returning-player spending freeze",
                "Use a temporary Pearl spending freeze until current systems, owned convenience, active events, free alternatives, and the long-term main are known. Returning accounts often already own unopened rewards, coupons, pets, maids, outfits, subscriptions, or infrastructure scattered across mail, Web Storage, Black Spirit's Safe, characters, and town storage.",
                "returning player pearl spending freeze duplicate rewards mail storage", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=254"),
            Guide("reward-inboxes", "Where Black Desert rewards actually go",
                "Check all reward surfaces before assuming something is missing: Web Storage on the website, in-game Mail (B), Black Spirit's Safe, Challenge rewards (Y), attendance/login rewards, event and pass interfaces, guild rewards, and Pearl Shop coupon book. Web Storage and mail can have expiration. Sending a website reward to a character may be irreversible, so verify server and target character first.",
                "rewards redemption coupon web storage mail black spirit safe challenge attendance pass expiry claim", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=165"),
            Guide("item-safety", "The irreversible-action rule",
                "Do not open, equip, register, enhance, extract, melt, delete, or vendor an unfamiliar item until you have checked its exact name, binding, expiration, class selection, character selection, marketplace status, NPC Exchange use, and current progression purpose. Put uncertain items in a named review storage and search the exact name.",
                "item keep sell open bind character family expiration safety warning", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=13"),
            Guide("transfer-map", "How to move an item to another character",
                "Start with Find My Item (Ctrl+F). Non-character-bound items may be moved through same-town storage, Magnus-linked storages, storage maids, direct character-inventory retrieval through the character/world-map interface, or the Central Market warehouse when eligible. Family Inventory only accepts eligible categories. Character-bound, guild, trade/barter, and certain treasure items can be blocked.",
                "transfer item another character storage maid magnus ctrl f family inventory central market", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=150"),
            Guide("binding-types", "Binding language to stop and read",
                "Character-bound normally means the item stays on that character. Family-bound means the family can use it only through allowed systems; it does not guarantee every storage or transfer method. Some boxes become bound only when opened, registered, equipped, or used. Character-specific coupons, inventory, weight, outfits, and similar products should stay unopened until the permanent target character is certain.",
                "bind on pickup character bound family bound box open register equip", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=13"),
            Guide("season-checklist", "Season character and graduation safety checklist",
                "Before graduation, complete the current season pass requirements, claim Black Spirit Pass rewards if applicable, resolve Tuvala upgrades and exchanges, verify remaining season materials, and review every graduation reward. Do not use premium enhancement materials or character-bound convenience items on a temporary class without a written reason.",
                "season graduation tuvala rewards pass claim fughar gear", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=241"),
            Guide("pearl-priority", "Pearl Shop priority framework",
                "Highest-value purchases are usually permanent family-wide utility that solves repeated friction. Conditional value includes maids, character slots, family inventory, Naderr infrastructure, and a campsite when the premium functions matter. Character-bound weight/inventory should wait until the main is certain. Temporary buffs, enhancement consumables, reroll items, duplicates, and random boxes usually need a large discount and a specific plan.",
                "pearl shop value buy avoid tent maid character slot weight inventory rng artisan cron", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=254"),
            Guide("pearl-rng", "Why discounted random boxes can still be bad value",
                "A percentage discount does not make a random box good value. Compare the probability-weighted expected value of all outcomes, ignore jackpot marketing, account for items you would not have bought directly, and set the value of duplicates near zero. Treat unavailable-in-Belgium/Netherlands notices as an additional gambling-risk signal.",
                "pearl random chance adventure box probabilities expected value gambling avoid", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=181"),
            Guide("pearl-refund", "Pearl purchase cancellation safety",
                "Check purchase history and cancellation eligibility before opening, using, equipping, registering, or partially consuming Pearl items. Once contents are used or changed, restoration can be restricted. Keep a screenshot of the offer, contents, price, sale period, and binding notices for expensive purchases.",
                "pearl purchase history cancel refund restore opened used", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=254"),
            Guide("storage-system", "Use storage names and roles",
                "Create storage roles instead of dumping everything together: Review/Unknown, Enhancement, Season, Event/Expiring, Treasure, Life-skill raw materials, Life-skill intermediates, Boss/weekly rewards, and Sell/Market. This makes Ctrl+F results and AI advice actionable and prevents accidental sale or use.",
                "storage organization unknown review enhancement season treasure event", "https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=43"),
            Guide("market-sale", "Before selling an item",
                "Compare NPC vendor value, NPC Exchange uses, crafting demand, Central Market price and liquidity, tax, future progression uses, and whether the item is rare or time-gated. Never list an item merely to transfer it between characters; use the Central Market warehouse without registering it for sale.",
                "sell market vendor exchange tax liquidity item transfer warehouse", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=47"),
            Guide("enhancement-resources", "Rare enhancement resources to preserve",
                "Advice of Valks, Valks' Cry, Origins of Dark Hunger, hammers, Cron Stones, Memory Fragments, Artisan's Memory, Caphras Stones, rare hearts, and exchange coupons are strategic resources. Their correct value depends on the current gear roadmap and expected enhancement cost. Avoid spending them to solve low-tier impatience.",
                "advice valks cron memory caphras hammer heart enhancement keep", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=48"),
            Guide("daily-weekly", "Minimal daily and weekly cockpit",
                "Daily: check expiring rewards, login/pass claims, active events, workers/pets if relevant, and the one progression action tied to the current goal. Weekly: review bosses and weekly content, season/pass deadlines, market alerts, Pearl coupons, storage cleanup, and progression roadmap. Do not let checklist volume replace the user's chosen goal.",
                "daily weekly checklist reset reward event goal focus", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=365"),
            Guide("find-my-item", "Find My Item is the first inventory tool",
                "Press Ctrl+F to search owned items across family locations. The result can identify characters, town storage, and Central Market warehouse, and may offer retrieval through the correct maid. Use this before buying a duplicate, moving multiple characters, or assuming an item vanished.",
                "find my item ctrl f inventory search maid duplicate lost", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=150"),
            Guide("world-map-character", "Manage family characters from the world map",
                "The world map family-character interface can show character location and inventory, move characters under specific conditions, and retrieve eligible items with maids. Character movement takes time and has restrictions for tagged characters, trade goods, and special regions.",
                "world map family character move inventory tagged trade goods", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=21"),
            Guide("market-warehouse", "Central Market warehouse is not the sale list",
                "Eligible items can be deposited into the Central Market warehouse and withdrawn by another character in the family without registering them for sale. Registering a sale is a separate irreversible market action that may incur tax and waiting/liquidity risk. Use Manage Warehouse for transfer and Register Item only when you actually intend to sell.",
                "central market warehouse transfer register sale tax family item", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=47"),
            Guide("exchange-before-vendor", "NPC Exchange can make vendor trash valuable",
                "Before selling unusual tokens, seals, remnants, event coins, pity pieces, boss drops, or regional materials, inspect the tooltip's NPC Exchange section and search the exact item. Exchange value can exceed vendor or market value and may unlock gear, failstacks, furniture, pets, crystals, or progression materials.",
                "npc exchange vendor token seal event coin pity item sell keep", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=13"),
            Guide("character-convenience", "Character-bound convenience belongs on a confirmed main",
                "Inventory expansion, weight limit, flute/horn variants, outfit boxes, skill preset items, and some coupons can become character- or class-specific. Keep them unopened until the class, succession/awakening plan, and long-term use are certain. A sale does not fix a bad permanent assignment.",
                "character bound inventory weight outfit coupon main class pearl", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=254"),
            Guide("item-disabled", "Why an item or button can be greyed out",
                "Common blockers include wrong character or class, character/family binding, season-only rules, item equipped or locked, full inventory/weight, active trade goods, tagged-character restrictions, party/combat/mount state, special-region rules, cooldown, expired item, missing prerequisite quest, and an incompatible target. Do not delete the item as a troubleshooting step.",
                "item grey disabled cannot open use transfer blocker class season tagged", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=13"),
            Guide("quest-filter", "Use quest categories instead of chasing every icon",
                "The Quest window's Main, Suggested, and Recurring categories contain different unlocks. Prioritize the current season/main path and account-wide unlocks such as inventory, pets, Magnus, family systems, journals, and prerequisites. Quest-type display settings can hide NPC icons and make available quests appear missing.",
                "quest window main suggested recurring filter hidden icon prerequisite", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=9"),
            Guide("ui-presets", "Build separate in-game UI presets",
                "Use Edit UI to create a clean combat/grinding preset and a management preset. Keep critical warnings, buffs, cooldowns, minimap, party, loot, weight, and durability visible. Save the preset after changes and repair individual panels before resetting the entire interface.",
                "edit ui preset combat grind management hide panel reset", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=11"),
            Guide("tag-item-copy", "Character Tag and Item Copy are not normal transfers",
                "Tagging links two characters under current game restrictions, while Item Copy creates governed copies of eligible gear. Read current costs, events, location requirements, trade-item restrictions, and untag rules before committing. Copied gear is not ordinary family inventory and should not be treated as freely transferable.",
                "character tag item copy gear transfer restriction trade item", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=363"),
            Guide("crystal-safety", "Crystal presets still require loss-risk awareness",
                "Save named crystal presets for content types, verify the active preset before entering combat, and check current destruction/protection rules before using expensive crystals. Do not assume a preset, extraction tool, or restoration policy makes every crystal risk-free.",
                "crystal preset destruction extraction protection restore combat", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=246"),
            Guide("pearl-offer-audit", "Audit the exact Pearl bundle, not its headline discount",
                "Break a bundle into items you would buy directly, items you might use, and padding worth zero to you. Subtract duplicates and free/event alternatives, penalize RNG and character binding, then compare the useful-value total against the sale price and monthly budget. Sale percentage alone is not a value score.",
                "pearl bundle discount value padding duplicate free alternative budget", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=254"),
            Guide("purchase-proof", "Preserve proof before an expensive Pearl purchase",
                "Capture the exact product page, included items, probabilities when applicable, sale dates, binding notices, purchase limit, and cancellation language. Review Purchase History before opening or consuming anything if the offer is wrong. Screenshots make support and self-auditing far easier.",
                "pearl purchase screenshot history cancel probability sale date bind", "https://www.naeu.playblackdesert.com/en-us/Wiki?wikiNo=254"),
            Guide("ap-dp-brackets", "AP/DP brackets: the hidden multiplier on every upgrade",
                "Sheet AP (equipment window) earns bonus AP at fixed thresholds: 100(+5), 140(+10), 170(+15), 184(+20), 209(+30), 235(+40), then 245/249/253/257/261/265/269 in fast steps (+48 to +137 total), 273-309 in +4 steps (up to +200), and beyond 309 every few points add +3 more (309=+200, 341=+214, 375=+233, 449=+297). Brackets apply separately to main-hand and awakening AP. DP brackets add +1% damage reduction per bracket from 203 (1%) to 401+ (30%). A 1-AP Caphras tap that crosses a bracket is worth 10-20 effective AP - always check the next threshold before big purchases. Hover the (?) next to AP/DP in game to see your current bracket bonuses.",
                "ap dp bracket bonus threshold caphras tap upgrade breakpoint damage reduction sheet", "https://www.blackdesertfoundry.com/ap-and-dp-brackets-guide/"),
            Guide("grind-spot-tiers", "Current grind-spot tiers by gear (pre-tax silver/hour with blue loot scroll)",
                "Rough current ladder: 100-190 AP: Titium Valley, Desert Naga, Bashim, Polly's Forest, Fadus/Loopy Tree (also season-friendly). 200-260 AP: Sycraia Upper, Shultz/Sausans, Mirumok 2-3P, Roud Sulfur, Gyfin Upper 5P, Murrowak's Labyrinth (winter). 260-300 AP: Gyfin Underground, Olun's Valley 3P base, Turos base, Crypt of Resting Thoughts, Jade Starlight Forest. 300+ AP: Dehkia spots (Turos 2P, Thornwood, Cyclops, Olun's 3P, Ash Forest, Crescent Shrine, Hystria, Aakman, Pila Ku), Quint Hill Trolls, Hexe Sanctuary, City of the Dead, Yzrahid Highlands, and Tungrad Ruins at the top (~320 AP / 410 DP, ~1.1B/h). Values swing with market trash prices - check the Route Planner for the live table and your gear fit.",
                "grind spot tier silver hour ap dp dehkia trash loot agris money farming", "https://grumpygreen.cricket/bdo-grinding-spots/"),
            Guide("season-path", "Season character path: Naru to PEN Tuvala to graduation",
                "Season characters gear up entirely through Tuvala: enhance Naru (main quests) to PEN with beginner stones, exchange for Tuvala, then push every Tuvala piece to PEN with Time-Filled Black Stones and refined stones - no Caphras, no boss gear, no failure downgrades below TRI-level risks that normal gear has. Do the season pass alongside the main quest for materials, pets, and inventory. Before graduating: finish the pass, use Fughar's Timepiece, exchange leftover season materials, and choose early vs natural graduation. Graduation converts Tuvala to family-bound gear (TET->PEN-equivalent via boss gear conversion coupons). Season spots (Titium, Naga, Polly's, Fadus, Mirumok, Sycraia) are tuned for this gear band.",
                "season character tuvala naru pen graduation fughar timepiece season pass simplified quest", "https://grumpygreen.cricket/season"),
            Guide("world-boss-rotation", "World bosses spawn in paired windows on a weekly rotation",
                "NA/EU PC world bosses spawn two-at-a-time in shared windows (e.g. Kzarka+another at the same minute), with Garmoth on a fixed daily line and Vell/Offin on specific days. Quint and Muraka are field bosses on a 15-minute timer. The Bosses tab lists the next spawns in your local time from the built-in NA/EU rotation tables; if a spawn looks wrong after a game update, cross-check the official wiki world-boss page or garmoth.com/boss-timer. Boss loot needs damage contribution - bring at least recommended-AP gear for the boss tier.",
                "world boss schedule kzarka kutum karanda nouver offin vell garmoth quint muraka sangoon bulgasal uturi pig king spawn time", "https://www.naeu.playblackdesert.com/Wiki?wikiNo=83"),
            Guide("pearl-shop-deals", "Pearl Shop deals rotate on Thursdays - stack coupons on sales",
                "New products and refreshed deals land Thursday after maintenance, and most sale windows run Thursday-to-Thursday (about two weeks). Discount Coupons sit in the Coupon Book (F3), often expire within days, and STACK on top of sale prices - a 20% coupon on an already-discounted Value Pack is the cheapest pearls-per-day in the game. Deep Value Pack discounts (seen up to -40%) rotate back periodically. Bonus events (extra-pearl boxes, 2-for-1-style pearl/outfit deals) cluster around celebrations and major updates - pairing one with a coupon is historically the best stacking moment. Family purchase limits are per-offer, not lifetime. Playbook: wishlist it, never pay launch price, wait for sale+coupon overlap, check the family limit first, then run the offer through the Pearl Shop tab's evaluator.",
                "pearl shop deals sale coupon rotation thursday value pack 2 for 1 discount outfit bonus box", "https://www.naeu.playblackdesert.com/en-US/News/Detail?groupContentNo=9596&countryType=en-US"),
            Guide("market-mechanics", "Central Market ticks, queues, and how listings really resolve",
                "Prices move only at 5-minute ticks, each repricing one alternating category group (half the market per tick, ~10-minute cycle; EU anchor about 19:18/19:28 server time). To snipe a listing, sit on the item as its group's tick approaches; to sell into demand, register just before your group ticks. Arrows mark the rails: up-arrow = true max (price cannot rise, buys become an RNG lottery shared by all pre-orders), max without arrow can still climb, min without down-arrow can still fall. Cheap non-queue items sell strictly first-listed-first-sold - never press Relist, it resets your position to the back of the queue. Registration-queue items (rare/expensive) sell at random at max or true-max price, first-come-first-served below, random again at true min. Multiple pre-orders do not multiply win odds - they let you win whole larger lots when you do win. Pre-order play: place a low order to get listing notifications, raise it inside the 15-minute window only if needed; a visible max-price order just tells sellers what to charge you. Pearl-item registration counts reset Mondays 00:00 UTC.",
                "central market tick timing snipe pre order true max true min random first come first serve relist queue position pearl item registration reset", "https://www.youtube.com/watch?v=gxpKhQiv0yU"),
        };

        var count = _db.UpsertKnowledgeBatch(records);
        _db.UpsertSourceState(new DataSourceState
        {
            SourceId = SourceId,
            DisplayName = "Cuda curated safety guide",
            Status = "ready",
            LastAttemptAt = now,
            LastSuccessAt = now,
            LastRecordCount = records.Count,
            MetadataJson = JsonSerializer.Serialize(new { version = 6, purpose = "onboarding-returner-retirement-class-choice-ui-decoder-item-safety-pearl-value-market-mechanics-deals" })
        });
        return count;
    }

    private static KnowledgeRecord Guide(string id, string title, string content, string tags, string url) => new()
    {
        SourceId = SourceId,
        ExternalId = id,
        Kind = id.Contains("pearl", StringComparison.OrdinalIgnoreCase) ? KnowledgeKinds.PearlShop :
               id.Contains("item", StringComparison.OrdinalIgnoreCase) || id.Contains("binding", StringComparison.OrdinalIgnoreCase) ? KnowledgeKinds.ItemSafety : KnowledgeKinds.Guide,
        Title = title,
        Summary = KnowledgeText.Truncate(content, 420),
        Content = content,
        Url = url,
        Region = "global",
        Tags = tags,
        MetadataJson = "{\"curated\":true,\"version\":5}",
        Confidence = 0.94,
        EffectiveAt = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
        RetrievedAt = DateTimeOffset.UtcNow,
        ContentHash = KnowledgeText.Hash(title, content, url)
    };
}
