using System.Collections.Concurrent;
using SoundRadar.Models;

namespace SoundRadar.Analysis;

public class AdaptiveThreshold
{
    private double _adaptationTimeSec;
    private readonly double _triggerFactor;
    private readonly ConcurrentDictionary<string, double> _averages = new();

    public int ProcessCallCount { get; private set; }

    public AdaptiveThreshold(double adaptationTimeSec = 3.0, double triggerFactor = 1.5)
    {
        _adaptationTimeSec = adaptationTimeSec;
        _triggerFactor = triggerFactor;
    }

    public double TriggerFactor => _triggerFactor;
    public double AdaptationTimeSec
    {
        get => _adaptationTimeSec;
        set => _adaptationTimeSec = value;
    }

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
        double alphaFast = 1.0 - Math.Exp(-frameDurationSec / _adaptationTimeSec);
        double alphaSlow = alphaFast * 0.1; // 10x slower for spikes
        var triggered = new List<BandAnalysis>();

        foreach (var band in bands)
        {
            double energy = band.Energy;

            // TryAdd returns true if first time (just initialized) — skip threshold check
            if (_averages.TryAdd(band.Name, energy))
                continue;

            double avg = _averages[band.Name];
            double threshold = avg * _triggerFactor;
            bool isSpike = energy > threshold && energy > 0.01;

            if (isSpike)
            {
                triggered.Add(band);
                // Slow update during spike — baseline rises slightly
                _averages[band.Name] = avg + alphaSlow * (energy - avg);
            }
            else
            {
                // Fast update for ambient noise tracking
                _averages[band.Name] = avg + alphaFast * (energy - avg);
            }
        }

        return triggered;
    }
}
