using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class RecoveryCenterView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private ObservableCollection<RetirementCheckItem> _retirement = new();

    public RecoveryCenterView()
    {
        InitializeComponent();
        RangeBox.ItemsSource = new[] { "Any", "Melee", "Ranged", "Hybrid" };
        PaceBox.ItemsSource = new[] { "Balanced", "Fast", "Slow" };
        ComplexityBox.ItemsSource = new[] { "Simple", "Moderate", "High" };
        SurvivalBox.ItemsSource = new[] { "Balanced", "High", "Low" };
        FocusBox.ItemsSource = new[] { "PvE", "Mixed", "PvP" };
        RangeBox.SelectedIndex = 0;
        PaceBox.SelectedIndex = 0;
        ComplexityBox.SelectedIndex = 1;
        SurvivalBox.SelectedIndex = 0;
        FocusBox.SelectedIndex = 0;
        ResetRetirement();
        BuildReturner(false);
        RecommendClasses();
    }

    public void Refresh()
    {
        var states = _hub.Db.GetSourceStates();
        var healthy = states.Count(x => x.Status.Equals("healthy", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("ready", StringComparison.OrdinalIgnoreCase));
        StatusText.Text = $"Live sources ready: {healthy}/{states.Count}";
    }

    private ReturnerProfile ReadReturnerProfile() => new()
    {
        MonthsAway = int.TryParse(MonthsAwayBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var months) ? Math.Clamp(months, 0, 240) : 6,
        InventoryIsChaotic = InventoryChaosBox.IsChecked == true,
        UnsureAboutCurrentGear = GearUnsureBox.IsChecked == true,
        UnsureAboutMainClass = MainUnsureBox.IsChecked == true,
        WantsFreshCharacter = FreshCharacterBox.IsChecked == true,
        HasUnclaimedRewards = RewardsBox.IsChecked == true,
        HasSeasonCharacter = SeasonBox.IsChecked == true,
        WantsToSpendPearls = SpendBox.IsChecked == true,
        Goal = ReturnerGoalBox.Text
    };

    private void BuildReturner(bool addTasks)
    {
        var plan = _hub.ReturnerRecovery.Build(ReadReturnerProfile(), addTasks);
        ReturnerHeadline.Text = plan.Headline;
        ReturnerSummary.Text = plan.Summary;
        ReturnerGrid.ItemsSource = plan.Steps;
        StatusText.Text = addTasks ? "Recovery plan added to Navigator." : "Recovery plan rebuilt.";
    }

    private void OnBuildReturner(object sender, RoutedEventArgs e) => BuildReturner(false);
    private void OnAddReturnerTasks(object sender, RoutedEventArgs e) => BuildReturner(true);

    private void OnActivateSpendShield(object sender, RoutedEventArgs e)
    {
        var until = DateTimeOffset.UtcNow.AddDays(7);
        _hub.Settings.Update(s => s.PearlSpendingFreezeUntilUtc = until);
        StatusText.Text = $"No-spend shield active until {until.ToLocalTime():f}. Pearl evaluations will fail closed until then.";
    }

    private async void OnRescueMe(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Running returner rescue: profile, live sources, safety shield, and Navigator plan…";
        var notes = new List<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(_hub.Settings.Current.ProfileToken))
            {
                try
                {
                    await _hub.SyncFromProfileAsync(_hub.Settings.Current.ProfileToken);
                    notes.Add("profile refreshed");
                    try
                    {
                        if (await _hub.SyncFromGarmothAsync() is not null) notes.Add("gear enrichment refreshed");
                    }
                    catch { notes.Add("optional gear enrichment unavailable"); }
                }
                catch
                {
                    notes.Add("profile unavailable; continuing with saved state");
                }
            }

            var report = await _hub.KnowledgeSync.SyncAllAsync(true);
            notes.Add($"{report.TotalRecords:N0} live records processed");
            BuildReturner(true);

            var until = DateTimeOffset.UtcNow.AddDays(7);
            _hub.Settings.Update(s => s.PearlSpendingFreezeUntilUtc = until);
            notes.Add("7-day no-spend shield enabled");
            StatusText.Text = "Returner rescue complete: " + string.Join(" · ", notes) + ".";
        }
        catch (Exception ex)
        {
            BuildReturner(true);
            StatusText.Text = "Recovery plan created with cached safety data; one or more live sources failed safely: " + ex.Message;
        }
    }

    private void ResetRetirement()
    {
        _retirement = new ObservableCollection<RetirementCheckItem>(_hub.CharacterRetirement.CreateChecklist());
        foreach (var item in _retirement) item.PropertyChanged += (_, _) => UpdateRetirementAssessment();
        RetirementGrid.ItemsSource = _retirement;
        UpdateRetirementAssessment();
    }

    private void UpdateRetirementAssessment()
    {
        var result = _hub.CharacterRetirement.Assess(_retirement);
        RetirementVerdict.Text = result.Verdict;
        RetirementProgress.Text = $"{result.Completed}/{result.Total} confirmed · {result.ReadinessPercent}% complete · {result.CriticalRemaining} critical remaining";
        RetirementVerdict.Foreground = result.SafeToDelete
            ? (System.Windows.Media.Brush)FindResource("EmberBrush")
            : (System.Windows.Media.Brush)FindResource("CrimsonBrush");
    }

    private void OnRetirementCellChanged(object sender, EventArgs e)
    {
        RetirementGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RetirementGrid.CommitEdit(DataGridEditingUnit.Row, true);
        UpdateRetirementAssessment();
    }

    private void OnRecalculateRetirement(object sender, RoutedEventArgs e) => UpdateRetirementAssessment();
    private void OnResetRetirement(object sender, RoutedEventArgs e) => ResetRetirement();

    private ClassPreferenceInput ReadClassPreferences() => new()
    {
        Range = Convert.ToString(RangeBox.SelectedItem) ?? "Any",
        Pace = Convert.ToString(PaceBox.SelectedItem) ?? "Balanced",
        Complexity = Convert.ToString(ComplexityBox.SelectedItem) ?? "Moderate",
        Survivability = Convert.ToString(SurvivalBox.SelectedItem) ?? "Balanced",
        Focus = Convert.ToString(FocusBox.SelectedItem) ?? "PvE",
        WantsSupport = SupportBox.IsChecked == true,
        WantsGrab = GrabBox.IsChecked == true,
        AvoidsHighApm = LowApmBox.IsChecked == true
    };

    private void RecommendClasses()
    {
        ClassGrid.ItemsSource = _hub.ClassAdvisor.Recommend(ReadClassPreferences(), 6);
        StatusText.Text = "Class matches refreshed. Trial before permanent investment.";
    }

    private void OnRecommendClasses(object sender, RoutedEventArgs e) => RecommendClasses();

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Syncing current guides, classes, events, offers, patch notes, and market data…";
        try
        {
            var report = await _hub.KnowledgeSync.SyncAllAsync(true);
            var failed = report.Sources.Count(x => !x.Success);
            StatusText.Text = failed == 0
                ? $"Sync complete: {report.TotalRecords:N0} records processed across {report.Sources.Count} sources."
                : $"Sync complete with {failed} degraded source(s). Cached and curated safety data remain available.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Sync failed safely: " + ex.Message;
        }
    }
}
