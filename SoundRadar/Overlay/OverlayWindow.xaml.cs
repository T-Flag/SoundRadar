using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SoundRadar.Analysis;
using SoundRadar.Models;

namespace SoundRadar.Overlay;

public partial class OverlayWindow : Window
{
    // --- Configurable colors ---
    private static readonly Color ColorLeft = (Color)ColorConverter.ConvertFromString("#00D4FF");
    private static readonly Color ColorRight = (Color)ColorConverter.ConvertFromString("#FF8C00");
    private static readonly Color ColorCenter = Colors.White;
    private const float CenterPanThreshold = 0.1f;

    // --- Radar geometry ---
    private const double RadiusFraction = 0.4;
    private const double ArcSpanDegrees = 30.0;
    private const double ArcMaxThickness = 0.20;
    private const double ArcMinThickness = 0.04;
    private const double GlowRadius = 12.0;

    // --- Win32 click-through ---
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int GWL_EXSTYLE = -20;

    // --- Global hotkeys via RegisterHotKey ---
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_TOGGLE = 1;
    private const int HOTKEY_SENS_UP = 2;
    private const int HOTKEY_SENS_DOWN = 3;
    private const int HOTKEY_QUIT = 4;
    private const uint MOD_CTRL_SHIFT = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    private const uint VK_O = 0x4F;
    private const uint VK_UP = 0x26;
    private const uint VK_DOWN = 0x28;
    private const uint VK_Q = 0x51;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly ConcurrentQueue<SoundEvent> _events = new();
    private readonly DispatcherTimer _renderTimer;
    private DirectionAnalyzer? _analyzer;
    private TextBlock? _thresholdLabel;
    private DispatcherTimer? _labelFadeTimer;
    private bool _overlayVisible = true;
    private IntPtr _hwnd;

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

    public void SetAnalyzer(DirectionAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // Click-through
        int extStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT);

        // Register global hotkeys
        RegisterHotKey(_hwnd, HOTKEY_TOGGLE, MOD_CTRL_SHIFT, VK_O);
        RegisterHotKey(_hwnd, HOTKEY_SENS_UP, MOD_CTRL_SHIFT, VK_UP);
        RegisterHotKey(_hwnd, HOTKEY_SENS_DOWN, MOD_CTRL_SHIFT, VK_DOWN);
        RegisterHotKey(_hwnd, HOTKEY_QUIT, MOD_CTRL_SHIFT, VK_Q);

