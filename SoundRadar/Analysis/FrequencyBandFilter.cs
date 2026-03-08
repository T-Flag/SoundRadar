using SoundRadar.Models;

namespace SoundRadar.Analysis;

public class FrequencyBandFilter
{
    private readonly (string Name, double LowHz, double HighHz)[] _bands;

    public FrequencyBandFilter(
        (double Low, double High)? subBass = null,
        (double Low, double High)? lowMid = null,
        (double Low, double High)? mid = null,
        (double Low, double High)? highMid = null)
    {
        var sb = subBass ?? (20, 80);
        var lm = lowMid ?? (80, 400);
        var m = mid ?? (400, 1800);
        var hm = highMid ?? (1800, 6000);

        _bands = new[]
        {
            ("SubBass", sb.Low, sb.High),
            ("LowMid", lm.Low, lm.High),
            ("Mid", m.Low, m.High),
            ("HighMid", hm.Low, hm.High),
        };
    }

    /// <summary>
    /// Analyzes left and right magnitude spectra and returns per-band analysis.
    /// </summary>
    public BandAnalysis[] Analyze(double[] leftMagnitudes, double[] rightMagnitudes, int sampleRate, int fftSize)
    {
        double binWidth = (double)sampleRate / fftSize;
        var results = new BandAnalysis[_bands.Length];

        for (int b = 0; b < _bands.Length; b++)
        {
            var (name, lowHz, highHz) = _bands[b];

            int startBin = Math.Max(1, (int)Math.Ceiling(lowHz / binWidth));
            int endBin = Math.Min(leftMagnitudes.Length - 1, (int)Math.Floor(highHz / binWidth));

            double leftEnergy = 0;
            double rightEnergy = 0;

            for (int i = startBin; i <= endBin; i++)
            {
                leftEnergy += leftMagnitudes[i] * leftMagnitudes[i];
                rightEnergy += rightMagnitudes[i] * rightMagnitudes[i];
            }

            double totalEnergy = leftEnergy + rightEnergy;
            float pan = 0f;
            if (totalEnergy > 1e-12)
            {
                float leftRms = (float)Math.Sqrt(leftEnergy);
                float rightRms = (float)Math.Sqrt(rightEnergy);
                float sum = leftRms + rightRms;
                if (sum > 0)
                    pan = (rightRms - leftRms) / sum;
            }

            float intensity = (float)Math.Clamp(Math.Sqrt(totalEnergy), 0, 1);

            results[b] = new BandAnalysis
            {
                Name = name,
                Energy = totalEnergy,
                Pan = pan,
                Intensity = intensity,
            };
        }

        return results;
    }
}
