using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class RoutePlannerView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;

    public RoutePlannerView()
    {
        InitializeComponent();
        ObjectiveBox.ItemsSource = Enum.GetValues(typeof(RouteObjective));
        ObjectiveBox.SelectedItem = RouteObjective.Balanced;
        Refresh();
    }

    public void Refresh()
    {
        var nodes = _hub.Db.GetRouteNodes();
        var selectedStart = (StartBox.SelectedItem as RouteNode)?.Key;
        var selectedDestination = (DestinationBox.SelectedItem as RouteNode)?.Key;
        StartBox.ItemsSource = nodes;
        DestinationBox.ItemsSource = nodes;
        StartBox.SelectedItem = nodes.FirstOrDefault(x => x.Key == selectedStart) ?? nodes.FirstOrDefault();
        DestinationBox.SelectedItem = nodes.FirstOrDefault(x => x.Key == selectedDestination) ?? nodes.Skip(1).FirstOrDefault() ?? nodes.FirstOrDefault();

        var state = _hub.Live.Current;
        ApBox.Text = Math.Max(state.Ap, state.Awakening).ToString(CultureInfo.InvariantCulture);
        DpBox.Text = state.Dp.ToString(CultureInfo.InvariantCulture);
        if (nodes.Count == 0)
            RouteSummary.Text = "No route data yet. Import nodes/routes/grind-zone JSON in Live Data Center.";
    }

    private void OnRecommend(object sender, RoutedEventArgs e)
    {
        var start = StartBox.SelectedItem as RouteNode;
        var objective = ObjectiveBox.SelectedItem is RouteObjective o ? o : RouteObjective.Balanced;
        var recs = _hub.RoutePlanner.RecommendFarms(Parse(ApBox.Text), Parse(DpBox.Text), start?.Key, objective, RiskBox.Value, 20);
        RecommendationsList.ItemsSource = recs.Select((x, i) => new
        {
            Title = $"#{i + 1} {x.Zone.Name} · {x.Fit}",
            Detail = $"{x.Reason} Score {x.Score:0.00} · source {x.Zone.SourceId} · updated {x.Zone.UpdatedAt.LocalDateTime:g}"
        }).ToList();
        RouteSummary.Text = recs.Count == 0
            ? "No farm-value records were found. Import grind-zone data containing silverPerHour and recommended AP/DP."
            : "Recommendations use your imported data, gear fit, travel overhead, objective, and risk tolerance.";
    }

    private void OnPlan(object sender, RoutedEventArgs e)
    {
        if (StartBox.SelectedItem is not RouteNode start || DestinationBox.SelectedItem is not RouteNode destination)
        {
            RouteSummary.Text = "Choose a start and destination.";
            return;
        }

        var plan = _hub.RoutePlanner.Plan(new RoutePlanRequest
        {
            StartKey = start.Key,
            DestinationKey = destination.Key,
            Objective = ObjectiveBox.SelectedItem is RouteObjective o ? o : RouteObjective.Balanced,
            PlayerAp = Parse(ApBox.Text),
            PlayerDp = Parse(DpBox.Text),
            RiskTolerance = RiskBox.Value
        });
        RouteSummary.Text = plan.Found
            ? $"{plan.Message}\nTravel {plan.TotalTravelMinutes:0.#} minutes · risk {plan.TotalRisk:0.00} · destination {plan.DestinationSilverPerHour:N0} silver/hour"
            : plan.Message;
        RouteSteps.ItemsSource = plan.Steps.Select(x => $"{x.Order}. {x.Instruction}").ToList();
    }

    private static int Parse(string value) => int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
