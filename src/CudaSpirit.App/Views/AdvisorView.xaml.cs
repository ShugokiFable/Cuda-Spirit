using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using Microsoft.Win32;

namespace CudaSpirit.App.Views;

/// <summary>
/// The AI Advisor tab. It hosts the shared <see cref="AdvisorChat"/> (same conversation as the
/// movable overlay) plus a vision "read my screen" button.
/// </summary>
public partial class AdvisorView : UserControl, IRefreshable
{
    private readonly AdvisorConversation _conv = ServiceHub.Instance.Conversation;

    public AdvisorView()
    {
        InitializeComponent();
    }

    public void Refresh() { /* the shared chat keeps its own state */ }

    private async void OnVision(object sender, RoutedEventArgs e)
    {
        if (!_conv.HasApiKey)
        {
            MessageBox.Show("Add an OpenRouter API key in Settings first.", "AI Advisor",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string dataUrl;
        var dlg = new OpenFileDialog
        {
            Title = "Choose a screenshot (Cancel to capture your screen)",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp"
        };
        if (dlg.ShowDialog() == true)
            dataUrl = ScreenCapture.FileToDataUrl(dlg.FileName);
        else
            dataUrl = ScreenCapture.CaptureVirtualScreenDataUrl();

        await _conv.SendWithImageAsync("", dataUrl);
    }
}
