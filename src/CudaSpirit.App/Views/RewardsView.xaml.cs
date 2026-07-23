using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class RewardsView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private List<RewardRow> _events = new();
    private List<RewardRow> _coupons = new();

    private sealed record RewardRow(string Title, string Meta, string Url);

    public RewardsView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var region = _hub.Settings.Current.Region.ToString().ToLowerInvariant();
        _events = _hub.Db.GetLatestKnowledge(60, region: region)
            .Where(x => x.Kind is KnowledgeKinds.Event or KnowledgeKinds.News or KnowledgeKinds.Maintenance)
            .Select(ToRow).ToList();
        _coupons = _hub.Db.GetLatestKnowledge(40, region: region)
            .Where(x => x.Kind == KnowledgeKinds.Coupon || x.Title.Contains("coupon", StringComparison.OrdinalIgnoreCase))
            .Select(ToRow).ToList();
        EventsList.ItemsSource = _events;
        CouponsList.ItemsSource = _coupons;
        StatusText.Text = $"Loaded {_events.Count} event/notice records and {_coupons.Count} coupon-related records from the local knowledge database.";
    }

    private static RewardRow ToRow(KnowledgeRecord x)
    {
        var date = x.ExpiresAt is { } expiry ? $"expires {expiry.LocalDateTime:g}" :
                   x.EffectiveAt is { } effective ? effective.LocalDateTime.ToString("g") : "date unknown";
        return new RewardRow(x.Title, $"{x.SourceId} · {date}", x.Url);
    }

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Refreshing official events, guides, Pearl Shop notices, patch notes, and configured live APIs…";
        try
        {
            var report = await _hub.KnowledgeSync.SyncAllAsync(true);
            StatusText.Text = string.Join(" · ", report.Sources.Select(x => $"{x.DisplayName}: {(x.Success ? "ok" : "degraded")}"));
            Refresh();
        }
        catch (Exception ex) { StatusText.Text = "Sync failed: " + ex.Message; }
    }

    private void OnOpenCouponGuide(object sender, RoutedEventArgs e) => Open("https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=165");
    private void OnOpenMailGuide(object sender, RoutedEventArgs e) => Open("https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=61");
    private void OnOpenSelectedEvent(object sender, MouseButtonEventArgs e) { if (EventsList.SelectedItem is RewardRow r) Open(r.Url); }
    private void OnOpenSelectedCoupon(object sender, MouseButtonEventArgs e) { if (CouponsList.SelectedItem is RewardRow r) Open(r.Url); }

    private static void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async void OnAskPriority(object sender, RoutedEventArgs e)
    {
        var eventsText = string.Join(" | ", _events.Take(12).Select(x => $"{x.Title} ({x.Meta})"));
        var couponsText = string.Join(" | ", _coupons.Take(8).Select(x => $"{x.Title} ({x.Meta})"));
        StatusText.Text = "Building a claim-first plan from current stored deadlines…";
        await _hub.Conversation.SendAsync($"Prioritize what I should claim or inspect first from these current database records. Events: {eventsText}. Coupons/notices: {couponsText}. Flag expiry, character-binding, class selection, inventory-space, and do-not-open-yet risks. Give exact claim surfaces.");
        StatusText.Text = "Claim-priority plan added to the shared AI Advisor conversation.";
    }

    private void OnResetChecks(object sender, RoutedEventArgs e)
    {
        foreach (var check in new[] { WebStorageCheck, MailCheck, SafeCheck, ChallengeCheck, AttendanceCheck, EventCheck, PassCheck, GuildCheck, CouponCheck, PearlCouponCheck })
            check.IsChecked = false;
    }
}
