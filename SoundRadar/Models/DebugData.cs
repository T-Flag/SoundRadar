namespace SoundRadar.Models;

public class DebugData
{
    // Audio capture info
    public int SampleRate { get; set; }
    public int BufferSize { get; set; }
    public bool CaptureActive { get; set; }

    // Raw signal
    public float RawPan { get; set; }
    public float NormalizedPan { get; set; }
    public float RawIntensity { get; set; }
    public float MaxExpectedPan { get; set; }

    // Adaptive threshold
    public double BaselineAvg { get; set; }
    public double TriggerLevel { get; set; }
    public double TriggerFactor { get; set; }
    public int ActiveEvents { get; set; }

    // Performance
    public double FrameTimeMs { get; set; }
    public int EventsPerSec { get; set; }
}

public class SoundLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Band { get; init; } = "";
    public float Pan { get; init; }
    public float Intensity { get; init; }

    public string Direction => Pan < -0.1f ? "L" : Pan > 0.1f ? "R" : "C";

    public override string ToString()
    {
        string time = Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        string dir = Direction;
        string panStr = Pan >= 0 ? $"+{Pan:F2}" : $"{Pan:F2}";
        return $"{time}  {Band,-8} {dir} {panStr}  int:{Intensity:F2}";
    }
}
