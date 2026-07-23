using System.Windows;
using System.Windows.Threading;
using CudaSpirit.App.Infra;

namespace CudaSpirit.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Surface unhandled exceptions instead of a silent crash.
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                MessageBox.Show(ex.Message, "Cuda Spirit - fatal error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Cuda Spirit - error",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ServiceHub.Instance.Dispose();
        base.OnExit(e);
    }
}
