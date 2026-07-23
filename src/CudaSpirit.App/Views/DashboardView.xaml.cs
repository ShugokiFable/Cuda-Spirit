using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class DashboardView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private bool _autoSyncTried;


    public DashboardView()
    {
        InitializeComponent();

        Refresh();

        // On first open, if a profile token was saved previously, refresh live stats from it.
        Loaded += async (_, _) =>
        {
            if (_autoSyncTried) return;
            _autoSyncTried = true;
            var token = _hub.Settings.Current.ProfileToken;
            if (!string.IsNullOrWhiteSpace(token))
            {
                await DoSyncAsync(token, silent: true);
                // After profile sync, try Garmoth too.
                await DoGarmothSync(silent: true);
            }
        };
    }

    public void Refresh()
    {
        var s = _hub.Live.Current;
        ClassBox.Text = s.ClassName;
        var importedZones = _hub.Db.GetRouteNodes()
            .Where(x => x.ExpectedSilverPerHour > 0)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        ZoneBox.ItemsSource = importedZones;
        ZoneBox.Text = s.GrindZone ?? "";
        ApBox.Text = s.Ap.ToString();
        AwkBox.Text = s.Awakening.ToString();
        DpBox.Text = s.Dp.ToString();
        SilverBox.Text = s.Silver.ToString();
        RenderSummary(s);
        RenderProfileSummary(s);
        RenderBosses();
        RenderAutoSummary(s);
        RenderGrindRecommendations(s);
    }

    // ── Profile sync ─────────────────────────────────────────────────────────

    private async void OnSyncProfile(object sender, RoutedEventArgs e)
    {
        var input = ProfileBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
            input = _hub.Settings.Current.ProfileToken;
        if (string.IsNullOrWhiteSpace(input))
        {
            SyncStatus.Text = "paste your profile link first";
            return;
        }
        await DoSyncAsync(input, silent: false);
        // After profile sync, try Garmoth too.
        await DoGarmothSync(silent: true);
    }

    private async Task DoSyncAsync(string urlOrToken, bool silent)
    {
        SyncButton.IsEnabled = false;
        SyncStatus.Text = "syncing…";
        try
        {
            var info = await _hub.SyncFromProfileAsync(urlOrToken);
            SyncStatus.Text = $"synced ✓ {DateTime.Now:HH:mm}";
            ProfileBox.Clear();
            Refresh();
        }
        catch (Exception ex)
        {
            SyncStatus.Text = "sync failed";
            if (!silent)
                ProfileSummary.Text = "Couldn't sync: " + ex.Message +
                    "  (Make sure your profile is public on the BDO site and you pasted the full profile link.)";
        }
        finally
        {
            SyncButton.IsEnabled = true;
        }
    }

    // ── Garmoth sync ─────────────────────────────────────────────────────────

    private async void OnSyncGarmoth(object sender, RoutedEventArgs e)
    {
        await DoGarmothSync(silent: false);
    }

    private async Task DoGarmothSync(bool silent)
    {
        GarmothButton.IsEnabled = false;
        GarmothStatus.Text = "fetching from Garmoth…";
        try
        {
            var garmoth = await _hub.SyncFromGarmothAsync();
            if (garmoth is not null)
            {
                var gearCount = garmoth.Gear.Count;
                var parts = new List<string>();
                if (garmoth.AP > 0) parts.Add($"AP {garmoth.AP}");
                if (garmoth.AwakeningAP > 0) parts.Add($"Awk {garmoth.AwakeningAP}");
                if (garmoth.DP > 0) parts.Add($"DP {garmoth.DP}");
                if (gearCount > 0) parts.Add($"{gearCount} gear items");
                GarmothStatus.Text = parts.Count > 0
                    ? $"Garmoth ✓ {string.Join(", ", parts)}"
                    : "Garmoth: profile found but no gear data available";
            }
            else
            {
                GarmothStatus.Text = silent
                    ? ""
                    : "Garmoth has no public lookup API - for gear/AP-DP use 📷 Scan Gear (vision) or enter them manually.";
            }
            Refresh();
        }
        catch (Exception ex)
        {
            GarmothStatus.Text = silent ? "" : $"Garmoth error: {ex.Message}";
        }
        finally
        {
            GarmothButton.IsEnabled = true;
        }
    }

    // ── Vision scan ──────────────────────────────────────────────────────────

    private async void OnScanGear(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_hub.Settings.Current.OpenRouterApiKey))
        {
            GarmothStatus.Text = "Add an OpenRouter API key in Settings first to use vision.";
            return;
        }

        ScanButton.IsEnabled = false;
        GarmothStatus.Text = "📷 Capturing screen for gear analysis…";
        try
        {
            var dataUrl = ScreenCapture.CaptureVirtualScreenDataUrl();
            var prompt = "Read this Black Desert Online gear/inventory screen. " +
                "For each equipped item, list: slot (main hand, off hand, awakening, helmet, armor, gloves, boots, necklace, ring, earring, belt), " +
                "item name, enhancement level (e.g. TET, PEN, TRI), and AP/DP if visible. " +
                "Also read the total AP, Awakening AP, and DP numbers shown on screen. " +
                "Format as a structured list. Be concise.";

            var answer = await _hub.Advisor.AskWithImagesAsync(prompt, new[] { dataUrl });

            // Show the vision result in the auto-summary.
            GarmothStatus.Text = "📷 Vision scan complete - see AI Advisor for details";
            AutoSummary.Text = "📷 Vision scan result:\n" + answer;
        }
        catch (Exception ex)
        {
            GarmothStatus.Text = $"📷 Scan error: {ex.Message}";
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    // ── Manual update ────────────────────────────────────────────────────────

    private void OnUpdate(object sender, RoutedEventArgs e)
    {
        var s = _hub.Live.Current;
        s.ClassName = ClassBox.Text.Trim();
        s.GrindZone = string.IsNullOrWhiteSpace(ZoneBox.Text) ? null : ZoneBox.Text.Trim();
        s.Ap = ParseInt(ApBox.Text, s.Ap);
        s.Awakening = ParseInt(AwkBox.Text, s.Awakening);
        s.Dp = ParseInt(DpBox.Text, s.Dp);
        s.Silver = ParseLong(SilverBox.Text, s.Silver);
        s.Source = "manual";
        _hub.Live.Publish(s);
        RenderSummary(s);
        RenderAutoSummary(s);
        RenderGrindRecommendations(s);
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private void RenderProfileSummary(PlayerState s)
    {
        if (string.IsNullOrEmpty(s.FamilyName))
        {
            ProfileSummary.Text = "";
            return;
        }
        var guild = string.IsNullOrEmpty(s.Guild) ? "" : $" · {s.Guild}";
        var gs = s.ReportedGearScore is { } r ? $" · GS {r}" : "";
        ProfileSummary.Text = $"👤 {s.FamilyName} · {s.ClassName} Lv{s.Level} ({s.Region}){gs} · CP {s.ContributionPoints}{guild}";
    }

    private void RenderSummary(PlayerState s)
    {
        GearScoreText.Text = s.GearScore.ToString("N0");

        int combinedAp = Math.Max(s.Ap, s.Awakening);
        ApDpText.Text = combinedAp > 0 || s.Dp > 0
            ? $"AP {combinedAp}  ·  DP {s.Dp}"
            : (s.ReportedGearScore is { } r ? $"Max Gear Score {r} (from profile)" : "AP / DP not set - sync or enter manually");
        SilverText.Text = $"{s.Silver:N0} silver";

        int toNext = _hub.Progression.ApToNextBracket(combinedAp);
        BracketText.Text = combinedAp > 0 && toNext > 0
            ? $"{toNext} AP to the next bracket ({_hub.Progression.NextApBracket(combinedAp)} AP)."
            : "Enter your AP/DP split for exact bracket math.";
    }

    private void RenderAutoSummary(PlayerState s)
    {
        var lines = new List<string>();

        // Show what's been auto-fetched.
        if (!string.IsNullOrEmpty(s.FamilyName))
            lines.Add($"✅ Family: {s.FamilyName} ({s.Region})");
        if (!string.IsNullOrEmpty(s.ClassName))
            lines.Add($"✅ Class: {s.ClassName} Lv{s.Level}");
        if (s.ReportedGearScore is { } gs && gs > 0)
            lines.Add($"✅ Gear Score: {gs} (from official profile)");
        if (s.ContributionPoints > 0)
            lines.Add($"✅ Contribution Points: {s.ContributionPoints}");
        if (s.EnergyMax > 0)
            lines.Add($"✅ Energy: {s.EnergyMax}");
        if (!string.IsNullOrEmpty(s.Guild))
            lines.Add($"✅ Guild: {s.Guild}");

        // Show what still needs input.
        if (s.Ap == 0 && s.Awakening == 0)
            lines.Add("⬜ AP/Awakening: not set (enter manually or use Garmoth/Scan)");
        if (s.Dp == 0)
            lines.Add("⬜ DP: not set");
        if (s.Silver == 0)
            lines.Add("⬜ Silver: enter from in-game");
        if (s.Gear.Count == 0)
            lines.Add("⬜ Gear items: none loaded (use Fetch Garmoth or 📷 Scan Gear)");

        if (s.Gear.Count > 0)
        {
            lines.Add($"✅ Gear: {s.Gear.Count} items loaded");
            foreach (var g in s.Gear.Where(g => g.Equipped).Take(8))
                lines.Add($"   • {g.Grade} {g.Name} ({g.Slot}) C{g.Caphras}");
        }

        if (lines.Count == 0)
            lines.Add("Nothing fetched yet. Paste your BDO profile link above and hit Sync.");

        AutoSummary.Text = string.Join("\n", lines);
    }

    private void RenderGrindRecommendations(PlayerState s)
    {
        var ap = Math.Max(s.Ap, s.Awakening);
        if (ap <= 0 || s.Dp <= 0)
        {
            GrindRecs.ItemsSource = new[] { "Enter your AP and DP to rank imported farm zones." };
            return;
        }

        var startKey = _hub.Db.GetRouteNodes()
            .FirstOrDefault(x => x.Name.Equals(s.GrindZone, StringComparison.OrdinalIgnoreCase))?.Key;
        var recs = _hub.RoutePlanner.RecommendFarms(ap, s.Dp, startKey, RouteObjective.Balanced, 0.5, 4)
            .Select(x => $"{(x.Fit == "Ready" ? "✅" : "⚠️")} {x.Zone.Name} · {x.Fit} · {x.Zone.ExpectedSilverPerHour:N0}/hr · updated {x.Zone.UpdatedAt.LocalDateTime:g}")
            .ToList();

        if (recs.Count == 0)
            recs.Add("No current farm records are indexed. Open Live Data Center and import grind-zone JSON.");

        GrindRecs.ItemsSource = recs;
    }

    private void RenderBosses()
    {
        BossMini.ItemsSource = _hub.Bosses.GetUpcoming(5)
            .Select(b => new
            {
                b.Name,
                Countdown = b.IsLive ? "LIVE now" : $"in {Format(b.TimeUntil)}  ·  {b.NextSpawn:ddd HH:mm}"
            })
            .ToList();
    }

    private static string Format(TimeSpan t) =>
        t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h" :
        t.TotalHours >= 1 ? $"{t.Hours}h {t.Minutes}m" : $"{t.Minutes}m";

    private static int ParseInt(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    private static long ParseLong(string s, long fallback) =>
        long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}