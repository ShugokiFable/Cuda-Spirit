<p align="center">
  <img src="src/CudaSpirit.App/cuda-spirit.png" width="128" alt="Cuda Spirit"/>
</p>

<h1 align="center">Cuda Spirit 2.4.1</h1>
<p align="center"><strong>A personalized Black Desert recovery, safety, live-data, and decision cockpit</strong></p>

Cuda Spirit is built for the part of Black Desert that punishes uncertainty: scattered reward inboxes, unexplained binding and transfer rules, irreversible item choices, character deletion traps, volatile progression advice, and Pearl Shop offers that look better than they are.

Version 2.4.1 combines deterministic safety engines, a versioned local SQLite knowledge store, current public-source synchronization, a returning-player recovery system, and retrieval-focused AI guidance. Unknown or stale facts fail closed instead of becoming confident guesses.

## GitHub source package

This distribution is the repository-ready source tree. It includes `.gitignore`, `.gitattributes`, Dependabot configuration, a Windows build-and-window-smoke-test workflow, reproducible checksums, and [repository setup instructions](REPOSITORY_SETUP.md). No license is invented or implied; add the license you intend before opening outside contributions.

## What 2.3 adds


### High-end visual cockpit

- Rebuilt the window shell with an OLED-first surface system, restrained accent lighting, clearer hierarchy, and consistent spacing.
- Added a global command palette with a proper placeholder, keyboard focus states, and direct tool routing.
- Compact navigation is now a true icon rail instead of a slightly narrower sidebar.
- Added polished cards, status pills, buttons, text fields, dropdowns, list selections, tabs, data grids, tooltips, and scrollbars across the application.
- Added density-aware spacing, accessible focus outlines, reduced-motion-aware page transitions, and live theme updates.
- Reworked Navigator, Recovery Center, Customize, and the floating Advisor overlay as visual showcase surfaces.

### One-click returner rescue

The Recovery Center can perform one guided recovery action:

- refresh a linked official adventurer profile when available;
- enrich gear state from an optional public Garmoth profile;
- synchronize current guides, patch notes, events, Pearl notices, classes, market data, alerts, and local exports;
- build an ordered recovery plan and add it to Navigator;
- activate a seven-day no-spend shield so comeback excitement cannot become a bad Pearl purchase.

Each source fails independently. A blocked profile page cannot prevent market, event, guide, or local-data synchronization.

### Returning-player plan

The plan adapts to time away, inventory chaos, uncertain gear, uncertain main class, season status, unclaimed rewards, reroll intent, spending temptation, and the user’s stated goal. It prioritizes:

1. account and reward recovery;
2. inventory quarantine and Find My Item checks;
3. current-system and patch review;
4. gear and progression re-baselining;
5. season and class trials;
6. only then, upgrades and spending.

### Character retirement gate

A fail-closed checklist blocks a “safe to delete” result until every critical category is manually confirmed, including:

- normal, Pearl, Family, mount, wagon, ship, storage, market, and mailbox inventories;
- equipped gear, artifacts, lightstones, crystals, costumes, tools, and life-skill equipment;
- character-bound coupons, weight, inventory, outfits, titles, and quest items;
- tagged gear and Item Copy state;
- horses, ships, workers, rentals, trade goods, guild items, and remote transports;
- season status, graduation, timepiece or equivalent systems, pending rewards, deletion cooldowns, and final client warnings.

The app cannot inspect the running game. The gate proves that the user checked each risk, not that deletion is magically reversible.

### Class and fresh-character advisor

Class recommendations are based on play feel rather than a frozen tier list: range, pace, complexity, survivability, PvE/PvP focus, support preference, grab preference, and APM tolerance. The workflow favors trial characters, current season paths, and Character Tag/Item Copy before assigning permanent paid value.

### Pearl Shop no-spend shield

The conservative evaluator now hard-blocks paid-offer recommendations during an active shield. It also scores permanence, family-wide utility, binding, randomness, bundle padding, real versus advertised discount, free alternatives, duplicate convenience, budget, playtime, and spending style.
The shield remains user-controlled and can be cleared from Pearl Shop Guard after an explicit confirmation.

### Autonomous data pipeline

