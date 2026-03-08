using FluentAssertions;
using SoundRadar.Analysis;

namespace SoundRadar.Tests;

public class SpectrumAnalyzerTests
{
    private const int SampleRate = 44100;
    private const int FftSize = 1024;

    private static float[] GenerateStereoSine(float frequency, int sampleRate, int frames, float leftAmp = 1f, float rightAmp = 1f)
    {
        var buffer = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            float sample = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            buffer[i * 2] = sample * leftAmp;
            buffer[i * 2 + 1] = sample * rightAmp;
        }
        return buffer;
    }

    [Fact]
    public void Sine440Hz_ShouldHavePeakAtCorrectBin()
    {
        var analyzer = new SpectrumAnalyzer(FftSize);
        var buffer = GenerateStereoSine(440f, SampleRate, FftSize);

        var (leftMagnitudes, rightMagnitudes) = analyzer.Analyze(buffer, SampleRate);

        int expectedBin = (int)Math.Round(440.0 * FftSize / SampleRate);
        int peakBin = Array.IndexOf(leftMagnitudes, leftMagnitudes.Max());

        peakBin.Should().BeCloseTo(expectedBin, 2);
    }

    [Fact]
    public void Silence_ShouldHaveAllMagnitudesNearZero()
    {
        var analyzer = new SpectrumAnalyzer(FftSize);
        var buffer = new float[FftSize * 2]; // all zeros

        var (leftMagnitudes, _) = analyzer.Analyze(buffer, SampleRate);

        leftMagnitudes.Max().Should().BeLessThan(0.001);
    }

    [Fact]
    public void Sine1000Hz_ShouldNotPeakAt440Hz()
    {
        var analyzer = new SpectrumAnalyzer(FftSize);
        var buffer = GenerateStereoSine(1000f, SampleRate, FftSize);

        var (leftMagnitudes, _) = analyzer.Analyze(buffer, SampleRate);

        int bin440 = (int)Math.Round(440.0 * FftSize / SampleRate);
        int bin1000 = (int)Math.Round(1000.0 * FftSize / SampleRate);

        leftMagnitudes[bin1000].Should().BeGreaterThan(leftMagnitudes[bin440] * 10);
    }

    [Fact]
    public void OutputSize_ShouldBeHalfFftSizePlusOne()
    {
        var analyzer = new SpectrumAnalyzer(FftSize);
        var buffer = GenerateStereoSine(440f, SampleRate, FftSize);

        var (leftMagnitudes, rightMagnitudes) = analyzer.Analyze(buffer, SampleRate);

        // ForwardReal returns N/2+1 bins (DC through Nyquist)
        leftMagnitudes.Length.Should().Be(FftSize / 2 + 1);
        rightMagnitudes.Length.Should().Be(FftSize / 2 + 1);
    }
}
