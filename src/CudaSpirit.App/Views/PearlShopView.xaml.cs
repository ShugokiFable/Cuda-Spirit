using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class PearlShopView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private List<OfferRow> _offers = new();
    private sealed record OfferRow(string Title, string Meta, string Summary, string Url);

    public PearlShopView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var region = _hub.Settings.Current.Region.ToString().ToLowerInvariant();
        _offers = _hub.Db.GetLatestKnowledge(60, KnowledgeKinds.PearlShop, region)
            .Where(x => x.SourceId == "official-pearl-shop" || x.Title.Contains("Pearl Shop", StringComparison.OrdinalIgnoreCase))
            .Select(x => new OfferRow(x.Title,
                $"{x.SourceId} · {(x.EffectiveAt?.LocalDateTime.ToString("g") ?? x.RetrievedAt.LocalDateTime.ToString("g"))}",
                x.Summary, x.Url)).ToList();
        OffersList.ItemsSource = _offers;
        var shield = _hub.Settings.Current.PearlSpendingFreezeUntilUtc;
        SpendShieldText.Text = shield is { } until && until > DateTimeOffset.UtcNow
            ? $"No-spend shield active until {until.ToLocalTime():f}. Paid offers are automatically blocked."
            : "No-spend shield inactive. Returning players can activate one in Returner & Reroll Recovery.";
        StatusText.Text = $"{_offers.Count} Pearl Shop notice records available. Manual scoring is saved to SQLite for later comparison.";
    }

    private void OnEvaluate(object sender, RoutedEventArgs e)
    {
        var result = _hub.PearlShop.Evaluate(new PearlOfferInput
        {
            Name = OfferNameBox.Text,
            ContentsText = ContentsBox.Text,
            PricePearls = Parse(PriceBox.Text),
            OriginalPricePearls = Parse(OriginalPriceBox.Text),
            HoursUntilOfferEnds = ParseSigned(ExpiryHoursBox.Text, -1),
            RandomContents = RandomBox.IsChecked == true,
            CharacterBound = CharacterBoundBox.IsChecked == true,
            PermanentFamilyWide = PermanentBox.IsChecked == true,
            HasFreeAlternative = FreeAlternativeBox.IsChecked == true,
            AlreadyOwnEquivalent = DuplicateBox.IsChecked == true,
            MostlyConsumablesOrPadding = PaddingBox.IsChecked == true,
            WouldBuyContentsIndividually = BuyAllBox.IsChecked == true
        });
        ScoreText.Text = $"{result.Score}/100";
        VerdictText.Text = result.Verdict;
        SummaryText.Text = result.Summary;
        PositivesList.ItemsSource = result.Positives.Count > 0 ? result.Positives : new[] { "No strong positive signal entered." };
        WarningsList.ItemsSource = result.Warnings.Count > 0 ? result.Warnings : new[] { "No automatic warning matched. Still verify contents, sale dates, and binding." };
    }

    private void OnClearSpendShield(object sender, RoutedEventArgs e)
    {
        if (_hub.Settings.Current.PearlSpendingFreezeUntilUtc is not { } until || until <= DateTimeOffset.UtcNow)
        {
            StatusText.Text = "No active no-spend shield to clear.";
            Refresh();
            return;
        }

        var answer = MessageBox.Show(
            $"The no-spend shield is active until {until.ToLocalTime():f}. Clear it now?",
            "Clear no-spend shield",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        _hub.Settings.Update(s => s.PearlSpendingFreezeUntilUtc = null);
        Refresh();
        StatusText.Text = "No-spend shield cleared by the user. Offer scoring remains conservative.";
    }

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Syncing current official Pearl Shop notices…";
        try
        {
            var report = await _hub.KnowledgeSync.SyncAllAsync(false);
            var source = report.Sources.FirstOrDefault(x => x.SourceId == "official-pearl-shop");
            StatusText.Text = source is null ? "Sync finished." : $"Pearl source: {(source.Success ? "healthy" : "degraded")} · {source.Message}";
            Refresh();
        }
        catch (Exception ex) { StatusText.Text = "Sync failed: " + ex.Message; }
    }

    private async void OnAskSelected(object sender, RoutedEventArgs e)
    {
        if (OffersList.SelectedItem is not OfferRow row)
        {
            StatusText.Text = "Select a current official Pearl Shop notice first.";
            return;
        }
        StatusText.Text = "Sending the selected official offer to the advisor with your budget and purchase profile…";
        await _hub.Conversation.SendAsync($"Evaluate this current official Pearl Shop notice for me: {row.Title}. Source metadata: {row.Meta}. Summary: {row.Summary}. Tell me what has real durable value, what is padding, binding or RNG risk, free alternatives, and whether to buy, wait, or skip for my saved profile.");
        StatusText.Text = "Evaluation added to the shared AI Advisor conversation.";
    }

    private void OnOpenOffer(object sender, MouseButtonEventArgs e)
    {
        if (OffersList.SelectedItem is OfferRow row && !string.IsNullOrWhiteSpace(row.Url))
            Process.Start(new ProcessStartInfo(row.Url) { UseShellExecute = true });
    }

    private static int Parse(string text) => int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? Math.Max(0, n) : 0;
    private static int ParseSigned(string text, int fallback) => int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}
