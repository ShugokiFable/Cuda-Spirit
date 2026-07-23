using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Ai;
using CudaSpirit.Core.Services.Settings;

namespace CudaSpirit.App.Infra;

/// <summary>
/// One shared advisor conversation for the whole app. The main "AI Advisor" tab and the movable
/// advisor overlay both render THIS collection, so a message asked in either place appears in both -
/// a single synced chat. Streaming updates each message in place.
/// </summary>
public sealed class AdvisorConversation
{
    private readonly AdvisorService _advisor;
    private readonly SettingsService _settings;

    public ObservableCollection<ChatEntry> Messages { get; } = new();
    public bool IsBusy { get; private set; }

    /// <summary>Raised on every streamed delta and on busy changes (for auto-scroll / button state).</summary>
    public event Action? Updated;

    /// <summary>The default "what to do next" prompt so the overlay button is one tap.</summary>
    public const string PlanPrompt =
        "Give me the 5 highest-impact things to do next right now, ranked most important first, " +
        "based on my current build in the context. One short line each, imperative, max 14 words, " +
        "with a brief reason in parentheses. No intro, no closing remarks.";

    public AdvisorConversation(AdvisorService advisor, SettingsService settings)
    {
        _advisor = advisor;
        _settings = settings;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_settings.Current.OpenRouterApiKey);

    public async Task SendAsync(string prompt, bool showUser = true, string? displayAs = null)
    {
        prompt = prompt.Trim();
        if (prompt.Length == 0 || IsBusy) return;
        if (!HasApiKey)
        {
            Messages.Add(ChatEntry.Ai("Add your OpenRouter API key in Settings → OpenRouter to chat."));
            Updated?.Invoke();
            return;
        }

        SetBusy(true);
        if (showUser) Messages.Add(ChatEntry.User(displayAs ?? prompt));
        var entry = ChatEntry.Ai("");
        Messages.Add(entry);
        Updated?.Invoke();

        try
        {
            var raw = new StringBuilder();
            await foreach (var delta in _advisor.StreamAsync(prompt, AiTaskKind.Reasoning))
            {
                raw.Append(delta);
                entry.Text = CleanMarkdown(raw.ToString());
                Updated?.Invoke();
            }
            if (entry.Text.Length == 0) entry.Text = "(no answer - try again)";
        }
        catch (Exception ex)
        {
            entry.Text = entry.Text.Length > 0 ? entry.Text + "\n\n⚠ " + ex.Message : "⚠ " + ex.Message;
        }
        finally { SetBusy(false); }
    }

    /// <summary>Vision: send a screenshot + prompt (the "read my screen" button).</summary>
    public async Task SendWithImageAsync(string prompt, string imageDataUrl)
    {
        if (IsBusy) return;
        if (!HasApiKey) { Messages.Add(ChatEntry.Ai("Add an API key in Settings first.")); Updated?.Invoke(); return; }

        SetBusy(true);
        Messages.Add(ChatEntry.User("📷 " + (string.IsNullOrWhiteSpace(prompt) ? "Read my screen" : prompt)));
        var entry = ChatEntry.Ai("Reading the screen…");
        Messages.Add(entry);
        Updated?.Invoke();
        try
        {
            var answer = await _advisor.AskWithImagesAsync(
                string.IsNullOrWhiteSpace(prompt)
                    ? "Read this Black Desert screen - identify gear/crystals/stats and give concise next-step advice."
                    : prompt,
                new[] { imageDataUrl });
            entry.Text = CleanMarkdown(answer);
        }
        catch (Exception ex) { entry.Text = "⚠ " + ex.Message; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) { IsBusy = busy; Updated?.Invoke(); }

    private static string CleanMarkdown(string s)
    {
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
        s = Regex.Replace(s, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "$1");
        s = Regex.Replace(s, @"__(.+?)__", "$1");
        s = Regex.Replace(s, @"`([^`]+)`", "$1");
        s = Regex.Replace(s, @"(?m)^\s{0,3}#{1,6}\s*", "");
        s = Regex.Replace(s, @"(?m)^(\s*)[-*]\s+", "$1• ");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }
}

/// <summary>One chat bubble; mutable Text with change notification so streamed deltas update live.</summary>
public sealed class ChatEntry : INotifyPropertyChanged
{
    private string _text = "";
    public string Text
    {
        get => _text;
        set { _text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); }
    }

    public HorizontalAlignment Align { get; init; }
    public Brush Bubble { get; init; } = Brushes.Transparent;
    public Brush Edge { get; init; } = Brushes.Transparent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static ChatEntry User(string text) => new()
    {
        Text = text,
        Align = HorizontalAlignment.Right,
        Bubble = new SolidColorBrush(Color.FromArgb(0x40, 0xE0, 0x1E, 0x37)),
        Edge = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0x1E, 0x37))
    };

    public static ChatEntry Ai(string text) => new()
    {
        Text = text,
        Align = HorizontalAlignment.Left,
        Bubble = new SolidColorBrush(Color.FromArgb(0xE6, 0x1B, 0x16, 0x26)),
        Edge = new SolidColorBrush(Color.FromArgb(0x59, 0x8B, 0x4F, 0xE0))
    };
}
