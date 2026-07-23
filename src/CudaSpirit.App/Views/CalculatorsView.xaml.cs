using System.Globalization;
using System.Windows.Controls;

namespace CudaSpirit.App.Views;

public partial class CalculatorsView : UserControl, IRefreshable
{
    // Widely-used community payout rates (fraction of sale price you keep after tax).
    private static readonly (string Label, double Rate)[] TaxTiers =
    {
        ("No bonuses - 65%", 0.65),
        ("Value Pack - 84.5%", 0.845),
        ("VP + Merchant Ring - 88.5%", 0.885),
        ("VP + Ring + Family Fame - 90%", 0.905),
    };

    private const long VendorCronPrice = 3_000_000; // fixed vendor price per cron

    private bool _ready;

    public CalculatorsView()
    {
        InitializeComponent();
        TaxTierBox.ItemsSource = TaxTiers.Select(t => t.Label).ToList();
        TaxTierBox.SelectedIndex = 1; // Value Pack
        _ready = true;
        RecalcTax();
        RecalcCron();
    }

    public void Refresh() { RecalcTax(); RecalcCron(); }

    private void OnTaxChanged(object sender, System.Windows.RoutedEventArgs e) => RecalcTax();
    private void OnCronChanged(object sender, System.Windows.RoutedEventArgs e) => RecalcCron();

    private void RecalcTax()
    {
        if (!_ready) return;
        long price = ParseLong(PriceBox.Text, 0);
        int idx = Math.Clamp(TaxTierBox.SelectedIndex, 0, TaxTiers.Length - 1);
        double rate = TaxTiers[idx].Rate;

        long net = (long)(price * rate);
        long tax = price - net;
        NetText.Text = $"{net:N0}";
        TaxText.Text = $"tax {tax:N0} ({1 - rate:P1})  ·  {Short(net)} net of {Short(price)}";
    }

    private void RecalcCron()
    {
        if (!_ready) return;
        long need = ParseLong(CronNeedBox.Text, 0);
        long mergePrice = ParseLong(MergePriceBox.Text, 0);

        long vendorTotal = need * VendorCronPrice;
        long mergeTotal = need * mergePrice;
        VendorText.Text = $"Vendor: {Short(vendorTotal)}  ({VendorCronPrice:N0} ea)";
        MergeText.Text = mergePrice > 0 ? $"Merge:  {Short(mergeTotal)}  ({mergePrice:N0} ea)" : "Merge:  enter a price";

        if (mergePrice <= 0 || need <= 0)
        {
            CronVerdict.Text = "";
            return;
        }
        long save = Math.Abs(vendorTotal - mergeTotal);
        CronVerdict.Text = mergeTotal < vendorTotal
            ? $"Merge saves {Short(save)} vs vendor."
            : $"Vendor is cheaper by {Short(save)} - merged crons cost more than 3M here.";
    }

    private static string Short(long v)
    {
        if (v >= 1_000_000_000) return $"{v / 1_000_000_000.0:0.00}B";
        if (v >= 1_000_000) return $"{v / 1_000_000.0:0.0}M";
        if (v >= 1_000) return $"{v / 1_000.0:0.0}K";
        return v.ToString();
    }

    private static long ParseLong(string s, long fallback) =>
        long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
