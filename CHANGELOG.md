# Cuda Spirit 2.4.1

## Obsidian UI major rework

- Replaced the rounded, pill-heavy visual language with crisp 3-4 px geometry.
- Rebuilt the main shell, title bar, command palette, navigation, status labels, cards, tabs, inputs, overlays, and selection controls.
- Removed decorative glow clouds and oversized card shadows in favor of matte layered surfaces and accent rails.
- Changed the default theme to Obsidian Gold and retuned every alternate theme for lower saturation and stronger readability.
- Reduced page-motion distance and duration.
- Added a proper GitHub release builder that emits a self-contained Nexus package requiring no .NET runtime or SDK for end users.
- The source bootstrapper now uses any compatible installed stable .NET 8+ SDK before downloading the private 8.0.423 fallback.

# Changelog

## 2.4.1

### Full Windows compiler correction

- Fixed all five errors reported by the real V2.3.1 Windows build.
- Replaced four invalid two-argument WPF `Thickness` constructor calls with valid four-edge constructors.
- Added an explicit `System.IO` import and fully qualified `Directory.Exists` in `DataCenterView`.
- Expanded the source validator to reject every two- or three-argument `Thickness` constructor in C# and to detect unqualified `Directory`/`File` use in the WPF project without `System.IO`.
- Added exact regression guards for `AppearanceService`, `MainWindow`, and `DataCenterView`.
- Updated the Nexus installer to locate and reuse SDK 8.0.423 from any adjacent Cuda Spirit Nexus folder, including V2.3.1.
- Bumped application, source, installer, workflow artifact, and network metadata to 2.4.1.

## 2.3.1

### Compiler hotfix

- Fixed C# compiler error CS0165 in `PearlShopAdvisor`: the nullable no-spend-shield deadline is now assigned before the boolean check and formatted through `GetValueOrDefault()`.
- Added a source-validator regression guard for this exact definite-assignment failure.
- Updated the Nexus installer to reuse the exact private .NET SDK 8.0.423 from an adjacent V2.3 installation when available, avoiding another 285 MB download.
- Bumped application, core, network user-agent, installer verification, CI artifact, and package metadata to 2.3.1.

## 2.3.0

### Premium interface and GitHub release

- Rebuilt the shared WPF visual system with OLED-first surfaces, refined typography, restrained gradients, consistent spacing, and accessible focus states.
- Reworked the frameless shell, title bar, global command palette, status treatment, navigation hierarchy, and content margins.
- Made compact navigation a true icon rail with contextual tooltips and compact overlay controls.
- Added reduced-motion-aware page transitions and improved density scaling.
- Restyled buttons, inputs, dropdowns, checkboxes, radio buttons, list rows, tabs, data grids, progress bars, tooltips, and scrollbars.
- Rebuilt Navigator, Returner & Reroll Recovery, Customize, Advisor Chat, and the floating Advisor window.
- Renamed the developer distribution from Discord Source to GitHub Source and added repository-ready documentation and workflow metadata.
- Bumped application, core, network user-agent, source package, and CI artifact metadata to 2.3.0.

## 2.2.0

### Returner and reroll recovery

- Added the Returner & Reroll Recovery Center.
- Added a one-click rescue flow that refreshes linked profile data, synchronizes every source, creates Navigator tasks, and enables a seven-day no-spend shield.
- Added a personalized returner plan based on time away, inventory state, gear certainty, class certainty, season status, rewards, spending risk, and current goal.
- Added a fail-closed character-retirement checklist covering inventory, Pearl value, gear, crystals, artifacts, mounts, ships, workers, quests, tags, season state, rewards, trade/guild items, and deletion cooldowns.
- Added a class play-feel matcher and safer fresh-character starting guidance.

### Anti-predatory purchase protection

- Added a persistent `PearlSpendingFreezeUntilUtc` setting.
- Pearl Shop evaluations are hard-blocked while the no-spend shield is active.
- Strengthened penalties for urgency language, unverifiable discounts, padding, consumable value, duplicate convenience, temporary subscriptions, character binding, RNG, and reroll sinks.
- Added visible shield state to the Pearl Shop Guard.
- Added an explicit, confirmation-gated control so users can clear their own shield without editing settings files.

### Autonomous synchronization

- Stored official profile tokens now actually refresh on launch and at a restrained six-hour cadence.
- Profile and optional Garmoth failures are isolated so they cannot block every other source.
- Added independent source schedules for patch notes, guides, events, Pearl notices, class data, market data, alerts, and local exports.
- Degraded sources now retry after a controlled 15-minute window instead of waiting their full healthy interval.
- Added rendered WebView2 fallback for official pages blocked by anti-bot protection.
- Added an automatic `%APPDATA%\CudaSpirit\imports` folder and recursive debounced JSON watching.
- Preserved incremental import state so unchanged large exports are skipped.

### Release engineering

- Bumped application and core assemblies to 2.2.0.
- Added cross-OS Windows targeting metadata and a Windows release workflow.
- Expanded the structural validator to the final 83 C# and 27 XAML/project files.
- Added a self-building Nexus installer that installs the pinned SDK locally, compiles, launch-tests, and runs the app without administrator rights.

## 2.1.0

### Guided cockpit

