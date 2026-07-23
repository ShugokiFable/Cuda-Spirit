using System.Text.Json;

namespace CudaSpirit.Core.Services.Settings;

/// <summary>Loads/saves <see cref="AppSettings"/> to %APPDATA%\CudaSpirit\settings.json.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly object _gate = new();

    public AppSettings Current { get; private set; } = new();

    public SettingsService(string? path = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CudaSpirit");
        Directory.CreateDirectory(dir);
        _path = path ?? Path.Combine(dir, "settings.json");
        Load();
        Normalize(Current);
        try { Directory.CreateDirectory(Current.LocalDataDirectory); } catch { }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (loaded is not null)
                {
                    Normalize(loaded);
                    Current = loaded;
                }
            }
        }
        catch
        {
            // Preserve a broken file for diagnosis, then fall back instead of crashing.
            try
            {
                if (File.Exists(_path))
                    File.Copy(_path, _path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", overwrite: false);
            }
            catch { }
            Current = new AppSettings();
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Models ??= new ModelRouting();
        settings.OpenRouterApiKey ??= "";
        settings.BdoAlertsApiKey ??= "";
        settings.LocalDataDirectory = string.IsNullOrWhiteSpace(settings.LocalDataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CudaSpirit", "imports")
            : settings.LocalDataDirectory.Trim();
        settings.FamilyName ??= "";
        settings.ProfileToken ??= "";
        settings.GameWindowTitle = string.IsNullOrWhiteSpace(settings.GameWindowTitle) ? "Black Desert" : settings.GameWindowTitle;
        settings.AutoSyncMinutes = Math.Clamp(settings.AutoSyncMinutes, 5, 1440);
        settings.KnowledgeMaxRecords = Math.Clamp(settings.KnowledgeMaxRecords, 1, 30);
        settings.KnowledgeMaxCharacters = Math.Clamp(settings.KnowledgeMaxCharacters, 2_000, 100_000);
        settings.PatchNoteDetailLimit = Math.Clamp(settings.PatchNoteDetailLimit, 1, 50);
        settings.MarketHotSyncLimit = Math.Clamp(settings.MarketHotSyncLimit, 10, 500);
        settings.PearlPurchaseCooldownHours = Math.Clamp(settings.PearlPurchaseCooldownHours, 0, 168);
        if (settings.PearlSpendingFreezeUntilUtc is { } freeze && freeze <= DateTimeOffset.UtcNow)
            settings.PearlSpendingFreezeUntilUtc = null;
        settings.ThemeId = Default(settings.ThemeId, "black-spirit");
        settings.Density = Default(settings.Density, "comfortable");
        settings.FontScale = Math.Clamp(settings.FontScale, 0.85, 1.35);
        settings.StartupPage = Default(settings.StartupPage, "navigator");
        settings.MainClass ??= "";
        settings.CurrentGoal = Default(settings.CurrentGoal, "Get oriented and avoid expensive mistakes");
        settings.WeeklyPlayHours = Math.Clamp(settings.WeeklyPlayHours, 1, 168);
        settings.MonthlyPearlBudget = Math.Clamp(settings.MonthlyPearlBudget, 0, 1_000_000);
        settings.MarketTaxRate = Math.Clamp(settings.MarketTaxRate, 0.5, 1.0);
        settings.OverlayOpacity = Math.Clamp(settings.OverlayOpacity, 0.1, 1.0);

        settings.Models.Background = Default(settings.Models.Background, "openrouter/auto");
        settings.Models.BackgroundFree = Default(settings.Models.BackgroundFree, "openrouter/free");
        settings.Models.Reasoning = Default(settings.Models.Reasoning, "openrouter/auto");
        settings.Models.ReasoningAlt = Default(settings.Models.ReasoningAlt, "openrouter/free");
        settings.Models.Vision = Default(settings.Models.Vision, "openrouter/auto");
        settings.Models.VisionAlt = Default(settings.Models.VisionAlt, "openrouter/free");
    }

    private static string Default(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public void Save()
    {
        lock (_gate)
        {
            var json = JsonSerializer.Serialize(Current, JsonOpts);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Copy(tmp, _path, overwrite: true);
            File.Delete(tmp);
        }
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Normalize(Current);
        Save();
    }
}
