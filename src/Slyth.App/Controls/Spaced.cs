using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Slyth.App.Controls;

/// <summary>
/// O WPF não tem letter-spacing; este atalho insere espaços finos entre as letras
/// para os rótulos em caixa alta: &lt;TextBlock ui:Spaced.Text="PONTUAÇÃO" /&gt;.
/// </summary>
public static class Spaced
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Spaced), new PropertyMetadata(null, (d, e) =>
        {
            if (d is TextBlock tb)
            {
                tb.Text = Apply(e.NewValue as string);
            }
        }));

    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);

    public static void SetText(DependencyObject d, string value) => d.SetValue(TextProperty, value);

    public static string Apply(string? text) => string.IsNullOrEmpty(text) ? "" : string.Join(" ", text.ToUpperInvariant().ToCharArray());
}

public sealed class SpacedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Spaced.Apply(value?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
