using System.Collections.Concurrent;
using SoundRadar.Models;

namespace SoundRadar.Analysis;

public class AdaptiveThreshold
{
    private readonly double _adaptationTimeSec;
    private readonly double _triggerFactor;
    private readonly ConcurrentDictionary<string, double> _averages = new();

    public int ProcessCallCount { get; private set; }

    public AdaptiveThreshold(double adaptationTimeSec = 3.0, double triggerFactor = 1.5)
    {
        _adaptationTimeSec = adaptationTimeSec;
        _triggerFactor = triggerFactor;
    }

    public double TriggerFactor => _triggerFactor;

    public double GetAverage(string bandName)
    {
        return _averages.TryGetValue(bandName, out var avg) ? avg : 0;
    }

    /// <summary>
    /// Processes band analysis results and returns bands that exceed their adaptive threshold.
    /// </summary>
    public List<BandAnalysis> Process(BandAnalysis[] bands, double frameDurationSec)
    {
        ProcessCallCount++;
        double alpha = 1.0 - Math.Exp(-frameDurationSec / _adaptationTimeSec);
        var triggered = new List<BandAnalysis>();

        foreach (var band in bands)
        {
            // TryAdd returns true if first time (just initialized) — skip threshold check
            if (_averages.TryAdd(band.Name, band.Energy))
                continue;

            double avg = _averages[band.Name];
            double threshold = avg * _triggerFactor;

            bool isSpike = band.Energy > threshold && band.Energy > 1e-10;

            if (isSpike)
            {
                triggered.Add(band);
                // Do NOT update average with spike values
            }
            else
            {
                // Update EMA only with non-spike values
                _averages[band.Name] = avg + alpha * (band.Energy - avg);
            }
        }

        return triggered;
    }
}
