using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
    private static readonly Color ColorRear = (Color)ColorConverter.ConvertFromString("#FF00FF");
    private const float CenterPanThreshold = 0.1f;

    // --- Spectrum band colors ---
    private static readonly Color ColorSubBass = (Color)ColorConverter.ConvertFromString("#FF4444");
    private static readonly Color ColorLowMid = (Color)ColorConverter.ConvertFromString("#FFAA00");
    private static readonly Color ColorMid = (Color)ColorConverter.ConvertFromString("#44FF44");
    private static readonly Color ColorHighMid = (Color)ColorConverter.ConvertFromString("#4488FF");

    // --- Opacity ---
    private const double MinOpacity = 0.6;
    private const double MaxOpacity = 1.0;
    private const double CenterOpacityMultiplier = 0.6;

    // --- Radar geometry ---
    private const double RadiusFraction = 0.4;
    private const double ArcSpanDegrees = 30.0;
    private const double ArcMaxThickness = 0.20;
    private const double ArcMinThickness = 0.04;
    private const double GlowRadius = 18.0;

    // --- Spectrum display ---
    private const double SpectrumBarWidth = 20;
    private const double SpectrumBarMaxHeight = 80;
    private const double SpectrumMargin = 10;

    // --- Debug panel ---
    private static readonly FontFamily MonoFont = new("Consolas");
    private const double DebugFontSize = 11.5;
    private static readonly SolidColorBrush DebugFg = new(Color.FromArgb(220, 255, 255, 255));
    private static readonly SolidColorBrush DebugBg = new(Color.FromArgb(217, 0, 0, 0));

    // --- Win32 click-through & always-on-top ---
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    // SetWindowPos constants
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    // --- Global hotkeys via RegisterHotKey ---
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_TOGGLE = 1;
    private const int HOTKEY_SENS_UP = 2;
    private const int HOTKEY_SENS_DOWN = 3;
    private const int HOTKEY_QUIT = 4;
    private const int HOTKEY_PAN_RANGE_UP = 5;
    private const int HOTKEY_PAN_RANGE_DOWN = 6;
    private const int HOTKEY_SPECTRUM = 7;
    private const int HOTKEY_DEBUG = 8;
    private const int HOTKEY_ADAPT_TIME = 9;
    private const int HOTKEY_NOISE_FLOOR = 10;
    private const uint MOD_CTRL_SHIFT = 0x0002 | 0x0004;
    private const uint VK_O = 0x4F;
    private const uint VK_UP = 0x26;
    private const uint VK_DOWN = 0x28;
    private const uint VK_LEFT = 0x25;
    private const uint VK_RIGHT = 0x27;
    private const uint VK_Q = 0x51;
    private const uint VK_S = 0x53;
    private const uint VK_D = 0x44;
    private const uint VK_A = 0x41;
    private const uint VK_N = 0x4E;

    // Cycle values for hotkeys
    private static readonly double[] AdaptTimeValues = { 0.5, 1.5, 3.0 };
    private static readonly double[] NoiseFloorValues = { -60, -40, -20 };

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private readonly ConcurrentQueue<SoundEvent> _events = new();
    private readonly DispatcherTimer _renderTimer;
    private DirectionAnalyzer? _analyzer;
    private AdaptiveThreshold? _adaptiveThreshold;
    private FrequencyBandFilter? _bandFilter;
    private AppConfig? _config;
    private bool _overlayVisible = true;
    private bool _spectrumVisible = false;
    private bool _debugVisible = true;
    private bool _isSurroundMode = false;
    private IntPtr _hwnd;
    private BandAnalysis[]? _currentBands;
    private DebugData? _debugData;

    // Event log
    private readonly List<SoundLogEntry> _eventLog = new();
    private const int MaxEventLogEntries = 5;

    // Performance tracking
    private readonly Stopwatch _frameStopwatch = Stopwatch.StartNew();
    private double _lastFrameTimeMs;
    private int _eventCounter;
    private int _eventsPerSec;
    private DateTime _lastEventCountReset = DateTime.UtcNow;

    // Setting highlight flash
    private string? _highlightSetting;
    private DateTime _highlightUntil;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Deactivated += (_, _) => ForceTopmost();

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += OnRenderTick;
        _renderTimer.Start();

        var topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        topmostTimer.Tick += (_, _) => ForceTopmost();
        topmostTimer.Start();
    }

    public void SetAnalyzer(DirectionAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public void SetAdaptiveThreshold(AdaptiveThreshold threshold)
    {
        _adaptiveThreshold = threshold;
    }

    public void SetBandFilter(FrequencyBandFilter filter)
    {
        _bandFilter = filter;
    }

    public void SetConfig(AppConfig config)
    {
        _config = config;
        _spectrumVisible = config.SpectrumDisplayVisible;
        _debugVisible = config.DebugVisible;
    }

    public void SetSurroundMode(bool surround)
    {
        _isSurroundMode = surround;
    }

    public void SetOverlayVisible(bool visible)
    {
        _overlayVisible = visible;
        OverlayCanvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateSpectrum(BandAnalysis[] bands)
    {
        _currentBands = bands;
    }

    public void UpdateDebugData(DebugData data)
    {
        _debugData = data;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        int extStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW);
        ForceTopmost();

        RegisterHotKey(_hwnd, HOTKEY_TOGGLE, MOD_CTRL_SHIFT, VK_O);
        RegisterHotKey(_hwnd, HOTKEY_SENS_UP, MOD_CTRL_SHIFT, VK_UP);
        RegisterHotKey(_hwnd, HOTKEY_SENS_DOWN, MOD_CTRL_SHIFT, VK_DOWN);
        RegisterHotKey(_hwnd, HOTKEY_QUIT, MOD_CTRL_SHIFT, VK_Q);
        RegisterHotKey(_hwnd, HOTKEY_PAN_RANGE_UP, MOD_CTRL_SHIFT, VK_RIGHT);
        RegisterHotKey(_hwnd, HOTKEY_PAN_RANGE_DOWN, MOD_CTRL_SHIFT, VK_LEFT);
        RegisterHotKey(_hwnd, HOTKEY_SPECTRUM, MOD_CTRL_SHIFT, VK_S);
        RegisterHotKey(_hwnd, HOTKEY_DEBUG, MOD_CTRL_SHIFT, VK_D);
        RegisterHotKey(_hwnd, HOTKEY_ADAPT_TIME, MOD_CTRL_SHIFT, VK_A);
        RegisterHotKey(_hwnd, HOTKEY_NOISE_FLOOR, MOD_CTRL_SHIFT, VK_N);

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
    }

    private void ForceTopmost()
    {
        if (_hwnd == IntPtr.Zero) return;
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void FlashSetting(string settingName)
    {
        _highlightSetting = settingName;
        _highlightUntil = DateTime.UtcNow.AddSeconds(1);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _analyzer != null)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case HOTKEY_TOGGLE:
                    ToggleOverlay();
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_SENS_UP:
                    _analyzer.IntensityThreshold /= 1.5f;
                    FlashSetting("Sensitivity");
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_SENS_DOWN:
                    _analyzer.IntensityThreshold *= 1.5f;
                    FlashSetting("Sensitivity");
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_PAN_RANGE_UP:
                    _analyzer.MaxExpectedPan += 0.05f;
                    FlashSetting("MaxExpectedPan");
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_PAN_RANGE_DOWN:
                    _analyzer.MaxExpectedPan -= 0.05f;
                    FlashSetting("MaxExpectedPan");
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_SPECTRUM:
                    _spectrumVisible = !_spectrumVisible;
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_DEBUG:
                    _debugVisible = !_debugVisible;
                    SaveConfig();
                    handled = true;
                    break;
                case HOTKEY_ADAPT_TIME:
                    CycleAdaptTime();
                    handled = true;
                    break;
                case HOTKEY_NOISE_FLOOR:
                    CycleNoiseFloor();
                    handled = true;
                    break;
                case HOTKEY_QUIT:
                    SaveConfig();
                    Application.Current.Shutdown();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private void CycleAdaptTime()
    {
        if (_adaptiveThreshold == null || _config == null) return;
        double current = _adaptiveThreshold.AdaptationTimeSec;
        int idx = Array.IndexOf(AdaptTimeValues, current);
        int next = (idx + 1) % AdaptTimeValues.Length;
        _adaptiveThreshold.AdaptationTimeSec = AdaptTimeValues[next];
        _config.AdaptiveThreshold.AdaptationTimeSec = AdaptTimeValues[next];
        FlashSetting("AdaptTime");
        SaveConfig();
    }

    private void CycleNoiseFloor()
    {
        if (_bandFilter == null || _config == null) return;
        double current = _bandFilter.NoiseFloorDb;
        int idx = Array.IndexOf(NoiseFloorValues, current);
        int next = (idx + 1) % NoiseFloorValues.Length;
        _bandFilter.NoiseFloorDb = NoiseFloorValues[next];
        _config.FrequencyBands.NoiseFloorDb = NoiseFloorValues[next];
        FlashSetting("NoiseFloor");
        SaveConfig();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_TOGGLE);
            UnregisterHotKey(_hwnd, HOTKEY_SENS_UP);
            UnregisterHotKey(_hwnd, HOTKEY_SENS_DOWN);
            UnregisterHotKey(_hwnd, HOTKEY_QUIT);
            UnregisterHotKey(_hwnd, HOTKEY_PAN_RANGE_UP);
            UnregisterHotKey(_hwnd, HOTKEY_PAN_RANGE_DOWN);
            UnregisterHotKey(_hwnd, HOTKEY_SPECTRUM);
            UnregisterHotKey(_hwnd, HOTKEY_DEBUG);
            UnregisterHotKey(_hwnd, HOTKEY_ADAPT_TIME);
            UnregisterHotKey(_hwnd, HOTKEY_NOISE_FLOOR);
        }
        base.OnClosed(e);
    }

    private void ToggleOverlay()
    {
        _overlayVisible = !_overlayVisible;
        OverlayCanvas.Visibility = _overlayVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveConfig()
    {
        if (_config == null || _analyzer == null) return;
        _config.IntensityThreshold = _analyzer.IntensityThreshold;
        _config.MaxExpectedPan = _analyzer.MaxExpectedPan;
        _config.OverlayVisible = _overlayVisible;
        _config.SpectrumDisplayVisible = _spectrumVisible;
        _config.DebugVisible = _debugVisible;
        _config.Save();
    }

    public void PushEvent(SoundEvent soundEvent)
    {
        _events.Enqueue(soundEvent);
        _eventCounter++;

        if (soundEvent.DominantBand != null)
        {
            _eventLog.Add(new SoundLogEntry
            {
                Timestamp = soundEvent.Timestamp,
                Band = soundEvent.DominantBand,
                Pan = soundEvent.Pan,
                Angle = soundEvent.Angle,
                IsSurround = soundEvent.IsSurround,
                Intensity = soundEvent.Intensity,
            });
            while (_eventLog.Count > MaxEventLogEntries)
                _eventLog.RemoveAt(0);
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        double elapsed = _frameStopwatch.Elapsed.TotalMilliseconds;
        _lastFrameTimeMs = elapsed;
        _frameStopwatch.Restart();

        if ((DateTime.UtcNow - _lastEventCountReset).TotalSeconds >= 1.0)
        {
            _eventsPerSec = _eventCounter;
            _eventCounter = 0;
            _lastEventCountReset = DateTime.UtcNow;
        }

        // Clear highlight if expired
        if (_highlightSetting != null && DateTime.UtcNow > _highlightUntil)
            _highlightSetting = null;

        OverlayCanvas.Children.Clear();

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

        // Draw compass labels in surround mode
        if (_isSurroundMode)
            DrawCompassLabels(centerX, centerY, radius);

        foreach (var evt in active)
        {
            _events.Enqueue(evt);
            DrawRadarArc(evt, centerX, centerY, radius);
        }

        if (_debugVisible)
        {
            foreach (var evt in active)
            {
                if (evt.DominantBand != null)
                    DrawArcLabel(evt, centerX, centerY, radius);
            }

            if (_debugData != null && Math.Abs(_debugData.RawIntensity) > 0.001f)
                DrawRawPanIndicator(centerX, centerY, radius);
        }

        if (_spectrumVisible && _currentBands != null)
            DrawSpectrumBars(width, height);

        if (_debugVisible)
        {
            DrawDebugPanel(width, height, active.Count);
            DrawControlsPanel(width, height);
            DrawEventLog(width, height);
        }
    }

    private void DrawCompassLabels(double cx, double cy, double radius)
    {
        // F(front) at top, B(back) at bottom, L at left, R at right
        var labels = new[] { ("F", 0.0), ("R", 90.0), ("B", 180.0), ("L", -90.0) };
        double labelRadius = radius + 15;
        byte alpha = 90;

        foreach (var (text, angleDeg) in labels)
        {
            double angleRad = (angleDeg - 90) * Math.PI / 180;
            double lx = cx + labelRadius * Math.Cos(angleRad);
            double ly = cy + labelRadius * Math.Sin(angleRad);

            var label = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                FontFamily = MonoFont,
                Foreground = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255)),
            };
            Canvas.SetLeft(label, lx - 5);
            Canvas.SetTop(label, ly - 9);
            OverlayCanvas.Children.Add(label);
        }
    }

    private void DrawRadarArc(SoundEvent evt, double cx, double cy, double radius)
    {
        float decay = evt.GetDecayFactor();
        if (decay <= 0) return;

        Color baseColor;
        double angleDeg;

        if (evt.IsSurround)
        {
            // Surround mode: use angle directly, color from angle
            angleDeg = evt.Angle;
            baseColor = AngleToColor(evt.Angle);
        }
        else
        {
            // Stereo mode: use PanToAngle, color from pan direction
            bool isLeft = evt.Pan < -CenterPanThreshold;
            bool isRight = evt.Pan > CenterPanThreshold;
            baseColor = isLeft ? ColorLeft : isRight ? ColorRight : ColorCenter;
            angleDeg = DirectionAnalyzer.PanToAngle(evt.Pan);
        }

        double rawOpacity = MinOpacity + (MaxOpacity - MinOpacity) * Math.Min(1.0, evt.Intensity * 2.0);
        rawOpacity *= decay;
        if (!evt.IsSurround && Math.Abs(evt.Pan) <= CenterPanThreshold)
            rawOpacity *= CenterOpacityMultiplier;
        byte opacity = (byte)(255 * Math.Clamp(rawOpacity, 0, 1));

        double thicknessFrac = ArcMinThickness + (ArcMaxThickness - ArcMinThickness) * evt.Intensity * decay;
        double outerRadius = radius;
        double innerRadius = radius * (1.0 - thicknessFrac);

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

        var sharpColor = Color.FromArgb((byte)(opacity * 0.85), baseColor.R, baseColor.G, baseColor.B);
        var sharpPath = new Path
        {
            Data = new PathGeometry(new[] { sharpFigure }),
            Fill = new SolidColorBrush(sharpColor),
        };
        OverlayCanvas.Children.Add(sharpPath);
    }

    private void DrawArcLabel(SoundEvent evt, double cx, double cy, double radius)
    {
        float decay = evt.GetDecayFactor();
        if (decay <= 0.1f) return;

        double angleDeg = evt.IsSurround ? evt.Angle : DirectionAnalyzer.PanToAngle(evt.Pan);
        double angleRad = (angleDeg - 90) * Math.PI / 180;
        double labelRadius = radius + 25;
        double lx = cx + labelRadius * Math.Cos(angleRad);
        double ly = cy + labelRadius * Math.Sin(angleRad);

        byte labelAlpha = (byte)(200 * decay);
        var label = new TextBlock
        {
            Text = $"{evt.DominantBand} {evt.Intensity:F2}",
            FontSize = 10,
            FontFamily = MonoFont,
            Foreground = new SolidColorBrush(Color.FromArgb(labelAlpha, 255, 255, 255)),
            Background = new SolidColorBrush(Color.FromArgb((byte)(80 * decay), 0, 0, 0)),
            Padding = new Thickness(3, 1, 3, 1),
        };
        Canvas.SetLeft(label, lx - 30);
        Canvas.SetTop(label, ly - 8);
        OverlayCanvas.Children.Add(label);
    }

    private void DrawRawPanIndicator(double cx, double cy, double radius)
    {
        if (_debugData == null) return;

        double rawAngleDeg;
        if (_isSurroundMode)
            rawAngleDeg = _debugData.SurroundAngle;
        else
            rawAngleDeg = DirectionAnalyzer.PanToAngle(_debugData.RawPan);

        double rawAngleRad = (rawAngleDeg - 90) * Math.PI / 180;

        double innerR = radius * 0.85;
        double outerR = radius * 1.05;

        var line = new Line
        {
            X1 = cx + innerR * Math.Cos(rawAngleRad),
            Y1 = cy + innerR * Math.Sin(rawAngleRad),
            X2 = cx + outerR * Math.Cos(rawAngleRad),
            Y2 = cy + outerR * Math.Sin(rawAngleRad),
            Stroke = new SolidColorBrush(Color.FromArgb(100, 180, 180, 180)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
        };
        OverlayCanvas.Children.Add(line);
    }

    private void DrawDebugPanel(double width, double height, int activeEvents)
    {
        var data = _debugData;
        string captureStatus = data?.CaptureActive == true ? "Active" : "Inactive";
        int sampleRate = data?.SampleRate ?? 0;
        int bufferSize = data?.BufferSize ?? 0;

        string rawPanStr = data != null ? FormatSigned(data.RawPan) : "+0.000";
        string normPanStr = data != null ? FormatSigned(data.NormalizedPan) : "+0.000";
        string rawIntStr = data != null ? $"{data.RawIntensity:F3}" : "0.000";

        string bandsSection = BuildBandsSection();

        double triggerFactor = data?.TriggerFactor ?? 1.5;
        double adaptTime = _adaptiveThreshold?.AdaptationTimeSec ?? 0.5;
        double noiseFloor = _bandFilter?.NoiseFloorDb ?? -60;

        // Settings section with highlight support
        bool hlSens = _highlightSetting == "Sensitivity";
        bool hlPan = _highlightSetting == "MaxExpectedPan";
        bool hlAdapt = _highlightSetting == "AdaptTime";
        bool hlNoise = _highlightSetting == "NoiseFloor";
        string sensVal = _analyzer != null ? $"{_analyzer.IntensityThreshold:F3}" : "0.010";
        string panVal = _analyzer != null ? $"{_analyzer.MaxExpectedPan:F2}" : "0.25";

        var sb = new StringBuilder();
        sb.AppendLine("=== SOUND RADAR DEBUG ===");
        sb.AppendLine($"Audio Capture: {captureStatus} | {sampleRate} Hz | Buffer: {bufferSize}");
        sb.AppendLine();

        // Surround mode section
        if (_isSurroundMode && data != null)
        {
            sb.AppendLine("-- Surround Mode --");
            string modeStr = $"7.1 ({data.ChannelCount} channels)";
            string angleStr = data.SurroundAngle >= 0 ? $"+{data.SurroundAngle:F1}°" : $"{data.SurroundAngle:F1}°";
            string dirStr = AngleToDirectionName(data.SurroundAngle);
            sb.AppendLine($"Mode:             {modeStr}");
            sb.AppendLine($"Angle:            {angleStr}  ({dirStr})");
            sb.AppendLine();
            sb.AppendLine("-- Channel Energy --");
            sb.Append(BuildChannelEnergySection(data.ChannelEnergies));
            sb.AppendLine();
        }

        sb.AppendLine("-- Raw Signal --");
        sb.AppendLine($"Pan (raw):        {rawPanStr}");
        sb.AppendLine($"Pan (normalized): {normPanStr}");
        sb.AppendLine($"Intensity (raw):   {rawIntStr}");
        sb.AppendLine();
        sb.AppendLine("-- Frequency Bands --");
        sb.Append(bandsSection);
        sb.AppendLine();
        sb.AppendLine("-- Settings --");
        sb.AppendLine($"{(hlSens ? ">>>" : "   ")} Sensitivity:     {sensVal}  [Ctrl+Shift+Up/Down]");
        sb.AppendLine($"{(hlPan ? ">>>" : "   ")} MaxExpectedPan:  {panVal}  [Ctrl+Shift+Left/Right]");
        sb.AppendLine($"    Trigger factor:  {triggerFactor:F1}");
        sb.AppendLine($"{(hlAdapt ? ">>>" : "   ")} Adapt time:     {adaptTime:F1}s  [Ctrl+Shift+A]");
        sb.AppendLine($"{(hlNoise ? ">>>" : "   ")} Noise floor:    {noiseFloor:F0}dB  [Ctrl+Shift+N]");
        sb.AppendLine();
        sb.AppendLine("-- Performance --");
        sb.AppendLine($"Frame time:        {_lastFrameTimeMs:F1}ms");
        sb.AppendLine($"Active events:     {activeEvents}");
        sb.AppendLine($"Events/sec:        {_eventsPerSec}");
        sb.Append($"FFT updates:       {_adaptiveThreshold?.ProcessCallCount ?? 0}");

        var panel = CreateDebugTextBlock(sb.ToString());
        Canvas.SetLeft(panel, 15);
        Canvas.SetTop(panel, 15);
        OverlayCanvas.Children.Add(panel);
    }

    private static string BuildChannelEnergySection(float[] energies)
    {
        if (energies.Length < 8)
            return "  (no data)\n";

        var sb = new StringBuilder();
        // FL(0), FR(1) on same line; SL(6), SR(7); RL(4), RR(5); FC(2), LFE(3)
        sb.AppendLine(FormatChannelPair("FL", energies[0], "FR", energies[1]));
        sb.AppendLine(FormatChannelPair("SL", energies[6], "SR", energies[7]));
        sb.AppendLine(FormatChannelPair("RL", energies[4], "RR", energies[5]));
        sb.AppendLine(FormatChannelPair("FC", energies[2], "LFE", energies[3]));
        return sb.ToString();
    }

    private static string FormatChannelPair(string name1, float energy1, string name2, float energy2)
    {
        return $"{name1,-3}: {EnergyBar(energy1)}  {energy1:F2}   {name2,-3}: {EnergyBar(energy2)}  {energy2:F2}";
    }

    private static string EnergyBar(float energy, int width = 8)
    {
        int filled = (int)(Math.Clamp(energy, 0, 1) * width);
        int empty = width - filled;
        return new string('\u2588', filled) + new string('\u2591', empty);
    }

    private string BuildBandsSection()
    {
        var bands = _currentBands;
        if (bands == null || bands.Length == 0)
            return "  (no data)\n";

        string[] names = { "SubBass", "LowMid", "Mid", "HighMid" };
        string[] ranges = { "[20-80Hz]", "[80-400Hz]", "[400-1800Hz]", "[1800-6000Hz]" };
        var sb = new StringBuilder();

        for (int i = 0; i < bands.Length && i < 4; i++)
        {
            double energy = bands[i].Energy; // already normalized [0,1]
            int filled = (int)(energy * 10);
            int empty = 10 - filled;
            string bar = new string('\u2588', filled) + new string('\u2591', empty);
            string panStr = FormatSigned(bands[i].Pan, 2);

            // Per-band baseline and trigger from AdaptiveThreshold
            double bandBase = _adaptiveThreshold?.GetAverage(names[i]) ?? 0;
            double bandTrig = bandBase * (_adaptiveThreshold?.TriggerFactor ?? 1.5);

            sb.AppendLine($"{names[i],-8} {ranges[i],-14}: {bar}  {energy:F2} (raw:{bands[i].RawEnergy:F4})  pan:{panStr}  base:{bandBase:F4}  trig:{bandTrig:F4}");
        }
        return sb.ToString();
    }

    private void DrawControlsPanel(double width, double height)
    {
        string controlsText =
            "=== CONTROLS ===\n" +
            "Ctrl+Shift+D      Toggle debug\n" +
            "Ctrl+Shift+S      Toggle spectrum\n" +
            "Ctrl+Shift+O      Toggle overlay\n" +
            "Ctrl+Shift+Up     Sensitivity +\n" +
            "Ctrl+Shift+Down   Sensitivity -\n" +
            "Ctrl+Shift+Right  MaxPan +0.05\n" +
            "Ctrl+Shift+Left   MaxPan -0.05\n" +
            "Ctrl+Shift+A      Adapt time cycle\n" +
            "Ctrl+Shift+N      Noise floor cycle\n" +
            "Ctrl+Shift+Q      Quit";

        var panel = CreateDebugTextBlock(controlsText);
        Canvas.SetLeft(panel, 15);
        Canvas.SetBottom(panel, 15);
        OverlayCanvas.Children.Add(panel);
    }

    private void DrawEventLog(double width, double height)
    {
        if (_eventLog.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== EVENT LOG ===");

        for (int i = _eventLog.Count - 1; i >= 0; i--)
            sb.AppendLine(_eventLog[i].ToString());

        var panel = CreateDebugTextBlock(sb.ToString().TrimEnd());
        Canvas.SetRight(panel, 15);
        Canvas.SetBottom(panel, 15);
        OverlayCanvas.Children.Add(panel);
    }

    private Border CreateDebugTextBlock(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = DebugFontSize,
            FontFamily = MonoFont,
            Foreground = DebugFg,
            Padding = new Thickness(10, 8, 10, 8),
        };

        return new Border
        {
            Background = DebugBg,
            CornerRadius = new CornerRadius(4),
            Child = tb,
        };
    }

    private static string FormatSigned(float value, int decimals = 3)
    {
        string format = decimals == 2 ? "F2" : "F3";
        return value >= 0 ? $"+{value.ToString(format)}" : value.ToString(format);
    }

    /// <summary>
    /// Maps a surround angle (-180..+180) to a color.
    /// Transition: cyan(left/-90) -> white(front/0) -> orange(right/+90) -> magenta(back/180) -> cyan
    /// </summary>
    private static Color AngleToColor(float angleDeg)
    {
        // Normalize to [0, 360)
        float a = ((angleDeg % 360) + 360) % 360;

        // Sectors: 0=front, 90=right, 180=rear, 270=left
        if (a < 90)
            return LerpColor(ColorCenter, ColorRight, (float)(a / 90));
        if (a < 180)
            return LerpColor(ColorRight, ColorRear, (float)((a - 90) / 90));
        if (a < 270)
            return LerpColor(ColorRear, ColorLeft, (float)((a - 180) / 90));
        return LerpColor(ColorLeft, ColorCenter, (float)((a - 270) / 90));
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static string AngleToDirectionName(float angle)
    {
        float a = ((angle % 360) + 360) % 360;
        if (a < 22.5 || a >= 337.5) return "Front";
        if (a < 67.5) return "Front-Right";
        if (a < 112.5) return "Right";
        if (a < 157.5) return "Rear-Right";
        if (a < 202.5) return "Rear";
        if (a < 247.5) return "Rear-Left";
        if (a < 292.5) return "Left";
        return "Front-Left";
    }

    private void DrawSpectrumBars(double width, double height)
    {
        var bands = _currentBands;
        if (bands == null || bands.Length == 0) return;

        Color[] bandColors = { ColorSubBass, ColorLowMid, ColorMid, ColorHighMid };
        string[] bandLabels = { "SB", "LM", "M", "HM" };

        double totalWidth = bands.Length * (SpectrumBarWidth + SpectrumMargin) - SpectrumMargin;
        double startX = width - totalWidth - 30;
        double baseY = height - 120;

        if (_debugVisible && _eventLog.Count > 0)
            baseY -= 140;

        var bg = new Rectangle
        {
            Width = totalWidth + 20,
            Height = SpectrumBarMaxHeight + 30,
            Fill = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            RadiusX = 5,
            RadiusY = 5,
        };
        Canvas.SetLeft(bg, startX - 10);
        Canvas.SetTop(bg, baseY - SpectrumBarMaxHeight - 10);
        OverlayCanvas.Children.Add(bg);

        for (int i = 0; i < bands.Length && i < 4; i++)
        {
            double energy = bands[i].Energy; // already normalized [0,1]
            double barHeight = energy * SpectrumBarMaxHeight;
            double x = startX + i * (SpectrumBarWidth + SpectrumMargin);

            var barColor = i < bandColors.Length ? bandColors[i] : Colors.White;

            var bar = new Rectangle
            {
                Width = SpectrumBarWidth,
                Height = barHeight,
                Fill = new SolidColorBrush(Color.FromArgb(200, barColor.R, barColor.G, barColor.B)),
                RadiusX = 2,
                RadiusY = 2,
            };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, baseY - barHeight);
            OverlayCanvas.Children.Add(bar);

            var label = new TextBlock
            {
                Text = i < bandLabels.Length ? bandLabels[i] : "?",
                FontSize = 10,
                FontFamily = MonoFont,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                TextAlignment = TextAlignment.Center,
                Width = SpectrumBarWidth,
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, baseY + 2);
            OverlayCanvas.Children.Add(label);
        }
    }
}
