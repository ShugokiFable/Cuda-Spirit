using System.Diagnostics;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.Core.Services.Knowledge;

/// <summary>
/// Source-aware knowledge synchronization. Fast-changing feeds run frequently, stable official
/// guides run daily, unchanged local exports are skipped by content state, and every source keeps
/// independent health/freshness metadata.
/// </summary>
public sealed class KnowledgeSyncService
{
    private readonly AppDatabase _db;
    private readonly SettingsService _settings;
    private readonly OfficialPatchNotesSource _official;
    private readonly OfficialGuideSource _guides;
    private readonly OfficialNewsSource _news;
    private readonly OfficialClassSource _classes;
    private readonly ArshaLiveSource _arsha;
    private readonly BdoAlertsSource _alerts;
    private readonly LocalCatalogImporter _local;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public bool IsSyncing => _syncGate.CurrentCount == 0;

    public KnowledgeSyncService(
        AppDatabase db,
        SettingsService settings,
        OfficialPatchNotesSource official,
        OfficialGuideSource guides,
        OfficialNewsSource news,
        OfficialClassSource classes,
        ArshaLiveSource arsha,
        BdoAlertsSource alerts,
        LocalCatalogImporter local)
    {
        _db = db;
        _settings = settings;
        _official = official;
        _guides = guides;
        _news = news;
        _classes = classes;
        _arsha = arsha;
        _alerts = alerts;
        _local = local;
    }

    public Task<SyncReport> SyncAllAsync(bool includeLocalImport = true, CancellationToken ct = default) =>
        SyncInternalAsync(_ => true, includeLocalImport, ct);

