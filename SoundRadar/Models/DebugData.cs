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

    // Surround
    public bool IsSurround { get; set; }
    public int ChannelCount { get; set; }
    public float SurroundAngle { get; set; }
    public float SurroundIntensity { get; set; }
    public float[] ChannelEnergies { get; set; } = Array.Empty<float>();
}

public class SoundLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Band { get; init; } = "";
    public float Pan { get; init; }
    public float Angle { get; init; }
    public bool IsSurround { get; init; }
    public float Intensity { get; init; }

    public string Direction
    {
        get
        {
            if (IsSurround)
                return AngleToDirection(Angle);
            return Pan < -0.1f ? "L" : Pan > 0.1f ? "R" : "C";
        }
    }

    public override string ToString()
    {
        string time = Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        string dir = Direction;
        if (IsSurround)
        {
            string angleStr = Angle >= 0 ? $"+{Angle:F0}°" : $"{Angle:F0}°";
            return $"{time}  {Band,-8} {dir,-3} {angleStr,5}  int:{Intensity:F2}";
        }
        string panStr = Pan >= 0 ? $"+{Pan:F2}" : $"{Pan:F2}";
        return $"{time}  {Band,-8} {dir} {panStr}  int:{Intensity:F2}";
    }

    private static string AngleToDirection(float angle)
    {
        float a = ((angle % 360) + 360) % 360;
        if (a < 22.5 || a >= 337.5) return "F";
        if (a < 67.5) return "FR";
        if (a < 112.5) return "R";
        if (a < 157.5) return "RR";
        if (a < 202.5) return "B";
        if (a < 247.5) return "RL";
        if (a < 292.5) return "L";
        return "FL";
    }
}
