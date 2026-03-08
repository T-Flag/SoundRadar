using SoundRadar.Models;

namespace SoundRadar.Analysis;

public class DirectionAnalyzer
{
    private float _intensityThreshold;
    private float _maxExpectedPan;

    public event Action<SoundEvent>? SoundDetected;

    public float IntensityThreshold
    {
        get => _intensityThreshold;
        set => _intensityThreshold = Math.Clamp(value, 0.001f, 1f);
    }

    public float MaxExpectedPan
    {
        get => _maxExpectedPan;
        set => _maxExpectedPan = Math.Clamp(value, 0.1f, 1f);
    }

    public DirectionAnalyzer(float intensityThreshold = 0.01f, float maxExpectedPan = 0.75f)
    {
        _intensityThreshold = intensityThreshold;
        _maxExpectedPan = maxExpectedPan;
    }

    public static float NormalizePan(float rawPan, float maxExpectedPan)
    {
        return Math.Clamp(rawPan / maxExpectedPan, -1f, 1f);
    }

    /// <summary>
    /// Maps pan value (-1..+1) to angle in degrees (-90..+90).
    /// Uses an exponential curve to accentuate extreme values.
    /// </summary>
    public static float PanToAngle(float pan)
    {
        float sign = Math.Sign(pan);
        float abs = Math.Abs(pan);
        float curved = (float)Math.Pow(abs, 0.7);
        return sign * curved * 90f;
    }

    public void ProcessBuffer(float[] samples, int sampleRate)
    {
        int frameCount = samples.Length / 2;
        if (frameCount == 0) return;

        double leftSumSq = 0;
        double rightSumSq = 0;

        for (int i = 0; i < frameCount; i++)
        {
            float l = samples[i * 2];
            float r = samples[i * 2 + 1];
            leftSumSq += l * l;
            rightSumSq += r * r;
        }

        float leftRms = (float)Math.Sqrt(leftSumSq / frameCount);
        float rightRms = (float)Math.Sqrt(rightSumSq / frameCount);
        float totalRms = (float)Math.Sqrt((leftSumSq + rightSumSq) / (2 * frameCount));

        if (totalRms < _intensityThreshold)
            return;

        float pan = 0f;
        float sum = leftRms + rightRms;
        if (sum > 0)
            pan = (rightRms - leftRms) / sum;

        float intensity = Math.Clamp(totalRms, 0f, 1f);

        float normalizedPan = NormalizePan(pan, _maxExpectedPan);

        SoundDetected?.Invoke(new SoundEvent
        {
            Pan = normalizedPan,
            Intensity = intensity,
            DominantFrequency = 0f,
            Timestamp = DateTime.UtcNow
        });
    }
}
