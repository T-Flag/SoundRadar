using FluentAssertions;
using SoundRadar.Analysis;
using SoundRadar.Models;

namespace SoundRadar.Tests;

public class AdaptiveThresholdTests
{
    [Fact]
    public void ConstantLevel_ShouldNotTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
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
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
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
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var normalBands = CreateBands(energy: 0.1);

        // Stabilize baseline
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        // Spike at 1.5x (at threshold, not above) → should NOT trigger
        var spikeBands = CreateBands(energy: 0.15);
        var result = threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AfterSpike_AverageShouldRiseSlowly()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var normalBands = CreateBands(energy: 0.1);

        // Stabilize baseline at ~0.1
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        double avgBefore = threshold.GetAverage("LowMid");

        // Send spike — baseline should rise slightly (slow EMA)
        var spikeBands = CreateBands(energy: 0.8);
        threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        double avgAfter = threshold.GetAverage("LowMid");

        // Average should rise, but only slightly (10x slower alpha)
        avgAfter.Should().BeGreaterThan(avgBefore);
        avgAfter.Should().BeLessThan(avgBefore + 0.05);
    }

    [Fact]
    public void SilenceThenSound_ShouldTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
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
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
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
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var bands = CreateBands(energy: 0.1);

        // Stabilize
        for (int i = 0; i < 300; i++)
            threshold.Process(bands, frameDurationSec: 1.0 / 60);

        double avg = threshold.GetAverage("Mid");
        double expectedTrigger = avg * 1.5;

        // Verify trigger level relationship
        expectedTrigger.Should().BeApproximately(avg * threshold.TriggerFactor, 0.001);
        avg.Should().BeApproximately(0.1, 0.005);
    }

    [Fact]
    public void ConstantLevel05_BaselineShouldConverge()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var bands = CreateBands(energy: 0.5);

        // Feed 100 frames of constant signal
        for (int i = 0; i < 100; i++)
            threshold.Process(bands, frameDurationSec: 1.0 / 60);

        double avg = threshold.GetAverage("Mid");
        avg.Should().BeApproximately(0.5, 0.05);
    }

    [Fact]
    public void TriggerFactor1_5_Spike2x_ShouldTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var normalBands = CreateBands(energy: 0.3);

        // Stabilize baseline at 0.3
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        // Spike at 2x (0.6 > 0.3 * 1.5 = 0.45) → should trigger
        var spikeBands = CreateBands(energy: 0.6);
        var result = threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public void TriggerFactor1_5_Spike1_2x_ShouldNotTrigger()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var normalBands = CreateBands(energy: 0.3);

        // Stabilize baseline at 0.3
        for (int i = 0; i < 300; i++)
            threshold.Process(normalBands, frameDurationSec: 1.0 / 60);

        // Spike at 1.2x (0.36 < 0.3 * 1.5 = 0.45) → should NOT trigger
        var spikeBands = CreateBands(energy: 0.36);
        var result = threshold.Process(spikeBands, frameDurationSec: 1.0 / 60);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ConstantLevel05_200Frames_BaselineShouldConverge()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var bands = CreateBands(energy: 0.5);

        // Feed 200 frames (~3.3s at 60fps) of constant 0.5
        for (int i = 0; i < 200; i++)
            threshold.Process(bands, frameDurationSec: 1.0 / 60);

        // Baseline must converge toward 0.5, not stay near 0
        double avg = threshold.GetAverage("Mid");
        avg.Should().BeGreaterThanOrEqualTo(0.4);
        avg.Should().BeLessThanOrEqualTo(0.55);
    }

    [Fact]
    public void ContinuousNoiseThenSilence_BaselineShouldDrop()
    {
        var threshold = new AdaptiveThreshold(adaptationTimeSec: 0.5, triggerFactor: 1.5);
        var noise = CreateBands(energy: 0.4);

        // Feed continuous noise for ~5s
        for (int i = 0; i < 300; i++)
            threshold.Process(noise, frameDurationSec: 1.0 / 60);

        double avgAfterNoise = threshold.GetAverage("Mid");
        avgAfterNoise.Should().BeGreaterThanOrEqualTo(0.35);

        // Then silence for ~5s
        var silence = CreateBands(energy: 0.0);
        for (int i = 0; i < 300; i++)
            threshold.Process(silence, frameDurationSec: 1.0 / 60);

        double avgAfterSilence = threshold.GetAverage("Mid");
        avgAfterSilence.Should().BeLessThan(0.05);
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
