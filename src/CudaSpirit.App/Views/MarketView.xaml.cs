using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class MarketView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private MarketItem? _last;

    public MarketView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh() => RenderAlerts();

    private async void OnLookup(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(ItemIdBox.Text, out var id))
        {
            StatusText.Text = "Enter a numeric item id.";
            return;
        }
        int sid = int.TryParse(SidBox.Text, out var s) ? s : 0;
        StatusText.Text = "Fetching…";
        try
        {
            var item = await _hub.Market.GetItemAsync(id, sid, _hub.Settings.Current.Region);
            if (item is null)
            {
                StatusText.Text = "No data returned for that item.";
                ResultCard.Visibility = Visibility.Collapsed;
                return;
            }
            _last = item;
            ResultName.Text = $"{item.Name}  (id {item.ItemId}, +{item.Sid})";
            ResultPrice.Text = $"{item.BasePrice:N0} silver";
            ResultMeta.Text = $"Stock {item.CurrentStock:N0}  ·  {item.TotalTrades:N0} total trades  ·  as of {item.Retrieved.ToLocalTime():HH:mm}";
            AlertPriceBox.Text = item.BasePrice.ToString();
            ResultCard.Visibility = Visibility.Visible;
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Lookup failed: {ex.Message}";
        }
    }

    private void OnAddAlert(object sender, RoutedEventArgs e)
    {
        if (_last is null) return;
        if (!long.TryParse(AlertPriceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var target))
            return;
        _hub.Db.UpsertAlert(new PriceAlert
        {
            ItemId = _last.ItemId,
            Sid = _last.Sid,
            ItemName = _last.Name,
            TargetPrice = target
        });
        RenderAlerts();
        StatusText.Text = $"Alert added for {_last.Name} at ≤ {target:N0}.";
    }

    private void OnDeleteAlert(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: long id })
        {
            _hub.Db.DeleteAlert(id);
            RenderAlerts();
        }
    }

    private async void OnCheckAlerts(object sender, RoutedEventArgs e)
    {
        var region = _hub.Settings.Current.Region;
        int fired = 0;
        foreach (var a in _hub.Db.GetAlerts().Where(a => a.Enabled))
        {
            var item = await _hub.Market.GetItemAsync(a.ItemId, a.Sid, region);
            if (item is not null && item.BasePrice <= a.TargetPrice)
            {
                a.LastTriggered = DateTimeOffset.UtcNow;
                _hub.Db.UpsertAlert(a);
                fired++;
            }
        }
        RenderAlerts();
        StatusText.Text = fired > 0
            ? $"{fired} alert(s) triggered - check the list, then buy in-game."
            : "No alerts triggered right now.";
    }

    private void RenderAlerts()
    {
        AlertList.ItemsSource = _hub.Db.GetAlerts().Select(a => new
        {
            a.Id,
            Title = $"{a.ItemName}  (+{a.Sid})",
            Sub = $"Notify at ≤ {a.TargetPrice:N0} silver",
            State = a.LastTriggered is { } t ? $"⚑ fired {t.ToLocalTime():MM/dd HH:mm}" : "watching"
        }).ToList();
    }
}
