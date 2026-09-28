using System.Windows;
using System.Windows.Threading;

namespace Slyth.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Ocorreu um erro inesperado:\n{e.Exception.Message}", "Slyth Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
