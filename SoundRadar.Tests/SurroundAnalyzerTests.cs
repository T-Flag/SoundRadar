using FluentAssertions;
using SoundRadar.Analysis;

namespace SoundRadar.Tests;

public class SurroundAnalyzerTests
{
    [Fact]
    public void SignalOnlyFL_ShouldGiveAngleMinus45()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(fl: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(-45f, 0.1f);
    }

    [Fact]
    public void SignalOnlyFR_ShouldGiveAnglePlus45()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(fr: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(45f, 0.1f);
    }

    [Fact]
    public void SignalOnlyFC_ShouldGiveAngle0()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(fc: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(0f, 0.1f);
    }

    [Fact]
    public void SignalOnlyRL_ShouldGiveAngleMinus135()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(rl: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(-135f, 0.1f);
    }

    [Fact]
    public void SignalOnlyRR_ShouldGiveAnglePlus135()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(rr: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(135f, 0.1f);
    }

    [Fact]
    public void SignalOnlySL_ShouldGiveAngleMinus90()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(sl: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(-90f, 0.1f);
    }

    [Fact]
    public void SignalOnlySR_ShouldGiveAnglePlus90()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(sr: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(90f, 0.1f);
    }

    [Fact]
    public void EqualFL_FR_ShouldGiveAngle0_FrontCenter()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(fl: 0.5f, fr: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(0f, 0.1f);
    }

    [Fact]
    public void EqualRL_RR_ShouldGiveAngle180_RearCenter()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(rl: 0.5f, rr: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        // atan2(0, negative) = +180°
        Math.Abs(result!.Value.Angle).Should().BeApproximately(180f, 0.1f);
    }

    [Fact]
    public void EqualFL_SL_ShouldGiveAngleMinus67_5()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(fl: 0.5f, sl: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().NotBeNull();
        result!.Value.Angle.Should().BeApproximately(-67.5f, 0.1f);
    }

    [Fact]
    public void Silence_ShouldReturnNull()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(); // all zeros

        var result = analyzer.Analyze(buffer, 8);

        result.Should().BeNull();
    }

    [Fact]
    public void SignalOnlyLFE_ShouldReturnNull()
    {
        var analyzer = new SurroundAnalyzer();
        var buffer = CreateSurroundBuffer(lfe: 0.5f);

        var result = analyzer.Analyze(buffer, 8);

        result.Should().BeNull();
    }

    /// <summary>
    /// Creates an 8-channel interleaved buffer with specified per-channel amplitudes.
    /// Channel order: FL(0), FR(1), FC(2), LFE(3), RL(4), RR(5), SL(6), SR(7)
    /// </summary>
    private static float[] CreateSurroundBuffer(
        float fl = 0f, float fr = 0f, float fc = 0f, float lfe = 0f,
        float rl = 0f, float rr = 0f, float sl = 0f, float sr = 0f,
        int frameCount = 1024)
    {
        float[] channelValues = { fl, fr, fc, lfe, rl, rr, sl, sr };
        var buffer = new float[frameCount * 8];
        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int ch = 0; ch < 8; ch++)
            {
                buffer[frame * 8 + ch] = channelValues[ch];
            }
        }
        return buffer;
    }
}
