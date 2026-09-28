using System.Windows;
using System.Windows.Interop;
using Slyth.App.Interop;
using Slyth.App.ViewModels;

namespace Slyth.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((MainViewModel)DataContext).LoadAsync();
        StateChanged += (_, _) => OnStateChanged();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Cantos arredondados e moldura escura no Windows 11 (ignorado no Windows 10).
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        var round = NativeMethods.DwmwcpRound;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaWindowCornerPreference, ref round, sizeof(int));
    }

    private void OnStateChanged()
    {
        // Janela sem borda maximizada passa um pouco da tela; compensa com margem.
        Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
