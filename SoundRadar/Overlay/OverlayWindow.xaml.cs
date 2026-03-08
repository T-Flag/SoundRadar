using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private const double RadiusFraction = 0.4;       // radar radius as fraction of min(width,height)
    private const double ArcSpanDegrees = 30.0;      // angular width of each arc
    private const double ArcMaxThickness = 0.20;     // max thickness as fraction of radius
    private const double ArcMinThickness = 0.04;     // min thickness as fraction of radius
    private const double GlowRadius = 12.0;

    // --- Win32 ---
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int GWL_EXSTYLE = -20;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_F9 = 0x78;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly ConcurrentQueue<SoundEvent> _events = new();
    private readonly DispatcherTimer _renderTimer;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private DirectionAnalyzer? _analyzer;
    private TextBlock? _thresholdLabel;
    private DispatcherTimer? _labelFadeTimer;
    private bool _overlayVisible = true;

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
        var hwnd = new WindowInteropHelper(this).Handle;
        int extStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT);

        _hookProc = HookCallback;
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule.ModuleName), 0);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hookId != IntPtr.Zero)
            UnhookWindowsHookEx(_hookId);
        base.OnClosed(e);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (vkCode == VK_F9)
            {
                Dispatcher.Invoke(ToggleOverlay);
            }
            else if (ctrl && vkCode == 0xBB && _analyzer != null) // Ctrl+=
            {
                Dispatcher.Invoke(() =>
                {
                    _analyzer.IntensityThreshold *= 1.5f;
                    ShowThresholdLabel();
                });
            }
            else if (ctrl && vkCode == 0xBD && _analyzer != null) // Ctrl+-
            {
                Dispatcher.Invoke(() =>
                {
                    _analyzer.IntensityThreshold /= 1.5f;
                    ShowThresholdLabel();
                });
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
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

        // Color selection
        bool isLeft = evt.Pan < -CenterPanThreshold;
        bool isRight = evt.Pan > CenterPanThreshold;
        Color baseColor = isLeft ? ColorLeft : isRight ? ColorRight : ColorCenter;

        // Opacity: intensity * decay, boosted for visibility, reduced for center
        byte opacity = (byte)(255 * Math.Min(1f, evt.Intensity * 2f) * decay);
        if (!isLeft && !isRight) opacity = (byte)(opacity * 0.5);

        // Thickness proportional to intensity and decay
        double thicknessFrac = ArcMinThickness + (ArcMaxThickness - ArcMinThickness) * evt.Intensity * decay;
        double outerRadius = radius;
        double innerRadius = radius * (1.0 - thicknessFrac);

        // Angular position from PanToAngle: 0°=top, negative=left, positive=right
        // In WPF drawing coords: 0° is right (+X), so we offset by -90° to make 0° = top
        double angleDeg = DirectionAnalyzer.PanToAngle(evt.Pan);
        double halfSpan = ArcSpanDegrees / 2;
        double startAngleDeg = angleDeg - 90 - halfSpan;
        double endAngleDeg = angleDeg - 90 + halfSpan;

        double startRad = startAngleDeg * Math.PI / 180;
        double endRad = endAngleDeg * Math.PI / 180;

        // Four corner points of the arc wedge
        var outerStart = new Point(cx + outerRadius * Math.Cos(startRad), cy + outerRadius * Math.Sin(startRad));
        var outerEnd = new Point(cx + outerRadius * Math.Cos(endRad), cy + outerRadius * Math.Sin(endRad));
        var innerEnd = new Point(cx + innerRadius * Math.Cos(endRad), cy + innerRadius * Math.Sin(endRad));
        var innerStart = new Point(cx + innerRadius * Math.Cos(startRad), cy + innerRadius * Math.Sin(startRad));

        // Build crescent path
        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerRadius, outerRadius), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new ArcSegment(innerStart, new Size(innerRadius, innerRadius), 0, false, SweepDirection.Counterclockwise, true));

        var color = Color.FromArgb(opacity, baseColor.R, baseColor.G, baseColor.B);

        // Glow layer (blurred, wider)
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

        // Sharp layer on top
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
