using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Slyth.App.ViewModels;

namespace Slyth.App.Views;

public partial class OptimizeView : UserControl
{
    public OptimizeView() => InitializeComponent();

    /// <summary>Clicar em qualquer lugar da linha alterna o interruptor.</summary>
    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<CheckBox>(source) is null &&
            sender is FrameworkElement { DataContext: TweakItemViewModel item })
        {
            item.IsSelected = !item.IsSelected;
        }
    }

    private static T? FindParent<T>(DependencyObject? node)
        where T : DependencyObject
    {
        while (node is not null and not T)
        {
            node = node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }

        return node as T;
    }
}
