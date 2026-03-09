namespace SoundRadar.Analysis;

public class SurroundAnalyzer
{
    // Default 7.1 channel layout: (buffer index, angle in degrees)
    // LFE (index 3) is excluded — not directional
    private static readonly (int Index, float AngleDeg)[] DefaultChannelMap =
    {
        (0, -45f),   // FL
        (1, 45f),    // FR
        (2, 0f),     // FC
        (4, -135f),  // RL
        (5, 135f),   // RR
        (6, -90f),   // SL
        (7, 90f),    // SR
    };

    private static readonly Dictionary<string, int> NameToIndex = new()
    {
        ["FL"] = 0, ["FR"] = 1, ["FC"] = 2,
        ["RL"] = 4, ["RR"] = 5, ["SL"] = 6, ["SR"] = 7,
    };

    private readonly (int Index, float AngleDeg)[] _channelMap;

    public float[] LastChannelEnergies { get; private set; } = new float[8];
    public float LastAngle { get; private set; }
    public float LastIntensity { get; private set; }

    public SurroundAnalyzer(Dictionary<string, float>? channelAngles = null)
    {
        if (channelAngles != null)
        {
            _channelMap = channelAngles
                .Where(kv => NameToIndex.ContainsKey(kv.Key))
                .Select(kv => (NameToIndex[kv.Key], kv.Value))
                .ToArray();
        }
        else
        {
            _channelMap = DefaultChannelMap;
        }
    }

    /// <summary>
    /// Analyzes an 8-channel interleaved buffer and returns the weighted direction angle.
    /// Returns null if total directional energy is below threshold (silence or LFE-only).
    /// </summary>
    public (float Angle, float Intensity)? Analyze(float[] samples, int channelCount)
    {
        if (channelCount < 8) return null;

        int frameCount = samples.Length / channelCount;
        if (frameCount == 0) return null;

        // Compute RMS per channel
        var rms = new float[channelCount];
        for (int ch = 0; ch < channelCount; ch++)
        {
            double sumSq = 0;
            for (int frame = 0; frame < frameCount; frame++)
            {
                float s = samples[frame * channelCount + ch];
                sumSq += s * s;
            }
            rms[ch] = (float)Math.Sqrt(sumSq / frameCount);
        }

        LastChannelEnergies = rms;

        // Weighted barycenter across directional channels (skip LFE)
        double sumX = 0, sumY = 0;
        float totalEnergy = 0;

        foreach (var (index, angleDeg) in _channelMap)
        {
            if (index >= rms.Length) continue;
            float energy = rms[index];
            double angleRad = angleDeg * Math.PI / 180;
            sumX += energy * Math.Cos(angleRad);
            sumY += energy * Math.Sin(angleRad);
            totalEnergy += energy;
        }

        if (totalEnergy < 0.001f)
            return null;

        float resultAngle = (float)(Math.Atan2(sumY, sumX) * 180 / Math.PI);

        // Intensity: RMS across directional channels
        double energySumSq = 0;
        foreach (var (index, _) in _channelMap)
        {
            if (index < rms.Length)
                energySumSq += rms[index] * rms[index];
        }
        float intensity = (float)Math.Sqrt(energySumSq / _channelMap.Length);

        LastAngle = resultAngle;
        LastIntensity = Math.Clamp(intensity, 0f, 1f);

        return (resultAngle, LastIntensity);
    }

    /// <summary>
    /// Downmixes a multichannel buffer to stereo (FL + FR).
    /// </summary>
    public static float[] DownmixToStereo(float[] samples, int channelCount)
    {
        if (channelCount <= 2) return samples;

        int frames = samples.Length / channelCount;
        var stereo = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            stereo[i * 2] = samples[i * channelCount];       // FL
            stereo[i * 2 + 1] = samples[i * channelCount + 1]; // FR
        }
        return stereo;
    }
}