        // Listen for WM_HOTKEY messages
        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case HOTKEY_TOGGLE:
                    ToggleOverlay();
                    handled = true;
                    break;
                case HOTKEY_SENS_UP when _analyzer != null:
                    _analyzer.IntensityThreshold /= 1.5f; // lower threshold = more sensitive
                    ShowThresholdLabel();
                    handled = true;
                    break;
                case HOTKEY_SENS_DOWN when _analyzer != null:
                    _analyzer.IntensityThreshold *= 1.5f; // higher threshold = less sensitive
                    ShowThresholdLabel();
                    handled = true;
                    break;
                case HOTKEY_QUIT:
                    Application.Current.Shutdown();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_TOGGLE);
            UnregisterHotKey(_hwnd, HOTKEY_SENS_UP);
            UnregisterHotKey(_hwnd, HOTKEY_SENS_DOWN);
            UnregisterHotKey(_hwnd, HOTKEY_QUIT);
        }
        base.OnClosed(e);
    }

    private void ToggleOverlay()
    {
        _overlayVisible = !_overlayVisible;
        OverlayCanvas.Visibility = _overlayVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowThresholdLabel()
    {
        if (_analyzer == null) return;

        if (_thresholdLabel == null)
        {
            _thresholdLabel = new TextBlock
            {
                FontSize = 18,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                Padding = new Thickness(10, 5, 10, 5),
            };
            Canvas.SetRight(_thresholdLabel, 20);
            Canvas.SetBottom(_thresholdLabel, 20);
        }

        _thresholdLabel.Text = $"Seuil : {_analyzer.IntensityThreshold:F3}";

        if (!OverlayCanvas.Children.Contains(_thresholdLabel))
            OverlayCanvas.Children.Add(_thresholdLabel);

        _labelFadeTimer?.Stop();
        _labelFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _labelFadeTimer.Tick += (_, _) =>
        {
            OverlayCanvas.Children.Remove(_thresholdLabel);
            _labelFadeTimer.Stop();
        };
        _labelFadeTimer.Start();
    }

    public void PushEvent(SoundEvent soundEvent)
    {
        _events.Enqueue(soundEvent);
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        bool hasLabel = _thresholdLabel != null && OverlayCanvas.Children.Contains(_thresholdLabel);
        OverlayCanvas.Children.Clear();
        if (hasLabel)
            OverlayCanvas.Children.Add(_thresholdLabel!);

        if (!_overlayVisible) return;

        double width = ActualWidth;
        double height = ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) * RadiusFraction;

        var active = new List<SoundEvent>();
        while (_events.TryDequeue(out var evt))
        {
            if (!evt.IsExpired)
                active.Add(evt);
        }

        foreach (var evt in active)
        {
            _events.Enqueue(evt);
            DrawRadarArc(evt, centerX, centerY, radius);
        }
    }

    private void DrawRadarArc(SoundEvent evt, double cx, double cy, double radius)
    {
        float decay = evt.GetDecayFactor();
        if (decay <= 0) return;

        bool isLeft = evt.Pan < -CenterPanThreshold;
        bool isRight = evt.Pan > CenterPanThreshold;
        Color baseColor = isLeft ? ColorLeft : isRight ? ColorRight : ColorCenter;

        byte opacity = (byte)(255 * Math.Min(1f, evt.Intensity * 2f) * decay);
        if (!isLeft && !isRight) opacity = (byte)(opacity * 0.5);

        double thicknessFrac = ArcMinThickness + (ArcMaxThickness - ArcMinThickness) * evt.Intensity * decay;
        double outerRadius = radius;
        double innerRadius = radius * (1.0 - thicknessFrac);

        double angleDeg = DirectionAnalyzer.PanToAngle(evt.Pan);
        double halfSpan = ArcSpanDegrees / 2;
        double startAngleDeg = angleDeg - 90 - halfSpan;
        double endAngleDeg = angleDeg - 90 + halfSpan;

        double startRad = startAngleDeg * Math.PI / 180;
        double endRad = endAngleDeg * Math.PI / 180;

        var outerStart = new Point(cx + outerRadius * Math.Cos(startRad), cy + outerRadius * Math.Sin(startRad));
        var outerEnd = new Point(cx + outerRadius * Math.Cos(endRad), cy + outerRadius * Math.Sin(endRad));
        var innerEnd = new Point(cx + innerRadius * Math.Cos(endRad), cy + innerRadius * Math.Sin(endRad));
        var innerStart = new Point(cx + innerRadius * Math.Cos(startRad), cy + innerRadius * Math.Sin(startRad));

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new ArcSegment(innerStart, new Size(innerRadius, innerRadius), 0, false, SweepDirection.Counterclockwise, true));

        var color = Color.FromArgb(opacity, baseColor.R, baseColor.G, baseColor.B);

        var glowPath = new Path
        {
            Data = new PathGeometry(new[] { figure }),
            Fill = new SolidColorBrush(color),
            Effect = new BlurEffect
            {
                Radius = GlowRadius * decay,
                KernelType = KernelType.Gaussian
            }
        };
        OverlayCanvas.Children.Add(glowPath);

        var sharpFigure = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        sharpFigure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0, false, SweepDirection.Clockwise, true));
        sharpFigure.Segments.Add(new LineSegment(innerEnd, true));
        sharpFigure.Segments.Add(new ArcSegment(innerStart, new Size(innerRadius, innerRadius), 0, false, SweepDirection.Counterclockwise, true));

        var sharpColor = Color.FromArgb((byte)(opacity * 0.8), baseColor.R, baseColor.G, baseColor.B);
        var sharpPath = new Path
        {
            Data = new PathGeometry(new[] { sharpFigure }),
            Fill = new SolidColorBrush(sharpColor),
        };
        OverlayCanvas.Children.Add(sharpPath);
    }
}