- Added Navigator as the default personalized home and next-action queue.
- Added profile-aware plans for brand-new, season, graduated, returning, midgame, endgame, life-skill, PvE, PvP, collection, casual, free-to-play, and spender workflows.
- Added persistent custom tasks, completion state, priority, cadence, due dates, source links, and pinned ordering.
- Added global Ctrl+K command search with intent routing and AI fallback.

### Item and inventory safety

- Added Item Intel with exact item-name and tooltip analysis.
- Added conservative stop/keep/store/transfer/use/sell/conditional verdicts.
- Added binding, expiry, season, outfit, coupon, enhancement, treasure, crystal, boss-heart, worker, trade, guild, and junk/exchange safeguards.
- Added Transfer Lab for storage, Magnus, maids, Family Inventory, family-character access, and Central Market warehouse eligibility.
- Added before-you-act checklists, blockers, confidence, matched rules, storage recommendations, and decision history.
- Added direct AI escalation containing the exact tooltip and deterministic safety result.

### Rewards and UI decoding

- Added Reward Claim Center covering Web Storage, coupons, Mail, Black Spirit's Safe, Challenge rewards, attendance, passes, event pages, and guild rewards.
- Added live official event/coupon records and expiry-prioritized advisor escalation.
- Added UI Decoder with searchable menu paths, hotkeys, prerequisites, safety fallbacks, and common blockers for 18 high-friction workflows.

### Pearl Shop Guard

- Added deterministic offer scoring using real versus original price, discount quality, permanence, family-wide value, character binding, randomness, free alternatives, duplicates, budget, owned utility, and spending style.
- Added warnings for RNG boxes, reroll sinks, duplicate convenience, consumable padding, uncertain-main purchases, and temporary subscription value.
- Added official Pearl-notice ingestion, selected-notice AI analysis, and evaluation history.

### Personalization and polish

- Added Black Spirit, Abyssal OLED, Serendia Gold, Kamasylvia, and Snowfield live themes.
- Added font scaling, density modes, compact navigation, reduced-motion preference, beginner hints, and startup-page selection.
- Added class, stage, current goal, play focus, weekly hours, Pearl budget, spending style, Magnus, maid, and tent profile fields.
- Converted shared appearance resources to live dynamic resources.
- Reorganized navigation into Guided, Tools, and App sections.

### Knowledge and AI

- Added official guide ingestion for coupons, storage, inventory, Family Inventory, maids, World Map, Central Market, seasons, campsite, purchase cancellation, enhancement, and quality-of-life systems.
- Added current official event and Pearl Shop notice ingestion.
- Upgraded curated safety knowledge to version 4.
- Added user preferences, owned convenience, open tasks, recent item decisions, and Pearl evaluations to retrieved AI context.
- Strengthened fail-safe rules for irreversible actions, binding, expiry, region, freshness, and missing evidence.

### Database and validation

- Upgraded the database to schema version 4.
- Added `companion_task`, `item_decision_history`, and `pearl_evaluation_history`.
- Added schema/FTS smoke tests for the new tables.
- Added project-level validation for XAML parsing, event wiring, dynamic-resource references, C# literal/delimiter structure, schema execution, FTS triggers, route foreign keys, and release hygiene.
- Fixed an advisor-context task-query argument mismatch found during final validation.

## 2.0.0

### Knowledge and live data

- Rebuilt the database as a versioned, provenance-aware SQLite knowledge store.
- Added source state tracking with attempt/success timestamps, status, errors, and record counts.
- Added FTS5 full-text indexing with automatic migration rebuild and LIKE fallback.
- Added official regional update/patch-note ingestion.
- Added bounded Arsha market hot-list ingestion and market-history observations.
- Added optional BDO Alerts ingestion for bosses, resets, news, maintenance, coupons, and supported market regions.
- Isolated optional BDO Alerts endpoint failures so partial success is retained.
- Added recurring automatic synchronization with configurable cadence.
- Added retention for expired live records, old market history, and AI cache entries.

### Local catalog

- Added recursive read-only JSON imports for items, recipes, skills, nodes, grind zones, and routes.
- Added per-file incremental import state so unchanged exports are skipped.
- Added file-size, file-count, object-count, and JSON-depth safety bounds.
- Added source-specific path hashes to avoid collisions between same-named files in different folders.
- Added route-edge endpoint validation and case normalization.

### AI advisor

- Replaced broad context dumping with query-aware database retrieval.
- Added record limits and character budgets.
- Added source, region, effective time, retrieval time, confidence, and source-health context.
- Added stable OpenRouter automatic and free fallback routes.
- Fixed empty fallback serialization.
- Strengthened no-botting/no-client-control system rules.

### Route planning

- Added grind/farm ranking by expected value, AP/DP fit, travel, objective, and risk.
- Added graph route planning with fastest, balanced, highest-value, and lowest-risk objectives.
- Added coordinate-based fallback estimates for incomplete graphs.
- Fixed path reconstruction edge ordering.
- Enforced non-negative path costs so Dijkstra cannot enter profit-credit cycles.

### UI and reliability

- Added Live Data Center and Farm Route Optimizer.
- Replaced stale hard-coded grind tables with imported current data.
- Added source/database statistics and knowledge search.
- Expanded technical settings for live sources, local imports, sync cadence, retrieval limits, and model fallbacks.
- Added live-source shortcuts to the reference browser.
- Made the navigation rail scrollable.
- Fixed stale market-cache fallback behavior.
- Fixed automatic-sync first-run and due-state logic.
