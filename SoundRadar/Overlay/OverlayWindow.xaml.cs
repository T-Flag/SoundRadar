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

    // --- Arc geometry ---
    private const double ArcVerticalSpan = 0.35;   // portion of screen height
    private const double ArcMaxThickness = 40.0;    // max thickness in pixels at full intensity
    private const double ArcMinThickness = 6.0;     // min thickness
    private const double ArcEdgeMargin = 8.0;       // pixels from screen edge
    private const double GlowRadius = 15.0;

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
        // Preserve threshold label across clears
        bool hasLabel = _thresholdLabel != null && OverlayCanvas.Children.Contains(_thresholdLabel);
        OverlayCanvas.Children.Clear();
        if (hasLabel)
            OverlayCanvas.Children.Add(_thresholdLabel!);

        if (!_overlayVisible) return;

        double width = ActualWidth;
        double height = ActualHeight;

        var active = new List<SoundEvent>();
        while (_events.TryDequeue(out var evt))
        {
            if (!evt.IsExpired)
                active.Add(evt);
        }

        foreach (var evt in active)
        {
            _events.Enqueue(evt);
            DrawEdgeArc(evt, width, height);
        }
    }

    private void DrawEdgeArc(SoundEvent evt, double screenWidth, double screenHeight)
    {
        float decay = evt.GetDecayFactor();
        if (decay <= 0) return;

        float absPan = Math.Abs(evt.Pan);
        bool isLeft = evt.Pan < -CenterPanThreshold;
        bool isRight = evt.Pan > CenterPanThreshold;
        bool isCenter = !isLeft && !isRight;

        // Pick color
        Color baseColor = isLeft ? ColorLeft : isRight ? ColorRight : ColorCenter;

        // Opacity based on intensity and decay
        byte opacity = (byte)(255 * Math.Min(1f, evt.Intensity * 2f) * decay);
        if (isCenter) opacity = (byte)(opacity * 0.5); // reduced opacity for center

        // Arc thickness based on intensity and decay
        double thickness = ArcMinThickness + (ArcMaxThickness - ArcMinThickness) * evt.Intensity * decay;

        // Vertical position: pan ±1.0 = middle of edge, pan closer to 0 = higher (toward "front")
        // Map absPan to vertical center of the arc: 0 = top quarter, 1 = center
        double verticalCenter = screenHeight * (0.2 + 0.3 * absPan);

        double arcHeight = screenHeight * ArcVerticalSpan * (0.5 + 0.5 * evt.Intensity);

        double top = verticalCenter - arcHeight / 2;
        double bottom = verticalCenter + arcHeight / 2;

        // Clamp to screen
        top = Math.Max(0, top);
        bottom = Math.Min(screenHeight, bottom);

        if (isCenter)
        {
            // Draw arcs on both sides for center sounds
            DrawSingleEdgeArc(true, top, bottom, thickness, baseColor, opacity, screenWidth, decay);
            DrawSingleEdgeArc(false, top, bottom, thickness, baseColor, opacity, screenWidth, decay);
        }
        else
        {
            DrawSingleEdgeArc(isLeft, top, bottom, thickness, baseColor, opacity, screenWidth, decay);
        }
    }

    private void DrawSingleEdgeArc(bool leftSide, double top, double bottom, double thickness,
        Color baseColor, byte opacity, double screenWidth, float decay)
    {
        double arcHeight = bottom - top;
        double midY = (top + bottom) / 2;

        // X position: on the edge of the screen
        double edgeX = leftSide ? ArcEdgeMargin : screenWidth - ArcEdgeMargin;

        // Build a crescent arc using a bezier curve
        double curveDepth = thickness * 1.5;
        double innerDepth = curveDepth - thickness;

        double xDir = leftSide ? 1 : -1;

        // Outer curve points
        var pTopOuter = new Point(edgeX, top);
        var pBottomOuter = new Point(edgeX, bottom);
        var pMidOuter = new Point(edgeX + xDir * curveDepth, midY);

        // Inner curve points
        var pTopInner = new Point(edgeX, top);
        var pBottomInner = new Point(edgeX, bottom);
        var pMidInner = new Point(edgeX + xDir * innerDepth, midY);

        // Build path: outer curve down, then inner curve back up
        var figure = new PathFigure { StartPoint = pTopOuter, IsClosed = true, IsFilled = true };

        // Outer curve (top → bottom via mid)
        figure.Segments.Add(new QuadraticBezierSegment(pMidOuter, pBottomOuter, true));

        // Inner curve (bottom → top via mid) — closing the crescent
        figure.Segments.Add(new QuadraticBezierSegment(pMidInner, pTopInner, true));

        var color = Color.FromArgb(opacity, baseColor.R, baseColor.G, baseColor.B);
        var glowColor = Color.FromArgb((byte)(opacity * 0.4), baseColor.R, baseColor.G, baseColor.B);

        var path = new Path
        {
            Data = new PathGeometry(new[] { figure }),
            Fill = new SolidColorBrush(color),
            Effect = new BlurEffect
            {
                Radius = GlowRadius * decay,
                KernelType = KernelType.Gaussian
            }
        };

        OverlayCanvas.Children.Add(path);

        // Draw a sharper inner arc on top for crispness
        var sharpFigure = new PathFigure { StartPoint = pTopOuter, IsClosed = true, IsFilled = true };
        sharpFigure.Segments.Add(new QuadraticBezierSegment(pMidOuter, pBottomOuter, true));
        sharpFigure.Segments.Add(new QuadraticBezierSegment(pMidInner, pTopInner, true));

        var sharpColor = Color.FromArgb((byte)(opacity * 0.8), baseColor.R, baseColor.G, baseColor.B);
        var sharpPath = new Path
        {
            Data = new PathGeometry(new[] { sharpFigure }),
            Fill = new SolidColorBrush(sharpColor),
        };

        OverlayCanvas.Children.Add(sharpPath);
    }
}
