using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class CustomizeView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private bool _loading;

    private sealed record ThemeChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record PageChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    public CustomizeView()
    {
        InitializeComponent();
        ThemeBox.ItemsSource = new[]
        {
            new ThemeChoice("black-spirit", "Obsidian Gold"),
            new ThemeChoice("abyssal-oled", "Abyssal OLED"),
            new ThemeChoice("serendia-gold", "Serendia Gold"),
            new ThemeChoice("kamasylvia", "Kamasylvia Grove"),
            new ThemeChoice("snowfield", "Mountain of Eternal Winter")
        };
        DensityBox.ItemsSource = new[] { "compact", "comfortable", "spacious" };
        StageBox.ItemsSource = Enum.GetValues<AdventurerStage>();
        FocusBox.ItemsSource = Enum.GetValues<PlayFocus>();
        SpendingBox.ItemsSource = Enum.GetValues<SpendingStyle>();
        StartupBox.ItemsSource = new[]
        {
            new PageChoice("navigator", "Adventurer Navigator"),
            new PageChoice("rewards", "Rewards & Redemption"),
            new PageChoice("items", "Item Intel & Transfer"),
            new PageChoice("pearl", "Pearl Shop Guard"),
            new PageChoice("dashboard", "Classic Dashboard"),
            new PageChoice("advisor", "AI Advisor")
        };
        Refresh();
    }

    public void Refresh()
    {
        _loading = true;
        var s = _hub.Settings.Current;
        ThemeBox.SelectedItem = ThemeBox.Items.Cast<ThemeChoice>().FirstOrDefault(x => x.Id == s.ThemeId) ?? ThemeBox.Items[0];
        DensityBox.SelectedItem = s.Density;
        FontScaleSlider.Value = s.FontScale;
        FontScaleText.Text = $"{s.FontScale:P0}";
        CompactNavBox.IsChecked = s.CompactNavigation;
        ReducedMotionBox.IsChecked = s.ReducedMotion;
        BeginnerHintsBox.IsChecked = s.ShowBeginnerHints;
        StartupBox.SelectedItem = StartupBox.Items.Cast<PageChoice>().FirstOrDefault(x => x.Id == s.StartupPage) ?? StartupBox.Items[0];
        StageBox.SelectedItem = s.AdventurerStage;
        FocusBox.SelectedItem = s.PlayFocus;
        SpendingBox.SelectedItem = s.SpendingStyle;
        MainClassBox.Text = s.MainClass;
        GoalBox.Text = s.CurrentGoal;
        HoursBox.Text = s.WeeklyPlayHours.ToString(CultureInfo.InvariantCulture);
        BudgetBox.Text = s.MonthlyPearlBudget.ToString(CultureInfo.InvariantCulture);
        MagnusBox.IsChecked = s.HasMagnusStorage;
        StorageMaidBox.IsChecked = s.HasStorageMaid;
        TransactionMaidBox.IsChecked = s.HasTransactionMaid;
        TentBox.IsChecked = s.HasTent;
        _loading = false;
    }

    private void OnLivePreview(object sender, RoutedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not ThemeChoice theme || DensityBox.SelectedItem is not string density) return;
        FontScaleText.Text = $"{FontScaleSlider.Value:P0}";
        _hub.Appearance.Apply(theme.Id, FontScaleSlider.Value, density);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var theme = ThemeBox.SelectedItem as ThemeChoice;
        var page = StartupBox.SelectedItem as PageChoice;
        _hub.Settings.Update(s =>
        {
            s.ThemeId = theme?.Id ?? "black-spirit";
            s.Density = DensityBox.SelectedItem as string ?? "comfortable";
            s.FontScale = FontScaleSlider.Value;
            s.CompactNavigation = CompactNavBox.IsChecked == true;
            s.ReducedMotion = ReducedMotionBox.IsChecked == true;
            s.ShowBeginnerHints = BeginnerHintsBox.IsChecked == true;
            s.StartupPage = page?.Id ?? "navigator";
            if (StageBox.SelectedItem is AdventurerStage stage) s.AdventurerStage = stage;
            if (FocusBox.SelectedItem is PlayFocus focus) s.PlayFocus = focus;
            if (SpendingBox.SelectedItem is SpendingStyle spending) s.SpendingStyle = spending;
            s.MainClass = MainClassBox.Text.Trim();
            s.CurrentGoal = string.IsNullOrWhiteSpace(GoalBox.Text) ? "Get oriented and avoid expensive mistakes" : GoalBox.Text.Trim();
            s.WeeklyPlayHours = ParseInt(HoursBox.Text, s.WeeklyPlayHours, 1, 168);
            s.MonthlyPearlBudget = ParseInt(BudgetBox.Text, s.MonthlyPearlBudget, 0, 1_000_000);
            s.HasMagnusStorage = MagnusBox.IsChecked == true;
            s.HasStorageMaid = StorageMaidBox.IsChecked == true;
            s.HasTransactionMaid = TransactionMaidBox.IsChecked == true;
            s.HasTent = TentBox.IsChecked == true;
        });
        _hub.Appearance.Apply();
        if (Window.GetWindow(this) is MainWindow shell) shell.ApplyShellPreferences();
        _hub.Navigator.Generate();
        StatusText.Text = $"Saved and applied at {DateTime.Now:t}. Your profile now feeds Navigator, Item Intel, transfers, Pearl scoring, and the AI advisor.";
    }

    private void OnResetAppearance(object sender, RoutedEventArgs e)
    {
        _hub.Settings.Update(s =>
        {
            s.ThemeId = "black-spirit";
            s.Density = "comfortable";
            s.FontScale = 1.0;
            s.CompactNavigation = false;
            s.ReducedMotion = false;
            s.ShowBeginnerHints = true;
            s.StartupPage = "navigator";
        });
        _hub.Appearance.Apply();
        if (Window.GetWindow(this) is MainWindow shell) shell.ApplyShellPreferences();
        Refresh();
        StatusText.Text = "Appearance reset. Personal progression, API keys, tasks, and decision history were preserved.";
    }

    private static int ParseInt(string text, int fallback, int min, int max) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, min, max)
            : fallback;
}
