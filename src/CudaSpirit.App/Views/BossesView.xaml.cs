using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class BossesView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;
    private readonly DispatcherTimer _timer;

    public BossesView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
        Refresh();
    }

    public void Refresh()
    {
        BossList.ItemsSource = _hub.Bosses.GetUpcoming(12).Select(b => new
        {
            b.Name,
            Sub = $"{b.Kind} boss · {b.Notes}",
            Countdown = b.IsLive ? "LIVE" : Format(b.TimeUntil),
            When = $"{b.NextSpawn:dddd, MMM d · HH:mm}",
            Accent = Accent(b)
        }).ToList();
    }

    private static Brush Accent(BossEvent b)
    {
        if (b.IsLive) return new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x3D));
        if (b.IsImminent) return new SolidColorBrush(Color.FromRgb(0xE0, 0x1E, 0x37));
        return new SolidColorBrush(Color.FromRgb(0x8B, 0x4F, 0xE0));
    }

    private static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h" :
               t.TotalHours >= 1 ? $"{t.Hours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";
    }
}
