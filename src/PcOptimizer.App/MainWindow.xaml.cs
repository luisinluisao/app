using System.Windows;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ((MainViewModel)DataContext).LoadAsync();
    }
}
