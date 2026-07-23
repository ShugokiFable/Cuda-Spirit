# Upgrade notes

## From V1.1, V2.0, V2.1, or V2.2 to V2.4.1

1. Exit every running Cuda Spirit instance.
2. Back up `%APPDATA%\CudaSpirit`.
3. Use the new Nexus installer or run `publish.bat` from source.
4. Start V2.4.1. Existing settings are normalized and the SQLite schema migrates automatically.
5. Open **Customize** and verify region, stage, focus, class, playtime, budget, and owned conveniences.
6. Open **Returner & Reroll Recovery** and use **One-click returner rescue** when coming back after a break.
7. Review **Live Data Center** source health.

## Behavior changes

- The app now creates `%APPDATA%\CudaSpirit\imports` automatically.
- Stored profile tokens refresh on launch when automatic synchronization is enabled.
- A seven-day no-spend shield can hard-block paid Pearl recommendations.
- Failed sources retry independently and cannot stop unrelated feeds.
- Character deletion guidance is fail-closed.
- Class advice is play-feel based; live balance/meta questions are retrieved from current sources instead of hard-coded tiers.

## Safe rollback

The database migration is forward-moving. Keep the backup made before first V2.4.1 launch if you may return to an older version.
