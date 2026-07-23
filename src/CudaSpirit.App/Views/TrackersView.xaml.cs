using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CudaSpirit.App.Views;

public partial class TrackersView : UserControl, IRefreshable
{
    private readonly ObservableCollection<BuffTimer> _buffs = new();
    private readonly DispatcherTimer _timer;

    // Preset grind buffs (name, minutes). Durations are editable - pre-filled with common values.
    private static readonly (string Label, int Minutes)[] Presets =
    {
        ("Elixir", 8),
        ("Perfume", 30),
        ("Draught", 60),
        ("Meal", 90),
        ("Special Meal", 120),
        ("Cron Meal", 120),
        ("Custom", 60),
    };

    public TrackersView()
    {
        InitializeComponent();
        BuffList.ItemsSource = _buffs;
        PresetBox.ItemsSource = Presets.Select(p => $"{p.Label} ({p.Minutes}m)").ToList();
        PresetBox.SelectedIndex = 2; // Draught

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => { _timer.Start(); Tick(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    public void Refresh() => Tick();

    private void Tick()
    {
        RenderResets();
        foreach (var b in _buffs) b.Tick();
        // Drop timers that finished a while ago to keep the list tidy.
        for (int i = _buffs.Count - 1; i >= 0; i--)
            if (_buffs[i].SecondsLeft < -120) _buffs.RemoveAt(i);
        NoBuffs.Visibility = _buffs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Recurring resets ──────────────────────────────────────────────────────

    private void RenderResets()
    {
        var items = new List<object>
        {
            MakeReset("Night Vendor refresh", "Patrigio restocks every hour", NextEvery(1)),
            MakeReset("Imperial Delivery reset", "Sell limit resets (default every 3h)", NextEvery(3)),
            MakeReset("Daily reset", "Attendance / daily quests (default 00:00 local)", NextEvery(24)),
        };
        ResetList.ItemsSource = items;
    }

    private static object MakeReset(string name, string note, DateTimeOffset next)
    {
        var left = next - DateTimeOffset.Now;
        return new
        {
            Name = name,
            Note = note,
            Countdown = Format(left),
            When = next.ToString("ddd HH:mm"),
            Accent = left <= TimeSpan.FromMinutes(10)
                ? new SolidColorBrush(Color.FromRgb(0xE0, 0x1E, 0x37))
                : new SolidColorBrush(Color.FromRgb(0x8B, 0x4F, 0xE0))
        };
    }

    /// <summary>Next occurrence of an event that repeats every <paramref name="hours"/> from local midnight.</summary>
    private static DateTimeOffset NextEvery(int hours)
    {
        var now = DateTimeOffset.Now;
        var t = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        while (t <= now) t = t.AddHours(hours);
        return t;
    }

    // ── Buff timers ───────────────────────────────────────────────────────────

    private void OnPreset(object sender, RoutedEventArgs e)
    {
        int i = PresetBox.SelectedIndex;
        if (i < 0 || i >= Presets.Length) return;
        var p = Presets[i];
        LabelBox.Text = p.Label == "Custom" ? "" : p.Label;
        MinutesBox.Text = p.Minutes.ToString();
    }

    private void OnStartBuff(object sender, RoutedEventArgs e)
    {
        double minutes = double.TryParse(MinutesBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : 0;
        if (minutes <= 0) return;
        var label = string.IsNullOrWhiteSpace(LabelBox.Text) ? "Buff" : LabelBox.Text.Trim();
        _buffs.Add(new BuffTimer(label, minutes * 60));
        NoBuffs.Visibility = Visibility.Collapsed;
        Tick();
    }

    private void OnRemoveBuff(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id })
        {
            var b = _buffs.FirstOrDefault(x => x.Id == id);
            if (b is not null) _buffs.Remove(b);
            Tick();
        }
    }

    private static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }
}

/// <summary>A single running grind-buff countdown.</summary>
public sealed class BuffTimer : INotifyPropertyChanged
{
    private const double BarMax = 240;

    public Guid Id { get; } = Guid.NewGuid();
    public string Label { get; }
    public DateTimeOffset EndsAt { get; }
    public double TotalSeconds { get; }

    public BuffTimer(string label, double totalSeconds)
    {
        Label = label;
        TotalSeconds = totalSeconds;
        EndsAt = DateTimeOffset.Now.AddSeconds(totalSeconds);
    }

    public double SecondsLeft => (EndsAt - DateTimeOffset.Now).TotalSeconds;
    private bool Expired => SecondsLeft <= 0;

    public string Remaining
    {
        get
        {
            if (Expired) return "REFRESH";
            var t = TimeSpan.FromSeconds(SecondsLeft);
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }
    }

    public double BarWidth => TotalSeconds <= 0 ? 0 : Math.Clamp(SecondsLeft / TotalSeconds, 0, 1) * BarMax;

    public Brush Edge => Expired
        ? new SolidColorBrush(Color.FromRgb(0xE0, 0x1E, 0x37))
        : SecondsLeft < 60
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x3D))
            : new SolidColorBrush(Color.FromRgb(0x8B, 0x4F, 0xE0));

    public Brush TextColor => Expired
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x3D))
        : new SolidColorBrush(Color.FromRgb(0xED, 0xE7, 0xF5));

    public Brush Bg => new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x12, 0x1F));

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Tick()
    {
        foreach (var p in new[] { nameof(Remaining), nameof(BarWidth), nameof(Edge), nameof(TextColor) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