    public Task<SyncReport> SyncDueAsync(bool includeLocalImport = true, CancellationToken ct = default)
    {
        var states = _db.GetSourceStates().ToDictionary(x => x.SourceId, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var marketMinutes = Math.Clamp(_settings.Current.AutoSyncMinutes, 5, 240);
        var intervals = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
        {
            ["official-patch-notes"] = TimeSpan.FromMinutes(Math.Max(60, marketMinutes)),
            ["official-guides"] = TimeSpan.FromHours(24),
            ["official-events"] = TimeSpan.FromMinutes(Math.Max(30, marketMinutes)),
            ["official-pearl-shop"] = TimeSpan.FromMinutes(Math.Max(60, marketMinutes)),
            ["official-classes"] = TimeSpan.FromHours(24),
            ["arsha-market"] = TimeSpan.FromMinutes(marketMinutes),
            ["bdo-alerts"] = TimeSpan.FromMinutes(marketMinutes),
            ["local-json"] = TimeSpan.FromMinutes(Math.Max(15, marketMinutes))
        };

        bool IsDue(string sourceId)
        {
            if (!states.TryGetValue(sourceId, out var state)) return true;

            // Healthy sources use their normal cadence. Degraded sources retry sooner, but never
            // faster than every 15 minutes, so a temporary outage does not disable a source for a
            // full day or create an aggressive request loop.
            if (state.Status.Equals("degraded", StringComparison.OrdinalIgnoreCase))
            {
                var lastAttempt = state.LastAttemptAt ?? state.LastSuccessAt;
                var retry = intervals[sourceId] < TimeSpan.FromMinutes(15)
                    ? intervals[sourceId]
                    : TimeSpan.FromMinutes(15);
                return lastAttempt is null || now - lastAttempt.Value >= retry;
            }

            var last = state.LastSuccessAt ?? state.LastAttemptAt;
            return last is null || now - last.Value >= intervals[sourceId];
        }

        return SyncInternalAsync(IsDue, includeLocalImport, ct);
    }

    private async Task<SyncReport> SyncInternalAsync(Func<string, bool> shouldRun, bool includeLocalImport, CancellationToken ct)
    {
        await _syncGate.WaitAsync(ct);
        var started = DateTimeOffset.UtcNow;
        try
        {
            var results = new List<SyncSourceResult>();

            if (shouldRun("official-patch-notes"))
                results.Add(await RunAsync("official-patch-notes", "Official patch notes", async token =>
                {
                    var records = await _official.FetchAsync(_settings.Current.Region, _settings.Current.PatchNoteDetailLimit, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} official update records.");
                }, ct));

            if (shouldRun("official-guides"))
                results.Add(await RunAsync("official-guides", "Official onboarding and item guides", async token =>
                {
                    var records = await _guides.FetchAsync(_settings.Current.Region, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} official guide pages for rewards, inventory, transfer, seasons, enhancement, purchases, and account recovery.");
                }, ct));

            if (shouldRun("official-events"))
                results.Add(await RunAsync("official-events", "Official active events", async token =>
                {
                    var records = await _news.FetchEventsAsync(_settings.Current.Region, _settings.Current.PatchNoteDetailLimit, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} official event records and deadlines.");
                }, ct));

            if (shouldRun("official-pearl-shop"))
                results.Add(await RunAsync("official-pearl-shop", "Official Pearl Shop notices", async token =>
                {
                    var records = await _news.FetchPearlShopAsync(_settings.Current.Region, _settings.Current.PatchNoteDetailLimit, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} current Pearl Shop notice records.");
                }, ct));

            if (shouldRun("official-classes"))
                results.Add(await RunAsync("official-classes", "Official class catalog", async token =>
                {
                    var records = await _classes.FetchAsync(_settings.Current.Region, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} official class catalog records.");
                }, ct));

            if (shouldRun("arsha-market"))
                results.Add(await RunAsync("arsha-market", "Arsha market hot list", async token =>
                {
                    var records = await _arsha.FetchHotAsync(_settings.Current.Region, _settings.Current.MarketHotSyncLimit, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} live market records.");
                }, ct));

            if (shouldRun("bdo-alerts") && !string.IsNullOrWhiteSpace(_settings.Current.BdoAlertsApiKey))
                results.Add(await RunAsync("bdo-alerts", "BDO Alerts live API", async token =>
                {
                    var records = await _alerts.FetchAsync(_settings.Current.BdoAlertsApiKey, _settings.Current.Region, token);
                    return (_db.UpsertKnowledgeBatch(records), $"Stored {records.Count} boss, reset, news, maintenance, coupon, and market payloads.");
                }, ct));

            if (shouldRun("local-json") && includeLocalImport && !string.IsNullOrWhiteSpace(_settings.Current.LocalDataDirectory) && Directory.Exists(_settings.Current.LocalDataDirectory))
                results.Add(await ImportLocalCoreAsync(_settings.Current.LocalDataDirectory, ct));

            if (results.Count > 0)
            {
                _db.PruneAiCache(TimeSpan.FromDays(14));
                _db.PruneExpiredKnowledge(TimeSpan.FromDays(7));
                _db.PruneMarketHistory(TimeSpan.FromDays(120));
                _db.Optimize();
            }

            return new SyncReport { StartedAt = started, FinishedAt = DateTimeOffset.UtcNow, Sources = results };
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task<SyncSourceResult> ImportLocalAsync(string directory, CancellationToken ct = default)
    {
        await _syncGate.WaitAsync(ct);
        try { return await ImportLocalCoreAsync(directory, ct); }
        finally { _syncGate.Release(); }
    }

    private Task<SyncSourceResult> ImportLocalCoreAsync(string directory, CancellationToken ct) =>
        RunAsync("local-json", "Local extracted game data", async token =>
        {
            var report = await _local.ImportDirectoryAsync(directory, token);
            var totals = _db.GetImportTotals();
            var count = totals.Records + totals.Nodes + totals.Edges;
            var warning = report.Warnings.Count == 0 ? "" : $" Warnings: {string.Join(" | ", report.Warnings.Take(3))}";
            return (count, $"Scanned {report.FilesScanned} files; imported {report.FilesImported}, skipped {report.FilesSkipped} unchanged; updated {report.KnowledgeRecords} records, {report.RouteNodes} nodes, {report.RouteEdges} edges. Indexed total: {totals.Records} records, {totals.Nodes} nodes, {totals.Edges} edges.{warning}");
        }, ct);

    private async Task<SyncSourceResult> RunAsync(
        string sourceId,
        string displayName,
        Func<CancellationToken, Task<(int Records, string Message)>> action,
        CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var now = DateTimeOffset.UtcNow;
        var previous = _db.GetSourceStates().FirstOrDefault(x => x.SourceId == sourceId);
        _db.UpsertSourceState(new DataSourceState
        {
            SourceId = sourceId,
            DisplayName = displayName,
            Status = "syncing",
            LastAttemptAt = now,
            LastSuccessAt = previous?.LastSuccessAt,
            LastRecordCount = previous?.LastRecordCount ?? 0,
            LastError = "",
            Cursor = previous?.Cursor ?? "",
            MetadataJson = previous?.MetadataJson ?? "{}"
        });

        try
        {
            var result = await action(ct);
            watch.Stop();
            _db.UpsertSourceState(new DataSourceState
            {
                SourceId = sourceId,
                DisplayName = displayName,
                Status = "healthy",
                LastAttemptAt = now,
                LastSuccessAt = DateTimeOffset.UtcNow,
                LastRecordCount = result.Records,
                LastError = "",
                Cursor = previous?.Cursor ?? "",
                MetadataJson = previous?.MetadataJson ?? "{}"
            });
            return new SyncSourceResult { SourceId = sourceId, DisplayName = displayName, Success = true, Records = result.Records, Message = result.Message, Duration = watch.Elapsed };
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            watch.Stop();
            _db.UpsertSourceState(new DataSourceState
            {
                SourceId = sourceId,
                DisplayName = displayName,
                Status = "degraded",
                LastAttemptAt = now,
                LastSuccessAt = previous?.LastSuccessAt,
                LastRecordCount = previous?.LastRecordCount ?? 0,
                LastError = ex.Message,
                Cursor = previous?.Cursor ?? "",
                MetadataJson = previous?.MetadataJson ?? "{}"
            });
            return new SyncSourceResult { SourceId = sourceId, DisplayName = displayName, Success = false, Records = 0, Message = ex.Message, Duration = watch.Elapsed };
        }
    }
}
