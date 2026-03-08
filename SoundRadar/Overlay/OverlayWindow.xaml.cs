using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SoundRadar.Analysis;
using SoundRadar.Models;

namespace SoundRadar.Overlay;

public partial class OverlayWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int GWL_EXSTYLE = -20;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

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
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN && _analyzer != null)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (ctrl && vkCode == 0xBB) // Ctrl + =  (OemPlus)
            {
                Dispatcher.Invoke(() =>
                {
                    _analyzer.IntensityThreshold *= 1.5f;
                    ShowThresholdLabel();
                });
            }
            else if (ctrl && vkCode == 0xBD) // Ctrl + - (OemMinus)
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

    private void ShowThresholdLabel()
    {
        if (_analyzer == null) return;

        if (_thresholdLabel == null)
        {
            _thresholdLabel = new TextBlock
            {
                FontSize = 24,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(12, 6, 12, 6),
            };
            Canvas.SetLeft(_thresholdLabel, 20);
            Canvas.SetTop(_thresholdLabel, 20);
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

        double angle = DirectionAnalyzer.PanToAngle(evt.Pan);

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
