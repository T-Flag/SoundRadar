namespace SoundRadar.Models;

public class SoundEvent
{
    public float Pan { get; init; }
    public float Intensity { get; init; }
    public float DominantFrequency { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan DisplayDuration { get; init; } = TimeSpan.FromMilliseconds(500);

    public bool IsExpired => DateTime.UtcNow - Timestamp > DisplayDuration;

    public float GetDecayFactor()
    {
        var elapsed = (float)(DateTime.UtcNow - Timestamp).TotalMilliseconds;
        var total = (float)DisplayDuration.TotalMilliseconds;
        var factor = 1f - (elapsed / total);
        return Math.Clamp(factor, 0f, 1f);
    }
}
