using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Slyth.App.Controls;

/// <summary>
/// Anel de pontuação desenhado à mão: marcações de relógio, trilho escuro e arco branco animado.
/// Durante a otimização mostra o progresso e um "cometa" girando.
/// </summary>
public sealed class ScoreRing : FrameworkElement
{
    // Declaradas primeiro: os callbacks das propriedades públicas animam estas.
    private static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        "DisplayValue", typeof(double), typeof(ScoreRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty DisplayProgressProperty = DependencyProperty.Register(
        "DisplayProgress", typeof(double), typeof(ScoreRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty SpinProperty = DependencyProperty.Register(
        "Spin", typeof(double), typeof(ScoreRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ScoreRing), new PropertyMetadata(0d, (d, e) => ((ScoreRing)d).AnimateTo(DisplayValueProperty, (double)e.NewValue / 100, 1.4)));

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ScoreRing), new PropertyMetadata(0d, (d, e) => ((ScoreRing)d).AnimateTo(DisplayProgressProperty, (double)e.NewValue, 0.5)));

    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy), typeof(bool), typeof(ScoreRing), new PropertyMetadata(false, (d, _) => ((ScoreRing)d).UpdateSpin()));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ScoreRing), new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush TrackBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16)));
    private static readonly Brush TickBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24)));
    private static readonly Brush TickMajorBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)));
    private static readonly Brush CometBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush ArcBrush = Frozen(new LinearGradientBrush(
        [new GradientStop(Colors.White, 0), new GradientStop(Color.FromRgb(0xB8, 0xB8, 0xB8), 1)], new Point(0, 0), new Point(1, 1)));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = size / 2 - Thickness / 2 - 2;

        // Marcações estilo relógio por dentro do anel.
        var tickOuter = radius - Thickness - 6;
        for (var i = 0; i < 60; i++)
        {
            var major = i % 5 == 0;
            var angle = i * 6 * Math.PI / 180;
            var inner = tickOuter - (major ? 7 : 4);
            var pen = new Pen(major ? TickMajorBrush : TickBrush, major ? 1.6 : 1);
            dc.DrawLine(pen, PointAt(center, inner, angle), PointAt(center, tickOuter, angle));
        }

        dc.DrawEllipse(null, new Pen(TrackBrush, Thickness), center, radius, radius);

        var busy = IsBusy;
        var fraction = Math.Clamp(busy ? (double)GetValue(DisplayProgressProperty) : (double)GetValue(DisplayValueProperty), 0, 1);
        var arcPen = new Pen(ArcBrush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        if (fraction >= 0.999)
        {
            dc.DrawEllipse(null, arcPen, center, radius, radius);
        }
        else if (fraction > 0.002)
        {
            dc.DrawGeometry(null, arcPen, Arc(center, radius, 0, fraction * 360));
            var head = PointAt(center, radius, fraction * 2 * Math.PI);
            dc.DrawEllipse(Brushes.Black, null, head, Thickness * 0.18, Thickness * 0.18);
        }

        if (busy)
        {
            var start = (double)GetValue(SpinProperty);
            var cometPen = new Pen(CometBrush, Thickness * 0.35) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, cometPen, Arc(center, radius + Thickness * 1.3, start, 70));
            dc.DrawEllipse(Brushes.White, null, PointAt(center, radius + Thickness * 1.3, (start + 70) * Math.PI / 180), Thickness * 0.3, Thickness * 0.3);
        }
    }

    private void AnimateTo(DependencyProperty property, double to, double seconds) =>
        BeginAnimation(property, new DoubleAnimation(to, TimeSpan.FromSeconds(seconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

    private void UpdateSpin()
    {
        if (IsBusy)
        {
            SetValue(DisplayProgressProperty, 0d);
            BeginAnimation(SpinProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.6)) { RepeatBehavior = RepeatBehavior.Forever });
        }
        else
        {
            BeginAnimation(SpinProperty, null);
        }

        InvalidateVisual();
    }

    /// <summary>Ângulo em radianos, 0 = topo, sentido horário.</summary>
    private static Point PointAt(Point c, double r, double angle) => new(c.X + r * Math.Sin(angle), c.Y - r * Math.Cos(angle));

    private static StreamGeometry Arc(Point c, double r, double startDegrees, double sweepDegrees)
    {
        var start = PointAt(c, r, startDegrees * Math.PI / 180);
        var end = PointAt(c, r, (startDegrees + sweepDegrees) * Math.PI / 180);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, sweepDegrees > 180, SweepDirection.Clockwise, true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
