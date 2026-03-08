namespace SoundRadar.Models;

public class BandAnalysis
{
    public string Name { get; init; } = "";
    public double Energy { get; init; }
    public double RawEnergy { get; init; }
    public float Pan { get; init; }
    public float Intensity { get; init; }
}
