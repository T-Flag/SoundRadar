namespace SoundRadar.Analysis;

public static class SelfSoundFilter
{
    /// <summary>
    /// Returns true if the sound event should be filtered (ignored) as a self-sound.
    /// Only applies in surround mode — front-center sounds within filterAngle are filtered.
    /// </summary>
    public static bool ShouldFilter(float angle, bool isSurround, bool filterEnabled, float filterAngle)
    {
        if (!filterEnabled || !isSurround) return false;
        // Normalize to [-180, 180]
        float a = ((angle % 360) + 360) % 360;
        if (a > 180) a -= 360;
        return Math.Abs(a) <= filterAngle;
    }
}
