# Data sources and freshness

Cuda Spirit stores normalized records in SQLite with source id, region, retrieval time, effective time, expiry, confidence, URL, content hash, and source-health state.

## Automatic public sources

- Official Black Desert patch/update notices.
- Official onboarding and system guides.
- Official active event and coupon notices.
- Official Pearl Shop notices.
- Official class catalog pages.
- Arsha Central Market data for supported regions.
- Optional BDO Alerts API data for bosses, resets, maintenance, news, coupons, and supported market feeds.
- Optional public adventurer profile and Garmoth profile enrichment.

## Local read-only imports

The app creates and watches:

```text
%APPDATA%\CudaSpirit\imports
```

Supported JSON families include items, recipes, skills, nodes, grind zones, and route edges. Imports are recursive, debounced, bounded, and incremental. Unchanged files are skipped by stored file state.

## Cadence

- Market and optional alert feeds: configured fast cadence, minimum 5 minutes.
- Active events: at least every 30 minutes.
- Pearl Shop notices and patch notes: at least every 60 minutes.
- Official guides and classes: every 24 hours.
- Linked profile: launch plus a restrained six-hour in-memory retry cadence.
- Degraded sources: controlled 15-minute retry window.
- Local exports: file-change trigger plus scheduled safety scan.

A source failure is isolated. Cached and curated safety records remain available and are marked with their actual freshness.

## AI retrieval

The AI advisor does not receive the whole database. SQLite FTS5 selects the most relevant bounded records, then includes their source, region, confidence, timestamps, expiry, and source-health metadata. Missing evidence is surfaced explicitly.
