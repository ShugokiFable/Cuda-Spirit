using System.Windows;
using System.Windows.Media;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.App.Infra;

/// <summary>Mutates the shared brushes/resources so existing windows update without a restart.</summary>
public sealed class AppearanceService
{
    private readonly SettingsService _settings;

    private sealed record Palette(
        string Deep, string Panel, string PanelHi, string Accent, string AccentDim,
        string Secondary, string SecondaryDim, string Ink, string InkSoft, string Ember,
        string SmokeA, string SmokeB, string SmokeC);

    private static readonly Dictionary<string, Palette> Palettes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Obsidian is the default: matte near-black surfaces, warm metal accent, no candy glow.
        ["black-spirit"] = new("#060708", "#0D0F12", "#15181D", "#D8AA63", "#6D5128", "#8793A4", "#46515F", "#F4F1EA", "#9B9EA6", "#E37A3D", "#08090B", "#060708", "#0B0C0F"),
        ["abyssal-oled"] = new("#020405", "#070B0F", "#0D141B", "#4DB6D7", "#17586D", "#8298B8", "#40516A", "#F1F6F8", "#8FA3AD", "#6EC7D9", "#020405", "#030608", "#05090D"),
        ["serendia-gold"] = new("#080806", "#11100C", "#191711", "#C8A15E", "#675126", "#8C806A", "#4B4437", "#F2EBDD", "#A8A08E", "#D5B56D", "#090806", "#080806", "#0D0C08"),
        ["kamasylvia"] = new("#050A08", "#0A1210", "#101B17", "#55B68A", "#245C45", "#789A91", "#3D5A53", "#ECF6F1", "#91AAA0", "#75C19F", "#050A08", "#060C0A", "#08110E"),
        ["snowfield"] = new("#080B0F", "#10151B", "#18202A", "#8FB7CD", "#3E667B", "#9AA8BE", "#505C70", "#F3F6F8", "#9DA9B5", "#B8D3DF", "#090C10", "#080B0F", "#0D1218"),
    };

    public AppearanceService(SettingsService settings) => _settings = settings;

    public IReadOnlyList<string> ThemeIds => Palettes.Keys.ToList();

    public void Apply() => Apply(_settings.Current.ThemeId, _settings.Current.FontScale, _settings.Current.Density);

    public void Apply(string themeId, double fontScale, string density)
    {
        if (!Palettes.TryGetValue(themeId, out var p)) p = Palettes["black-spirit"];
        var deep = Parse(p.Deep);
        var panel = Parse(p.Panel);
        var panelHi = Parse(p.PanelHi);
        var accent = Parse(p.Accent);
        var secondary = Parse(p.Secondary);
        var ink = Parse(p.Ink);

        SetBrush("BgDeepBrush", deep);
        SetBrush("BgPanelBrush", panel);
        SetBrush("BgPanelHiBrush", panelHi);
        SetBrush("BgPanelAltBrush", Blend(panelHi, ink, 0.06));
        SetBrush("CrimsonBrush", accent);
        SetBrush("CrimsonDimBrush", Parse(p.AccentDim));
        SetBrush("PurpleBrush", secondary);
        SetBrush("PurpleDimBrush", Parse(p.SecondaryDim));
        SetBrush("InkBrush", ink);
        SetBrush("InkSoftBrush", Parse(p.InkSoft));
        SetBrush("EmberBrush", Parse(p.Ember));
        SetBrush("BorderSubtleBrush", WithAlpha(ink, 0x20));
        SetBrush("BorderStrongBrush", WithAlpha(ink, 0x46));
        SetBrush("SurfaceHoverBrush", WithAlpha(ink, 0x12));
        SetBrush("SurfacePressedBrush", WithAlpha(ink, 0x20));
        SetBrush("AccentWashBrush", WithAlpha(accent, 0x26));
        SetBrush("SecondaryWashBrush", WithAlpha(secondary, 0x22));
        SetColor("Crimson", accent);
        SetColor("Purple", secondary);
        SetColor("Ember", Parse(p.Ember));

        Application.Current.Resources["AccentGradientBrush"] = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(accent, 0), new(Blend(accent, Parse(p.Ember), 0.58), 1)
            }, new Point(0, 0), new Point(1, 1));
        Application.Current.Resources["SecondaryGradientBrush"] = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(secondary, 0), new(Blend(secondary, Parse(p.Ember), 0.24), 1)
            }, new Point(0, 0), new Point(1, 1));
        Application.Current.Resources["SmokeBrush"] = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Parse(p.SmokeA), 0), new(Parse(p.SmokeB), 0.54), new(Parse(p.SmokeC), 1)
            }, new Point(0, 0), new Point(1, 1));
        Application.Current.Resources["EmberGlow"] = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.18, 0.08), Center = new Point(0.18, 0.08), RadiusX = 0.85, RadiusY = 0.85,
            GradientStops = new GradientStopCollection
            {
                new(WithAlpha(accent, 0x14), 0), new(Colors.Transparent, 1)
            }
        };

        Application.Current.Resources["AppFontSize"] = 13d * Math.Clamp(fontScale, 0.85, 1.35);
        Application.Current.Resources["SmallFontSize"] = 12d * Math.Clamp(fontScale, 0.85, 1.35);
        Application.Current.Resources["ControlPad"] = density.Equals("compact", StringComparison.OrdinalIgnoreCase)
            ? new Thickness(12, 7, 12, 7)
            : density.Equals("spacious", StringComparison.OrdinalIgnoreCase)
                ? new Thickness(18, 10, 18, 10)
                : new Thickness(14, 8, 14, 8);
    }

    private static void SetBrush(string key, string hex) => SetBrush(key, Parse(hex));
    private static void SetBrush(string key, Color color) =>
        Application.Current.Resources[key] = new SolidColorBrush(color);

    private static void SetColor(string key, string hex) => SetColor(key, Parse(hex));
    private static void SetColor(string key, Color color) => Application.Current.Resources[key] = color;
    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
    private static Color Blend(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Mix(byte x, byte y) => (byte)Math.Round(x + ((y - x) * amount));
        return Color.FromArgb(255, Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B));
    }
}
