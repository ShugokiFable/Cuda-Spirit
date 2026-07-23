using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CudaSpirit.App.Infra;

namespace CudaSpirit.App.Views;

public partial class DataCenterView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;

    public DataCenterView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var stats = _hub.Db.GetStats();
        StatsText.Text = $"{stats.KnowledgeRecords:N0} knowledge records · {stats.MarketSnapshots:N0} market snapshots · " +
                         $"{stats.MarketHistoryPoints:N0} price observations · {stats.RouteNodes:N0} route nodes · {stats.RouteEdges:N0} edges · {stats.ImportedFiles:N0} tracked JSON files\n" +
                         $"Database: {_hub.Db.DatabasePath}" +
                         (stats.FreshestKnowledgeAt is { } fresh ? $"\nFreshest record: {fresh.LocalDateTime:g}" : "");

        SourcesGrid.ItemsSource = _hub.Db.GetSourceStates().Select(s => new
        {
            Name = s.DisplayName,
            s.Status,
            LastSuccess = s.LastSuccessAt?.LocalDateTime.ToString("g") ?? "Never",
            Records = s.LastRecordCount.ToString("N0"),
            Error = s.LastError
        }).ToList();
    }

    private async void OnSync(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        StatusText.Text = "Syncing official updates, live market data, and configured local exports…";
        try
        {
            var report = await _hub.KnowledgeSync.SyncAllAsync(includeLocalImport: true);
            StatusText.Text = string.Join("\n", report.Sources.Select(x =>
                $"{(x.Success ? "✓" : "!")} {x.DisplayName}: {x.Message} ({x.Duration.TotalSeconds:0.0}s)"));
        }
        catch (Exception ex)
        {
            StatusText.Text = "Sync failed: " + ex.Message;
        }
        finally
        {
            SyncButton.IsEnabled = true;
            Refresh();
        }
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select BDO JSON export directory",
            Multiselect = false,
            InitialDirectory = System.IO.Directory.Exists(_hub.Settings.Current.LocalDataDirectory)
                ? _hub.Settings.Current.LocalDataDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog() != true) return;

        StatusText.Text = "Importing and indexing JSON…";
        try
        {
            _hub.Settings.Update(s => s.LocalDataDirectory = dialog.FolderName);
            var result = await _hub.KnowledgeSync.ImportLocalAsync(dialog.FolderName);
            StatusText.Text = result.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Import failed: " + ex.Message;
        }
        finally
        {
            Refresh();
        }
    }

    private void OnSearch(object sender, RoutedEventArgs e) => Search();

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Search();
    }

    private void Search()
    {
        var hits = _hub.Knowledge.Search(SearchBox.Text, _hub.Settings.Current.Region, 30);
        ResultsList.ItemsSource = hits.Select(x => new
        {
            x.Record.Title,
            Summary = string.IsNullOrWhiteSpace(x.Record.Summary)
                ? x.Record.Content[..Math.Min(x.Record.Content.Length, 500)]
                : x.Record.Summary,
            Meta = $"{x.Record.Kind} · {x.Record.SourceId} · {x.Record.Region} · " +
                   $"effective {(x.Record.EffectiveAt?.LocalDateTime.ToString("g") ?? "unknown")} · retrieved {x.Record.RetrievedAt.LocalDateTime:g}"
        }).ToList();
        StatusText.Text = $"Found {hits.Count} indexed record(s).";
    }
}
