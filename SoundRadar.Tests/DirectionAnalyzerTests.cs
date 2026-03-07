using FluentAssertions;
using SoundRadar.Analysis;
using SoundRadar.Models;

namespace SoundRadar.Tests;

public class DirectionAnalyzerTests
{
    private const int SampleRate = 44100;

    private static float[] CreateStereoBuffer(float leftLevel, float rightLevel, int frames = 882)
    {
        var buffer = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            buffer[i * 2] = leftLevel;
            buffer[i * 2 + 1] = rightLevel;
        }
        return buffer;
    }

    [Fact]
    public void SilentBuffer_ShouldNotEmitEvent()
    {
        var analyzer = new DirectionAnalyzer();
        SoundEvent? received = null;
        analyzer.SoundDetected += e => received = e;

        var silence = CreateStereoBuffer(0f, 0f);
        analyzer.ProcessBuffer(silence, SampleRate);

        received.Should().BeNull();
    }

    [Fact]
    public void FullLeftBuffer_ShouldEmitPanNearMinusOne()
    {
        var analyzer = new DirectionAnalyzer(intensityThreshold: 0.01f);
        SoundEvent? received = null;
        analyzer.SoundDetected += e => received = e;

        var buffer = CreateStereoBuffer(0.8f, 0f);
        analyzer.ProcessBuffer(buffer, SampleRate);

        received.Should().NotBeNull();
        received!.Pan.Should().BeApproximately(-1f, 0.1f);
    }

    [Fact]
    public void FullRightBuffer_ShouldEmitPanNearPlusOne()
    {
        var analyzer = new DirectionAnalyzer(intensityThreshold: 0.01f);
        SoundEvent? received = null;
        analyzer.SoundDetected += e => received = e;

        var buffer = CreateStereoBuffer(0f, 0.8f);
        analyzer.ProcessBuffer(buffer, SampleRate);

        received.Should().NotBeNull();
        received!.Pan.Should().BeApproximately(1f, 0.1f);
    }

    [Fact]
    public void EqualLeftRight_ShouldEmitPanNearZero()
    {
        var analyzer = new DirectionAnalyzer(intensityThreshold: 0.01f);
        SoundEvent? received = null;
        analyzer.SoundDetected += e => received = e;

        var buffer = CreateStereoBuffer(0.5f, 0.5f);
        analyzer.ProcessBuffer(buffer, SampleRate);

        received.Should().NotBeNull();
        received!.Pan.Should().BeApproximately(0f, 0.1f);
    }

    [Fact]
    public void BufferBelowThreshold_ShouldNotEmitEvent()
    {
        var analyzer = new DirectionAnalyzer(intensityThreshold: 0.5f);
        SoundEvent? received = null;
        analyzer.SoundDetected += e => received = e;

        var buffer = CreateStereoBuffer(0.01f, 0.01f);
        analyzer.ProcessBuffer(buffer, SampleRate);

        received.Should().BeNull();
    }

    [Fact]
    public void BufferAboveThreshold_ShouldEmitEventWithCorrectIntensity()
    {
        var analyzer = new DirectionAnalyzer(intensityThreshold: 0.01f);
        SoundEvent? received = null;
        analyzer.SoundDetected += e => received = e;

        var buffer = CreateStereoBuffer(0.5f, 0.5f);
        analyzer.ProcessBuffer(buffer, SampleRate);

        received.Should().NotBeNull();
        received!.Intensity.Should().BeGreaterThan(0f);
        received!.Intensity.Should().BeLessThanOrEqualTo(1f);
    }
}
