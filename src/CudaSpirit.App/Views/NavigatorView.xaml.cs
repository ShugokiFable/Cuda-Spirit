using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class NavigatorView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;

    public NavigatorView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var s = _hub.Settings.Current;
        GoalText.Text = s.CurrentGoal;
        ProfileText.Text = $"{s.AdventurerStage} · {s.PlayFocus} · {s.SpendingStyle} · {s.WeeklyPlayHours}h/week" +
                           (string.IsNullOrWhiteSpace(s.MainClass) ? "" : $" · Main: {s.MainClass}");
        var tasks = _hub.Navigator.Generate();
        TasksGrid.ItemsSource = tasks;
        QueueCount.Text = $"{tasks.Count} open";
        var next = tasks.FirstOrDefault();
        NextTitle.Text = next?.Title ?? "Plan is clear";
        NextDetail.Text = next?.Detail ?? "Add a goal or rebuild the plan when your stage changes.";
    }

    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        Refresh();
        StatusText.Text = $"Plan rebuilt from your current stage and preferences at {DateTime.Now:t}.";
    }

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Syncing official guides, events, Pearl Shop notices, patch notes, and market data…";
        try
        {
            var report = await _hub.KnowledgeSync.SyncAllAsync(true);
            StatusText.Text = $"Sync complete: {report.Sources.Count(x => x.Success)}/{report.Sources.Count} sources healthy, {report.TotalRecords:N0} records processed.";
        }
        catch (Exception ex) { StatusText.Text = "Sync failed: " + ex.Message; }
    }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: long id }) _hub.Db.SetTaskStatus(id, "done");
        else if (sender is Button b && long.TryParse(Convert.ToString(b.Tag), out var parsed)) _hub.Db.SetTaskStatus(parsed, "done");
        Refresh();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && long.TryParse(Convert.ToString(b.Tag), out var id)) _hub.Db.DeleteTask(id);
        Refresh();
    }

    private void OnAddTask(object sender, RoutedEventArgs e)
    {
        var title = Microsoft.VisualBasic.Interaction.InputBox("What do you need to remember?", "Add Cuda Spirit task", "");
        if (string.IsNullOrWhiteSpace(title)) return;
        _hub.Db.UpsertTask(new CompanionTask { Title = title.Trim(), Detail = "Custom task", Category = "custom", Priority = 60 });
        Refresh();
    }
}
