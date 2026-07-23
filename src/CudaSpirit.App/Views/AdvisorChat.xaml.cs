using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CudaSpirit.App.Infra;

namespace CudaSpirit.App.Views;

/// <summary>
/// Reusable advisor chat. Both the AI Advisor tab and the movable overlay host this control and bind
/// to the one shared <see cref="AdvisorConversation"/>, so the conversation is synced across them.
/// </summary>
public partial class AdvisorChat : UserControl
{
    private readonly AdvisorConversation _conv = ServiceHub.Instance.Conversation;

    public AdvisorChat()
    {
        InitializeComponent();
        Chat.ItemsSource = _conv.Messages;
        Loaded += (_, _) => { _conv.Updated += OnConvUpdated; UpdateButtons(); ScrollToEnd(); };
        Unloaded += (_, _) => _conv.Updated -= OnConvUpdated;
    }

    private void OnConvUpdated()
    {
        // Streaming resumes on the UI thread, but guard just in case.
        if (Dispatcher.CheckAccess()) { UpdateButtons(); ScrollToEnd(); }
        else Dispatcher.Invoke(() => { UpdateButtons(); ScrollToEnd(); });
    }

    private void UpdateButtons()
    {
        SendBtn.IsEnabled = !_conv.IsBusy;
        QuickBtn.IsEnabled = !_conv.IsBusy;
    }

    private void ScrollToEnd() => Scroll.ScrollToEnd();

    private async void OnSend(object sender, RoutedEventArgs e) => await SendInputAsync();

    private async void OnInputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_conv.IsBusy)
        {
            e.Handled = true;
            await SendInputAsync();
        }
    }

    private async Task SendInputAsync()
    {
        var text = Input.Text.Trim();
        if (text.Length == 0 || _conv.IsBusy) return;
        Input.Clear();
        await _conv.SendAsync(text);
    }

    private async void OnQuick(object sender, RoutedEventArgs e)
    {
        if (_conv.IsBusy) return;
        await _conv.SendAsync(AdvisorConversation.PlanPrompt, showUser: true, displayAs: "What should I do next?");
    }
}
