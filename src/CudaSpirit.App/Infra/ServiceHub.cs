using System.Net.Http;
using System.IO;
using System.Windows;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Ai;
using CudaSpirit.Core.Services.Bosses;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Enhancement;
using CudaSpirit.Core.Services.Grind;
using CudaSpirit.Core.Services.Guidance;
using CudaSpirit.Core.Services.Live;
using CudaSpirit.Core.Services.Knowledge;
using CudaSpirit.Core.Services.Routing;
using CudaSpirit.Core.Services.Market;
using CudaSpirit.Core.Services.OpenRouter;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.App.Infra;

/// <summary>
/// Tiny composition root. Constructs every service once and exposes them to the views. Kept simple
/// (no DI container) so the wiring is obvious - this is the single place the whole graph is built.
/// </summary>
public sealed class ServiceHub : IDisposable
{
    public static ServiceHub Instance { get; } = new();

    public HttpClient Http { get; }
    public SettingsService Settings { get; }
    public AppDatabase Db { get; }
    public OpenRouterClient OpenRouter { get; }
    public ManualLiveStateProvider Live { get; }
    public KnowledgeRetriever Knowledge { get; }
    public BuiltInKnowledgeSeeder BuiltInKnowledge { get; }
    public OfficialPatchNotesSource OfficialPatchNotes { get; }
    public OfficialGuideSource OfficialGuides { get; }
    public OfficialNewsSource OfficialNews { get; }
    public OfficialClassSource OfficialClasses { get; }
    public ArshaLiveSource ArshaLive { get; }
    public BdoAlertsSource BdoAlerts { get; }
    public LocalCatalogImporter LocalImporter { get; }
    public KnowledgeSyncService KnowledgeSync { get; }
    public FarmRoutePlanner RoutePlanner { get; }
    public ContextAggregator Context { get; }
    public AdvisorService Advisor { get; }
    public AdvisorConversation Conversation { get; }
    public BdoMarketClient Market { get; }
    public BdoProfileClient Profile { get; }
    public GarmothClient Garmoth { get; }
    public EnhancementSimulator Enhancement { get; }
    public BossScheduleService Bosses { get; }
    public ProgressionHelper Progression { get; }
    public GrindTracker Grind { get; }
    public ItemSafetyAdvisor ItemSafety { get; }
    public TransferAdvisor Transfer { get; }
    public PearlShopAdvisor PearlShop { get; }
    public ProgressionPlanner Navigator { get; }
    public ReturnerRecoveryPlanner ReturnerRecovery { get; }
    public CharacterRetirementPlanner CharacterRetirement { get; }
    public ClassAdvisor ClassAdvisor { get; }
    public AppearanceService Appearance { get; }

    /// <summary>Tracks the BDO game window position/size for overlay positioning.</summary>
    public GameWindowTracker Tracker { get; }

    private readonly CancellationTokenSource _autoSyncCts = new();
    private Task? _autoSyncTask;
    private FileSystemWatcher? _localWatcher;
    private System.Threading.Timer? _localImportTimer;
    private readonly object _watcherGate = new();
    private DateTimeOffset _nextProfileSyncAttemptUtc = DateTimeOffset.MinValue;
    private int _started;

    private ServiceHub()
    {
        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        Settings = new SettingsService();
        Db = new AppDatabase();

        BuiltInKnowledge = new BuiltInKnowledgeSeeder(Db);
        BuiltInKnowledge.Seed();
        OpenRouter = new OpenRouterClient(Http, () => Settings.Current.OpenRouterApiKey);
        Live = new ManualLiveStateProvider(Db);
        Knowledge = new KnowledgeRetriever(Db);
        OfficialPatchNotes = new OfficialPatchNotesSource(Http, FetchOfficialPageRenderedAsync);
        OfficialGuides = new OfficialGuideSource(Http, FetchOfficialPageRenderedAsync);
        OfficialNews = new OfficialNewsSource(Http, FetchOfficialPageRenderedAsync);
        OfficialClasses = new OfficialClassSource(Http, FetchOfficialPageRenderedAsync);
        ArshaLive = new ArshaLiveSource(Http, Db);
        BdoAlerts = new BdoAlertsSource(Http, Db);
        LocalImporter = new LocalCatalogImporter(Db);
        KnowledgeSync = new KnowledgeSyncService(Db, Settings, OfficialPatchNotes, OfficialGuides, OfficialNews, OfficialClasses, ArshaLive, BdoAlerts, LocalImporter);
        RoutePlanner = new FarmRoutePlanner(Db);
        Context = new ContextAggregator(Live, Settings, Knowledge, Db);
        Advisor = new AdvisorService(OpenRouter, Context, Settings, Db);
        Conversation = new AdvisorConversation(Advisor, Settings);
        Market = new BdoMarketClient(Http, Db);
        Profile = new BdoProfileClient(Http);
        Garmoth = new GarmothClient(Http);
        Enhancement = new EnhancementSimulator();
        Bosses = new BossScheduleService();
        Progression = new ProgressionHelper();
        Grind = new GrindTracker(Db, Settings.Current.MarketTaxRate);
        ItemSafety = new ItemSafetyAdvisor(Db, Knowledge, Settings);
        Transfer = new TransferAdvisor();
        PearlShop = new PearlShopAdvisor(Db, Settings);
        Navigator = new ProgressionPlanner(Db, Settings);
        ReturnerRecovery = new ReturnerRecoveryPlanner(Db, Settings);
        CharacterRetirement = new CharacterRetirementPlanner();
        ClassAdvisor = new ClassAdvisor();
        Appearance = new AppearanceService(Settings);
        Tracker = new GameWindowTracker(Settings.Current.GameWindowTitle);
    }

