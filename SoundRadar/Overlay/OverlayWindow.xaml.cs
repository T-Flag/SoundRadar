using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SoundRadar.Models;

namespace SoundRadar.Overlay;

public partial class OverlayWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private readonly ConcurrentQueue<SoundEvent> _events = new();
    private readonly DispatcherTimer _renderTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int extStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT);
    }

    public void PushEvent(SoundEvent soundEvent)
    {
        _events.Enqueue(soundEvent);
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        OverlayCanvas.Children.Clear();

        double width = ActualWidth;
        double height = ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) * 0.4;

        var active = new List<SoundEvent>();
        while (_events.TryDequeue(out var evt))
        {
            if (!evt.IsExpired)
                active.Add(evt);
        }

        foreach (var evt in active)
        {
            _events.Enqueue(evt);
            DrawArc(evt, centerX, centerY, radius);
        }
    }

    private void DrawArc(SoundEvent evt, double cx, double cy, double radius)
    {
        float decay = evt.GetDecayFactor();
        if (decay <= 0) return;

        double angle = evt.Pan * 80; // -80° to +80° from top
        double angleRad = (angle - 90) * Math.PI / 180;

        double arcSpan = 30;
        double startAngle = angle - 90 - arcSpan / 2;
        double endAngle = angle - 90 + arcSpan / 2;

        double innerRadius = radius * 0.85;
        double outerRadius = radius;

        var startRad = startAngle * Math.PI / 180;
        var endRad = endAngle * Math.PI / 180;

        var p1 = new Point(cx + innerRadius * Math.Cos(startRad), cy + innerRadius * Math.Sin(startRad));
        var p2 = new Point(cx + outerRadius * Math.Cos(startRad), cy + outerRadius * Math.Sin(startRad));
        var p3 = new Point(cx + outerRadius * Math.Cos(endRad), cy + outerRadius * Math.Sin(endRad));
        var p4 = new Point(cx + innerRadius * Math.Cos(endRad), cy + innerRadius * Math.Sin(endRad));

        var figure = new PathFigure { StartPoint = p1, IsClosed = true };
        figure.Segments.Add(new LineSegment(p2, true));
        figure.Segments.Add(new ArcSegment(p3, new Size(outerRadius, outerRadius), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(p4, true));
        figure.Segments.Add(new ArcSegment(p1, new Size(innerRadius, innerRadius), 0, false, SweepDirection.Counterclockwise, true));

        byte opacity = (byte)(255 * evt.Intensity * decay);
        var color = evt.Pan < 0
            ? Color.FromArgb(opacity, 0, 180, 255)   // Blue for left
            : Color.FromArgb(opacity, 255, 100, 0);   // Orange for right

        var path = new Path
        {
            Data = new PathGeometry(new[] { figure }),
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Color.FromArgb((byte)(opacity * 0.5), 255, 255, 255)),
            StrokeThickness = 1
        };

        OverlayCanvas.Children.Add(path);
    }
}
