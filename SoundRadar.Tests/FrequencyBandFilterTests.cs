using FluentAssertions;
using SoundRadar.Analysis;

namespace SoundRadar.Tests;

public class FrequencyBandFilterTests
{
    private const int SampleRate = 44100;
    private const int FftSize = 2048; // better frequency resolution for band tests

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
    public void Sine250Hz_ShouldConcentrateInLowMid()
    {
        var spectrumAnalyzer = new SpectrumAnalyzer(FftSize);
        var filter = new FrequencyBandFilter();
        var buffer = GenerateStereoSine(250f, SampleRate, FftSize);

        var (left, right) = spectrumAnalyzer.Analyze(buffer, SampleRate);
        var bands = filter.Analyze(left, right, SampleRate, FftSize);

        var lowMid = bands.First(b => b.Name == "LowMid");
        var others = bands.Where(b => b.Name != "LowMid");

        lowMid.Energy.Should().BeGreaterThan(0);
        foreach (var other in others)
            lowMid.Energy.Should().BeGreaterThan(other.Energy * 10);
    }

    [Fact]
    public void Sine3000Hz_ShouldConcentrateInHighMid()
    {
        var spectrumAnalyzer = new SpectrumAnalyzer(FftSize);
        var filter = new FrequencyBandFilter();
        var buffer = GenerateStereoSine(3000f, SampleRate, FftSize);

        var (left, right) = spectrumAnalyzer.Analyze(buffer, SampleRate);
        var bands = filter.Analyze(left, right, SampleRate, FftSize);

        var highMid = bands.First(b => b.Name == "HighMid");
        var others = bands.Where(b => b.Name != "HighMid");

        highMid.Energy.Should().BeGreaterThan(0);
        foreach (var other in others)
            highMid.Energy.Should().BeGreaterThan(other.Energy * 10);
    }

    [Fact]
    public void Sine250Hz_FullLeft_ShouldHavePanNearMinusOne()
    {
        var spectrumAnalyzer = new SpectrumAnalyzer(FftSize);
        var filter = new FrequencyBandFilter();
        var buffer = GenerateStereoSine(250f, SampleRate, FftSize, leftAmp: 1f, rightAmp: 0f);

        var (left, right) = spectrumAnalyzer.Analyze(buffer, SampleRate);
        var bands = filter.Analyze(left, right, SampleRate, FftSize);

        var lowMid = bands.First(b => b.Name == "LowMid");
        lowMid.Pan.Should().BeApproximately(-1f, 0.1f);
    }

    [Fact]
    public void TwoSines_ShouldHaveEnergyInBothBands()
    {
        var spectrumAnalyzer = new SpectrumAnalyzer(FftSize);
        var filter = new FrequencyBandFilter();

        // Mix 250Hz + 3000Hz
        var buffer = new float[FftSize * 2];
        for (int i = 0; i < FftSize; i++)
        {
            float s1 = (float)Math.Sin(2 * Math.PI * 250 * i / SampleRate);
            float s2 = (float)Math.Sin(2 * Math.PI * 3000 * i / SampleRate);
            buffer[i * 2] = s1 + s2;
            buffer[i * 2 + 1] = s1 + s2;
        }

        var (left, right) = spectrumAnalyzer.Analyze(buffer, SampleRate);
        var bands = filter.Analyze(left, right, SampleRate, FftSize);

        var lowMid = bands.First(b => b.Name == "LowMid");
        var highMid = bands.First(b => b.Name == "HighMid");

        lowMid.Energy.Should().BeGreaterThan(0.01);
        highMid.Energy.Should().BeGreaterThan(0.01);
    }

    [Fact]
    public void Silence_ShouldHaveAllBandsNearZero()
    {
        var spectrumAnalyzer = new SpectrumAnalyzer(FftSize);
        var filter = new FrequencyBandFilter();
        var buffer = new float[FftSize * 2];

        var (left, right) = spectrumAnalyzer.Analyze(buffer, SampleRate);
        var bands = filter.Analyze(left, right, SampleRate, FftSize);

        foreach (var band in bands)
            band.Energy.Should().BeLessThan(0.001);
    }
}
