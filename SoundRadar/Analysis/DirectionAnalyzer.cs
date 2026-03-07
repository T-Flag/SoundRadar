using SoundRadar.Models;

namespace SoundRadar.Analysis;

public class DirectionAnalyzer
{
    private readonly float _intensityThreshold;

    public event Action<SoundEvent>? SoundDetected;

    public DirectionAnalyzer(float intensityThreshold = 0.05f)
    {
        _intensityThreshold = intensityThreshold;
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

        SoundDetected?.Invoke(new SoundEvent
        {
            Pan = Math.Clamp(pan, -1f, 1f),
            Intensity = intensity,
            DominantFrequency = 0f,
            Timestamp = DateTime.UtcNow
        });
    }
}