- Fast feeds and stable guides use separate schedules.
- Degraded sources retry after a controlled 15-minute window instead of remaining stale for a day.
- Official pages fall back to a rendered WebView2 browser when raw requests encounter bot protection.
- Linked profiles refresh on launch and then at a restrained six-hour cadence.
- `%APPDATA%\CudaSpirit\imports` is created automatically and watched recursively for supported JSON exports.
- File changes use debounced incremental import, content state, size limits, object limits, and unchanged-file skipping.
- Every record stores source, region, confidence, retrieval time, effective time, expiry, and content hash.
- SQLite FTS5 returns only relevant records to the AI advisor.

## Existing cockpit

- **Navigator:** persistent personalized priorities, due dates, cadence, pins, completion, and custom tasks.
- **Reward Claim Center:** website coupons, Web Storage, Mail, Black Spirit’s Safe, Challenges, attendance, passes, event pages, and guild rewards.
- **Item Intel:** stop, keep, store, transfer, use, sell, conditional, or unknown verdicts from exact item names and tooltips.
- **Transfer Lab:** ordinary storage, Magnus, storage maids, transaction maids, Family Inventory, Central Market warehouse, and family-character access.
- **Pearl Shop Guard:** deterministic value scoring plus current official notice retrieval.
- **UI Decoder:** searchable human-language paths through Black Desert’s interface maze.
- **Farm Route Optimizer:** AP/DP fit, expected value, travel overhead, objective, risk, and graph routing.
- **Live Data Center:** source health, freshness, record counts, imports, search, and manual synchronization.
- **AI Advisor:** source-attributed retrieval with profile, tasks, decisions, evaluations, freshness, and model fallback context.
- **Personalization:** five themes, font scaling, density, compact navigation, reduced motion, beginner hints, startup page, progression profile, budget, and owned conveniences.
- **Ctrl+K:** global command routing and AI fallback.

## Safety boundary

Cuda Spirit is a companion and decision-support app. It does not send keyboard or mouse input, move or fight for the player, inject DLLs, manipulate packets, read process memory, alter the game client, evade anti-cheat, install `.pak` files, or provide unattended gameplay.

## Application data

```text
%APPDATA%\CudaSpirit\
  settings.json
  cudaspirit.db
  imports\
```

Back up this folder before replacing an older build.

## Build

Requirements:

- Windows 10 or 11 x64
- .NET 8 SDK 8.0.423, pinned by `global.json`
- Microsoft Edge WebView2 Runtime for rendered official-page and profile fallback

Run:

```text
publish.bat
```

The script publishes a self-contained single-file Windows executable, strips debug/document sidecars, launches it, verifies that a window appears, and closes the test instance.

The Nexus package includes a separate one-click local builder that installs the pinned SDK into its own folder without requiring administrator rights.

## Source map

```text
src/CudaSpirit.Core/Services/Data/       SQLite, FTS, migrations, tasks, decisions, market and route data
src/CudaSpirit.Core/Services/Guidance/   recovery, retirement, class, item, transfer, Pearl and progression engines
src/CudaSpirit.Core/Services/Knowledge/  official/community sources, local imports, scheduling and retrieval
src/CudaSpirit.App/Views/RecoveryCenterView.*
src/CudaSpirit.App/Views/NavigatorView.*
src/CudaSpirit.App/Views/RewardsView.*
src/CudaSpirit.App/Views/ItemIntelView.*
src/CudaSpirit.App/Views/PearlShopView.*
src/CudaSpirit.App/Views/UiDecoderView.*
src/CudaSpirit.App/Views/CustomizeView.*
```

See `COCKPIT_GUIDE.md`, `DATA_SOURCES.md`, `PRIVACY.md`, `UPGRADE_NOTES.md`, and `VALIDATION_REPORT.md`.

Cuda Spirit is not affiliated with Pearl Abyss.


## Public release packaging

Run `BUILD_NEXUS_RELEASE.bat` on Windows. It uses any installed stable .NET 8+ SDK and creates a self-contained `CudaSpirit.exe`. People downloading that compiled Nexus release need neither a .NET runtime nor an SDK. The source-bootstrapper distribution remains only a fallback when a precompiled binary is unavailable.


## V2.4.1 overlay hotfix

Transparent overlay icons now use vector paths instead of font glyphs or emoji, with device-pixel rounding and layered-window-safe grayscale rendering.
