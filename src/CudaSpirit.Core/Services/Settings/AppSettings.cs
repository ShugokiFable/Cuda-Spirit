using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Settings;

/// <summary>
/// User configuration. Persisted as JSON under %APPDATA%\CudaSpirit\settings.json.
/// The API key never leaves the machine except in the Authorization header to OpenRouter.
/// </summary>
public sealed class AppSettings
{
    public string OpenRouterApiKey { get; set; } = "";

    /// <summary>Optional free key for the BDO Alerts live boss/reset/market API.</summary>
    public string BdoAlertsApiKey { get; set; } = "";

    /// <summary>Directory containing read-only JSON exports such as items.json, recipes.json, nodes.json and routes.json.</summary>
    public string LocalDataDirectory { get; set; } = "";

    /// <summary>Refresh live knowledge sources when the application starts.</summary>
    public bool AutoSyncOnLaunch { get; set; } = true;

    /// <summary>Minimum interval between automatic sync attempts.</summary>
    public int AutoSyncMinutes { get; set; } = 15;

    public int KnowledgeMaxRecords { get; set; } = 8;
    public int KnowledgeMaxCharacters { get; set; } = 16_000;
    public int PatchNoteDetailLimit { get; set; } = 8;
    public int MarketHotSyncLimit { get; set; } = 100;

    /// <summary>Use the bundled WebView2 engine when official pages return an anti-bot challenge.</summary>
    public bool UseRenderedOfficialFallback { get; set; } = true;

    /// <summary>Watch configured local JSON exports and import shortly after a file changes.</summary>
    public bool WatchLocalDataDirectory { get; set; } = true;

    /// <summary>Recommended cooling-off period for non-expiring Pearl purchases.</summary>
    public int PearlPurchaseCooldownHours { get; set; } = 24;

    /// <summary>Optional hard no-spend shield. Paid Pearl offers are blocked while this UTC deadline is active.</summary>
    public DateTimeOffset? PearlSpendingFreezeUntilUtc { get; set; }

    // Personalization and guided-mode profile.
    public string ThemeId { get; set; } = "black-spirit";
    public string Density { get; set; } = "comfortable";
    public double FontScale { get; set; } = 1.0;
    public bool ReducedMotion { get; set; }
    public bool CompactNavigation { get; set; }
    public bool ShowBeginnerHints { get; set; } = true;
    public string StartupPage { get; set; } = "navigator";
    public AdventurerStage AdventurerStage { get; set; } = AdventurerStage.BrandNew;
    public PlayFocus PlayFocus { get; set; } = PlayFocus.Guided;
    public SpendingStyle SpendingStyle { get; set; } = SpendingStyle.ValueBuyer;
    public string MainClass { get; set; } = "";
    public string CurrentGoal { get; set; } = "Get oriented and avoid expensive mistakes";
    public int WeeklyPlayHours { get; set; } = 10;
    public int MonthlyPearlBudget { get; set; }
    public bool HasMagnusStorage { get; set; }
    public bool HasStorageMaid { get; set; }
    public bool HasTransactionMaid { get; set; }
    public bool HasTent { get; set; }

    /// <summary>Model routing. Each AI task kind maps to a model id the user can override.</summary>
    public ModelRouting Models { get; set; } = new();

    public Region Region { get; set; } = Region.NA;

    /// <summary>Family name shown on the dashboard (from the last profile sync).</summary>
    public string FamilyName { get; set; } = "";

    /// <summary>
    /// Stored adventurer-profile token (the profileTarget). Set once by pasting your profile link;
    /// the app re-fetches gear score / level / CP from the official profile on each launch.
    /// </summary>
    public string ProfileToken { get; set; } = "";

    /// <summary>Marketplace value tax after Value Pack + Family Fame etc. Default 0.845 (with VP + rewards).</summary>
    public double MarketTaxRate { get; set; } = 0.845;

    /// <summary>Cache TTL for AI answers keyed by prompt hash.</summary>
    public int AiCacheMinutes { get; set; } = 120;

    /// <summary>Overlay opacity 0.1..1.0.</summary>
    public double OverlayOpacity { get; set; } = 0.92;

    public bool OverlayClickThrough { get; set; } = true;

    /// <summary>When true the overlay pins to its saved position and ignores game-window tracking.</summary>
    public bool OverlayLocked { get; set; }

    /// <summary>Saved overlay X in screen pixels (absolute). -1 = not yet saved.</summary>
    public double OverlayX { get; set; } = -1;

    /// <summary>Saved overlay Y in screen pixels (absolute). -1 = not yet saved.</summary>
    public double OverlayY { get; set; } = -1;

    /// <summary>Pixel offset from the left edge of the game window when tracking (not locked).</summary>
    public int OverlayOffsetX { get; set; } = 24;

    /// <summary>Pixel offset from the top of the game window when tracking (not locked).</summary>
    public int OverlayTrackingOffsetY { get; set; } = 120;

    /// <summary>Saved Next-Step Tasks panel position (absolute screen pixels). -1 = not yet placed.</summary>
    public double TasksX { get; set; } = -1;
    public double TasksY { get; set; } = -1;

    /// <summary>Window title substring used to track the game window for overlay positioning.</summary>
    public string GameWindowTitle { get; set; } = "Black Desert";
}

/// <summary>Model ids per routing bucket. Defaults follow the requested OpenRouter catalog.</summary>
public sealed class ModelRouting
{
    /// <summary>Cheap/fast - data normalization, parsing anomalies, quick standardizations.</summary>
    public string Background { get; set; } = "openrouter/auto";

    /// <summary>Free alternative for background tasks.</summary>
    public string BackgroundFree { get; set; } = "openrouter/free";

    /// <summary>Strong reasoning - chat, combo optimization, market/enhancement routes.</summary>
    public string Reasoning { get; set; } = "openrouter/auto";

    /// <summary>Secondary reasoning option.</summary>
    public string ReasoningAlt { get; set; } = "openrouter/free";

    /// <summary>Vision - manual "read my screen" button and OCR fallback.</summary>
    public string Vision { get; set; } = "openrouter/auto";

    /// <summary>Secondary vision option.</summary>
    public string VisionAlt { get; set; } = "openrouter/free";

    /// <summary>Prefer the free background model when a request is flagged low-value.</summary>
    public bool PreferFreeForBackground { get; set; }

    public string Resolve(AiTaskKind kind) => kind switch
    {
        AiTaskKind.Background => PreferFreeForBackground ? BackgroundFree : Background,
        AiTaskKind.Reasoning => Reasoning,
        AiTaskKind.Vision => Vision,
        _ => Reasoning
    };

    public IReadOnlyList<string> Fallbacks(AiTaskKind kind)
    {
        var primary = Resolve(kind);
        var candidates = kind switch
        {
            AiTaskKind.Background => new[] { BackgroundFree, ReasoningAlt },
            AiTaskKind.Reasoning => new[] { ReasoningAlt, Background },
            AiTaskKind.Vision => new[] { VisionAlt, ReasoningAlt },
            _ => Array.Empty<string>()
        };
        return candidates
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals(primary, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }
}
