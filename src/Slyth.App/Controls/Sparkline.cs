using System.Windows;
using System.Windows.Media;

namespace Slyth.App.Controls;

/// <summary>Minigráfico de linha com área em degradê. Os pontos vêm num espaço 100×100.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(PointCollection), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Pen LinePen = CreatePen();
    private static readonly Brush AreaBrush = CreateArea();

    public PointCollection? Points
    {
        get => (PointCollection?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var points = Points;
        if (points is null || points.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var w = ActualWidth;
        var h = ActualHeight - 4;
        Point Map(Point p) => new(p.X / 100 * w, 2 + p.Y / 100 * h);

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var l = line.Open())
        using (var a = area.Open())
        {
            l.BeginFigure(Map(points[0]), false, false);
            a.BeginFigure(new Point(0, ActualHeight), true, true);
            a.LineTo(Map(points[0]), false, false);
            for (var i = 1; i < points.Count; i++)
            {
                l.LineTo(Map(points[i]), true, true);
                a.LineTo(Map(points[i]), false, false);
            }

            a.LineTo(new Point(w, ActualHeight), false, false);
        }

        dc.DrawGeometry(AreaBrush, null, area);
        dc.DrawGeometry(null, LinePen, line);
        var last = Map(points[^1]);
        dc.DrawEllipse(Brushes.White, null, last, 2.6, 2.6);
    }

    private static Pen CreatePen()
    {
        var pen = new Pen(Brushes.White, 1.4) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }

    private static Brush CreateArea()
    {
        var brush = new LinearGradientBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF), Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 90);
        brush.Freeze();
        return brush;
    }
}
