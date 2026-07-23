using System.Globalization;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Enhancement;

namespace CudaSpirit.App.Views;

public partial class EnhancementView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private bool _ready;

    public EnhancementView()
    {
        InitializeComponent();
        KindBox.ItemsSource = Enum.GetValues(typeof(EnhanceKind));
        var grades = Enum.GetValues(typeof(EnhanceGrade));
        CurrentBox.ItemsSource = grades;
        TargetBox.ItemsSource = grades;
        KindBox.SelectedItem = EnhanceKind.Weapon;
        CurrentBox.SelectedItem = EnhanceGrade.TRI;
        TargetBox.SelectedItem = EnhanceGrade.TET;
        _ready = true;
        Recalculate();
    }

    public void Refresh() => Recalculate();

    private void OnChanged(object sender, System.Windows.RoutedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        if (!_ready) return;
        var kind = (EnhanceKind)(KindBox.SelectedItem ?? EnhanceKind.Weapon);
        var current = (EnhanceGrade)(CurrentBox.SelectedItem ?? EnhanceGrade.TRI);
        var target = (EnhanceGrade)(TargetBox.SelectedItem ?? EnhanceGrade.TET);
        int stacks = ParseInt(StacksBox.Text, 30);
        bool cron = CronBox.IsChecked == true;
        int cronPer = ParseInt(CronCostBox.Text, 1);

        var sim = _hub.Enhancement;

        // Odds for the tap out of the grade just below target.
        var tapGrade = target > EnhanceGrade.Base ? (EnhanceGrade)((int)target - 1) : current;
        double chance = sim.ChanceAt(kind, tapGrade, stacks);
        ChanceText.Text = chance.ToString("P1", CultureInfo.InvariantCulture);

        var (rmin, rmax) = sim.RecommendedStacks(target);
        RecStacksText.Text = $"Sweet spot for {target}: {rmin}–{rmax} stacks";

        var estCron = sim.Estimate(kind, current, target, stacks, useCron: true, cronPer);
        var estNoCron = sim.Estimate(kind, current, target, stacks, useCron: false, cronPer);
        var est = cron ? estCron : estNoCron;

        if (target <= current)
        {
            TapsText.Text = "Target is at or below current grade.";
            CronsText.Text = "";
            ConfText.Text = "";
            TotalCostText.Text = "-";
            CostBreakdownText.Text = "";
            CronCompareText.Text = "";
        }
        else
        {
            TapsText.Text = $"≈ {est.ExpectedTaps:N1} taps on average";
            CronsText.Text = cron ? $"≈ {est.ExpectedCrons:N0} cron stones" : "No cron (downgrade on fail)";
            ConfText.Text = $"~{est.SuccessWithinExpectedTaps:P0} chance within that tap budget";

            RenderCost(est, estCron, estNoCron, cron);
        }

        RenderCurve(kind, tapGrade);
    }

    private void RenderCost(EnhanceEstimate est, EnhanceEstimate estCron, EnhanceEstimate estNoCron, bool cron)
    {
        long matCost = ParseLong(MatCostBox.Text, 0);
        long cronPrice = ParseLong(CronPriceBox.Text, 0);

        if (matCost <= 0 && cronPrice <= 0)
        {
            TotalCostText.Text = "-";
            CostBreakdownText.Text = "Enter your material cost/tap and cron price for a silver estimate.";
            CronCompareText.Text = "";
            return;
        }

        long mat = (long)(est.ExpectedTaps * matCost);
        long crons = cron ? (long)(est.ExpectedCrons * cronPrice) : 0;
        long total = mat + crons;
        TotalCostText.Text = $"{total:N0}  ({Short(total)})";
        CostBreakdownText.Text = cron
            ? $"materials ≈ {Short(mat)}  +  {est.ExpectedCrons:N0} crons ≈ {Short(crons)}"
            : $"materials ≈ {Short(mat)}  (no cron)";

        // Compare cron vs no-cron on total silver (no-cron omits item-destruction losses).
        long costCron = (long)(estCron.ExpectedTaps * matCost + estCron.ExpectedCrons * cronPrice);
        long costNoCron = (long)(estNoCron.ExpectedTaps * matCost);
        string cheaper = costCron <= costNoCron ? "cron is cheaper here" : "no-cron is cheaper on paper";
        CronCompareText.Text =
            $"With cron ≈ {Short(costCron)}  ·  Without cron ≈ {Short(costNoCron)} - {cheaper}. " +
            "No-cron cost ignores the silver you lose when the item degrades/breaks, so cron usually wins on high grades.";
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

    private void RenderCurve(EnhanceKind kind, EnhanceGrade grade)
    {
        var sim = _hub.Enhancement;
        var rows = new List<object>();
        foreach (var fs in new[] { 0, 10, 20, 30, 40, 50, 70, 90, 110, 140 })
        {
            double p = sim.ChanceAt(kind, grade, fs);
            rows.Add(new
            {
                Label = $"{fs} FS",
                Pct = p.ToString("P0", CultureInfo.InvariantCulture),
                BarWidth = Math.Max(2.0, p * 320.0)
            });
        }
        CurveList.ItemsSource = rows;
    }

    private static int ParseInt(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
