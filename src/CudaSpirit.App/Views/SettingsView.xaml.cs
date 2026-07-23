using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class SettingsView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;

    public SettingsView()
    {
        InitializeComponent();
        RegionBox.ItemsSource = Enum.GetValues(typeof(Region));
        Refresh();
    }

    public void Refresh()
    {
        var s = _hub.Settings.Current;
        ApiKeyBox.Password = s.OpenRouterApiKey;
        BdoAlertsKeyBox.Password = s.BdoAlertsApiKey;
        LocalDataBox.Text = s.LocalDataDirectory;
        AutoSyncBox.IsChecked = s.AutoSyncOnLaunch;
        AutoSyncMinutesBox.Text = s.AutoSyncMinutes.ToString(CultureInfo.InvariantCulture);
        KnowledgeRecordsBox.Text = s.KnowledgeMaxRecords.ToString(CultureInfo.InvariantCulture);
        KnowledgeCharactersBox.Text = s.KnowledgeMaxCharacters.ToString(CultureInfo.InvariantCulture);
        PatchLimitBox.Text = s.PatchNoteDetailLimit.ToString(CultureInfo.InvariantCulture);
        MarketLimitBox.Text = s.MarketHotSyncLimit.ToString(CultureInfo.InvariantCulture);
        RenderedFallbackBox.IsChecked = s.UseRenderedOfficialFallback;
        WatchLocalDataBox.IsChecked = s.WatchLocalDataDirectory;
        PearlCooldownBox.Text = s.PearlPurchaseCooldownHours.ToString(CultureInfo.InvariantCulture);

        ReasoningBox.Text = s.Models.Reasoning;
        ReasoningAltBox.Text = s.Models.ReasoningAlt;
        BackgroundBox.Text = s.Models.Background;
        BackgroundFreeBox.Text = s.Models.BackgroundFree;
        VisionBox.Text = s.Models.Vision;
        VisionAltBox.Text = s.Models.VisionAlt;
        PreferFreeBox.IsChecked = s.Models.PreferFreeForBackground;

        RegionBox.SelectedItem = s.Region;
        TaxBox.Text = s.MarketTaxRate.ToString(CultureInfo.InvariantCulture);
        OpacityBox.Text = s.OverlayOpacity.ToString(CultureInfo.InvariantCulture);
        WindowTitleBox.Text = s.GameWindowTitle;
        OffsetYBox.Text = s.OverlayTrackingOffsetY.ToString(CultureInfo.InvariantCulture);
        ClickThroughBox.IsChecked = s.OverlayClickThrough;
    }

    private void OnBrowseData(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select BDO JSON export directory",
            Multiselect = false,
            InitialDirectory = Directory.Exists(LocalDataBox.Text)
                ? LocalDataBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog() == true) LocalDataBox.Text = dialog.FolderName;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _hub.Settings.Update(s =>
        {
            s.OpenRouterApiKey = ApiKeyBox.Password.Trim();
            s.BdoAlertsApiKey = BdoAlertsKeyBox.Password.Trim();
            s.LocalDataDirectory = LocalDataBox.Text.Trim();
            s.AutoSyncOnLaunch = AutoSyncBox.IsChecked == true;
            s.AutoSyncMinutes = ParseInt(AutoSyncMinutesBox.Text, s.AutoSyncMinutes, 5, 1440);
            s.KnowledgeMaxRecords = ParseInt(KnowledgeRecordsBox.Text, s.KnowledgeMaxRecords, 1, 30);
            s.KnowledgeMaxCharacters = ParseInt(KnowledgeCharactersBox.Text, s.KnowledgeMaxCharacters, 2_000, 100_000);
            s.PatchNoteDetailLimit = ParseInt(PatchLimitBox.Text, s.PatchNoteDetailLimit, 1, 50);
            s.MarketHotSyncLimit = ParseInt(MarketLimitBox.Text, s.MarketHotSyncLimit, 10, 500);
            s.UseRenderedOfficialFallback = RenderedFallbackBox.IsChecked == true;
            s.WatchLocalDataDirectory = WatchLocalDataBox.IsChecked == true;
            s.PearlPurchaseCooldownHours = ParseInt(PearlCooldownBox.Text, s.PearlPurchaseCooldownHours, 0, 168);

            s.Models.Reasoning = NonEmpty(ReasoningBox.Text, "openrouter/auto");
            s.Models.ReasoningAlt = NonEmpty(ReasoningAltBox.Text, "openrouter/free");
            s.Models.Background = NonEmpty(BackgroundBox.Text, "openrouter/auto");
            s.Models.BackgroundFree = NonEmpty(BackgroundFreeBox.Text, "openrouter/free");
            s.Models.Vision = NonEmpty(VisionBox.Text, "openrouter/auto");
            s.Models.VisionAlt = NonEmpty(VisionAltBox.Text, "openrouter/free");
            s.Models.PreferFreeForBackground = PreferFreeBox.IsChecked == true;

            if (RegionBox.SelectedItem is Region r) s.Region = r;
            if (double.TryParse(TaxBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var tax))
                s.MarketTaxRate = Math.Clamp(tax, 0.5, 1.0);
            if (double.TryParse(OpacityBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var op))
                s.OverlayOpacity = Math.Clamp(op, 0.1, 1.0);
            s.GameWindowTitle = NonEmpty(WindowTitleBox.Text, "Black Desert");
            s.OverlayTrackingOffsetY = ParseInt(OffsetYBox.Text, s.OverlayTrackingOffsetY, 0, 2000);
            s.OverlayClickThrough = ClickThroughBox.IsChecked == true;
        });
        _hub.RefreshLocalDataWatcher();
        SavedLabel.Text = $"Saved ✓  {DateTime.Now:HH:mm:ss}";
    }

    private static int ParseInt(string value, int fallback, int min, int max) =>
        int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            ? Math.Clamp(n, min, max)
            : fallback;

    private static string NonEmpty(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
