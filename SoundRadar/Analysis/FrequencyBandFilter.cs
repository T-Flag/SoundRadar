using SoundRadar.Models;

namespace SoundRadar.Analysis;

public class FrequencyBandFilter
{
    private readonly (string Name, double LowHz, double HighHz)[] _bands;
    private double _noiseFloorDb;
    private readonly double _ceilingDb;

    public FrequencyBandFilter(
        (double Low, double High)? subBass = null,
        (double Low, double High)? lowMid = null,
        (double Low, double High)? mid = null,
        (double Low, double High)? highMid = null,
        double noiseFloorDb = -60,
        double ceilingDb = 0)
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
        _noiseFloorDb = noiseFloorDb;
        _ceilingDb = ceilingDb;
    }

    public double NoiseFloorDb
    {
        get => _noiseFloorDb;
        set => _noiseFloorDb = value;
    }

    /// <summary>
    /// Analyzes left and right magnitude spectra and returns per-band analysis.
    /// Energy is normalized to [0,1] using dB scale with configurable noise floor and ceiling.
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
            int binCount = 0;

            for (int i = startBin; i <= endBin; i++)
            {
                leftEnergy += leftMagnitudes[i] * leftMagnitudes[i];
                rightEnergy += rightMagnitudes[i] * rightMagnitudes[i];
                binCount++;
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

            // Normalize by (fftSize/2)² to convert FFT magnitudes to amplitude scale
            double fftScale = (fftSize / 2.0) * (fftSize / 2.0);
            double meanMagSquared = binCount > 0 ? totalEnergy / (binCount * fftScale) : 0;
            double energyDb = 10.0 * Math.Log10(meanMagSquared + 1e-10);
            double dbRange = _ceilingDb - _noiseFloorDb;
            double normalizedEnergy = dbRange > 0
                ? Math.Clamp((energyDb - _noiseFloorDb) / dbRange, 0, 1)
                : 0;

            float intensity = (float)normalizedEnergy;

            results[b] = new BandAnalysis
            {
                Name = name,
                Energy = normalizedEnergy,
                RawEnergy = meanMagSquared,
                Pan = pan,
                Intensity = intensity,
            };
        }

        return results;
    }
}