    /// <summary>
    /// Pull the official adventurer profile for <paramref name="urlOrToken"/> (a profile link or raw
    /// token), merge it into the live player state, and remember the token for auto-refresh on launch.
    /// Returns the parsed profile. Throws with a clear message if it can't be read.
    /// </summary>
    public async Task<ProfileInfo> SyncFromProfileAsync(string urlOrToken, CancellationToken ct = default)
    {
        var token = BdoProfileClient.ExtractToken(urlOrToken)
            ?? throw new InvalidOperationException(
                "Couldn't find a profile token. Paste your full profile link (…/Adventure/Profile?profileTarget=…).");

        // BDO's site is behind Imperva Incapsula now, which 403s a plain HttpClient. Load it in the
        // bundled WebView2 (real Chromium) so the bot-check resolves, then parse the rendered HTML.
        var url = BdoProfileClient.BuildProfileUrl(token, Settings.Current.Region);
        var html = await WebViewFetcher.FetchHtmlAsync(
            url,
            ready: h => h.Contains("class=\"nick\"", StringComparison.OrdinalIgnoreCase));

        var info = BdoProfileClient.TryParse(html);
        if (string.IsNullOrEmpty(info.FamilyName))
            throw new InvalidOperationException(
                "Couldn't read the profile. Make sure your Adventurer profile is set to Public on the BDO website " +
                "and the link/token is correct. (The site uses bot protection, so the first fetch can take a few seconds.)");
        info.ProfileToken = token;
        if (info.Region == Region.NA && Settings.Current.Region != Region.NA)
            info.Region = Settings.Current.Region;

        // Merge into the current state (preserve manual AP/DP/silver the user may have entered).
        var s = Live.Current;
        s.FamilyName = info.FamilyName;
        s.Guild = info.Guild;
        s.ClassName = info.MainClass;
        s.CharacterName = info.MainCharacterName;
        s.Level = info.MainLevel;
        s.Region = info.Region;
        s.ReportedGearScore = info.GearScore > 0 ? info.GearScore : null;
        if (info.ContributionPoints > 0) s.ContributionPoints = info.ContributionPoints;
        if (info.Energy > 0) { s.EnergyCurrent = info.Energy; s.EnergyMax = info.Energy; }
        s.Source = "profile";
        Live.Publish(s);

        // Persist token + region for next-launch auto-refresh.
        Settings.Update(cfg =>
        {
            cfg.ProfileToken = info.ProfileToken;
            cfg.FamilyName = info.FamilyName;
            cfg.Region = info.Region;
        });
        return info;
    }

    /// <summary>
    /// Try to fetch gear data from Garmoth using the stored family name, and merge AP/DP/gear
    /// into the live state. Returns the Garmoth profile or null if not found.
    /// </summary>
    public async Task<GarmothProfile?> SyncFromGarmothAsync(CancellationToken ct = default)
    {
        var familyName = Settings.Current.FamilyName;
        if (string.IsNullOrWhiteSpace(familyName)) return null;

        var garmoth = await Garmoth.GetProfileAsync(familyName, Settings.Current.Region, ct);
        if (garmoth is null) return null;

        var s = Live.Current;

        // Merge AP/DP if Garmoth has them and the user hasn't manually entered better values.
        if (garmoth.AP > 0 && s.Ap == 0) s.Ap = garmoth.AP;
        if (garmoth.AwakeningAP > 0 && s.Awakening == 0) s.Awakening = garmoth.AwakeningAP;
        if (garmoth.DP > 0 && s.Dp == 0) s.Dp = garmoth.DP;

        // Merge gear items (add any that aren't already present).
        var existingSlots = s.Gear.Select(g => g.Slot).ToHashSet();
        foreach (var item in garmoth.Gear)
        {
            if (!existingSlots.Contains(item.Slot))
            {
                s.Gear.Add(item);
                existingSlots.Add(item.Slot);
            }
        }

        s.Source = "garmoth";
        Live.Publish(s);
        return garmoth;
    }

