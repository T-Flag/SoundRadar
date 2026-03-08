using FluentAssertions;
using SoundRadar.Analysis;
using SoundRadar.Models;

namespace SoundRadar.Tests;

public class AdaptiveThresholdTests
{
    [Fact]
    public void ConstantLevel_ShouldNotTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var bands = CreateBands(energy: 0.1);
        int triggered = 0;

        // Feed constant level for many frames (~5 seconds at 60fps)
        for (int i = 0; i < 300; i++)
        {
            var result = threshold.Process(bands, frameDurationSec: 1.0 / 60);
            triggered += result.Count;
        }

        triggered.Should().Be(0);
    }

    [Fact]
    public void ConstantThenSpike3x_ShouldTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var normalBands = CreateBands(energy: 0.1);

        // Stabilize baseline
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        // Spike at 3x
        var spikeBands = CreateBands(energy: 0.3);
        var result = threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ConstantThenSpike1_5x_ShouldNotTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var normalBands = CreateBands(energy: 0.1);

        // Stabilize baseline
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        // Spike at 1.5x (below 2.5 factor)
        var spikeBands = CreateBands(energy: 0.15);
        var result = threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AfterSpike_AverageShouldNotRise()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var normalBands = CreateBands(energy: 0.1);

        // Stabilize baseline
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        double avgBefore = threshold.GetAverage("LowMid");

        // Send spike
        var spikeBands = CreateBands(energy: 1.0);
        threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        double avgAfter = threshold.GetAverage("LowMid");

        // Average should not have increased (spike excluded from EMA)
        avgAfter.Should().BeLessThanOrEqualTo(avgBefore + 0.001);
    }

    [Fact]
    public void SilenceThenSound_ShouldTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var silence = CreateBands(energy: 0.0);

        // Feed silence
        for (int i = 0; i < 100; i++)
            threshold.Process(silence, frameDurationSec: 1.0 / 60);

        // Any sound should trigger (average ≈ 0)
        var sound = CreateBands(energy: 0.1);
        var result = threshold.Process(sound, frameDurationSec: 1.0 / 60);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ConstantLevel_BaselineShouldConverge()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var bands = CreateBands(energy: 0.1);

        // Feed 100 frames of constant signal
        for (int i = 0; i < 100; i++)
            threshold.Process(bands, frameDurationSec: 1.0 / 60);

        // Baseline should converge close to the constant level
        double avg = threshold.GetAverage("Mid");
        avg.Should().BeApproximately(0.1, 0.02);
    }

    [Fact]
    public void TriggerLevel_ShouldEqualBaseline_TimesFactor()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 2.5);
        var bands = CreateBands(energy: 0.1);

        // Stabilize
        for (int i = 0; i < 300; i++)
            threshold.Process(bands, frameDurationSec: 1.0 / 60);

        double avg = threshold.GetAverage("Mid");
        double expectedTrigger = avg * 2.5;

        // Verify trigger level relationship
        expectedTrigger.Should().BeApproximately(avg * threshold.TriggerFactor, 0.001);
        avg.Should().BeApproximately(0.1, 0.005);
    }

    private static BandAnalysis[] CreateBands(double energy)
    {
        return new[]
        {
            new BandAnalysis { Name = "SubBass", Energy = energy, Pan = 0f, Intensity = (float)energy },
            new BandAnalysis { Name = "LowMid", Energy = energy, Pan = 0f, Intensity = (float)energy },
            new BandAnalysis { Name = "Mid", Energy = energy, Pan = 0f, Intensity = (float)energy },
            new BandAnalysis { Name = "HighMid", Energy = energy, Pan = 0f, Intensity = (float)energy },
        };
    }
}