    /// <summary>Start background services. Safe to call more than once.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        Tracker.Start();
        RefreshLocalDataWatcher();
        _autoSyncTask = RunAutoSyncLoopAsync(_autoSyncCts.Token);
    }

    private async Task RunAutoSyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Settings.Current.AutoSyncOnLaunch)
                {
                    try
                    {
                        await SyncStoredProfileIfDueAsync(ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Profile enrichment is optional and must never block official guides,
                        // events, market, alerts, or local data from refreshing.
                    }

                    await KnowledgeSync.SyncDueAsync(includeLocalImport: true, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Individual source failures are persisted by KnowledgeSyncService.
                // The scheduler remains alive and retries at the next interval.
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncStoredProfileIfDueAsync(CancellationToken ct)
    {
        var token = Settings.Current.ProfileToken;
        if (string.IsNullOrWhiteSpace(token) || DateTimeOffset.UtcNow < _nextProfileSyncAttemptUtc) return;

        // Avoid hammering the official profile page after either success or a transient failure.
        _nextProfileSyncAttemptUtc = DateTimeOffset.UtcNow.AddHours(6);
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("The desktop dispatcher is not available.");
        var operation = dispatcher.InvokeAsync(() => SyncFromProfileAsync(token, ct));
        await operation.Task.Unwrap().WaitAsync(ct);

        try
        {
            await SyncFromGarmothAsync(ct);
        }
        catch when (!ct.IsCancellationRequested)
        {
            // Garmoth is an optional enrichment source. The official profile result remains valid.
        }
    }

    private async Task<string> FetchOfficialPageRenderedAsync(string url, CancellationToken ct)
    {
        if (!Settings.Current.UseRenderedOfficialFallback)
            throw new InvalidOperationException("Rendered official-page fallback is disabled in settings.");

        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("The desktop dispatcher is not available.");
        var operation = dispatcher.InvokeAsync(() => WebViewFetcher.FetchHtmlAsync(
            url,
            html => html.Length > 1_000 &&
                    !html.Contains("Incapsula incident", StringComparison.OrdinalIgnoreCase) &&
                    !html.Contains("Request unsuccessful", StringComparison.OrdinalIgnoreCase),
            timeoutMs: 30_000));
        return await operation.Task.Unwrap().WaitAsync(ct);
    }

    public void RefreshLocalDataWatcher()
    {
        lock (_watcherGate)
        {
            _localImportTimer?.Dispose();
            _localImportTimer = null;
            if (_localWatcher is not null)
            {
                _localWatcher.EnableRaisingEvents = false;
                _localWatcher.Dispose();
                _localWatcher = null;
            }

            var path = Settings.Current.LocalDataDirectory;
            if (!Settings.Current.WatchLocalDataDirectory || string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            _localWatcher = new FileSystemWatcher(path, "*.json")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _localWatcher.Changed += OnLocalDataChanged;
            _localWatcher.Created += OnLocalDataChanged;
            _localWatcher.Deleted += OnLocalDataChanged;
            _localWatcher.Renamed += OnLocalDataChanged;
        }
    }

    private void OnLocalDataChanged(object sender, FileSystemEventArgs e)
    {
        lock (_watcherGate)
        {
            _localImportTimer?.Dispose();
            _localImportTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    var path = Settings.Current.LocalDataDirectory;
                    if (Directory.Exists(path)) await KnowledgeSync.ImportLocalAsync(path, _autoSyncCts.Token);
                }
                catch (OperationCanceledException) { }
                catch { }
            }, null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        _autoSyncCts.Cancel();
        try { _autoSyncTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        lock (_watcherGate)
        {
            _localImportTimer?.Dispose();
            _localWatcher?.Dispose();
        }
        _autoSyncCts.Dispose();
        Tracker.Dispose();
        Db.Dispose();
        Http.Dispose();
    }

}
