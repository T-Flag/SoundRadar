using FluentAssertions;
using SoundRadar.Analysis;

namespace SoundRadar.Tests;

public class PanNormalizationTests
{
    [Fact]
    public void RawPanEqualToMaxExpected_ShouldNormalizeToOne()
    {
        var analyzer = new DirectionAnalyzer { MaxExpectedPan = 0.75f };
        DirectionAnalyzer.NormalizePan(0.75f, 0.75f).Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void NegativeRawPanEqualToMaxExpected_ShouldNormalizeToMinusOne()
    {
        DirectionAnalyzer.NormalizePan(-0.75f, 0.75f).Should().BeApproximately(-1f, 0.01f);
    }

    [Fact]
    public void RawPanZero_ShouldNormalizeToZero()
    {
        DirectionAnalyzer.NormalizePan(0f, 0.75f).Should().BeApproximately(0f, 0.01f);
    }

    [Fact]
    public void RawPanAboveMaxExpected_ShouldClampToOne()
    {
        DirectionAnalyzer.NormalizePan(0.9f, 0.75f).Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void RawPanHalfOfMaxExpected_ShouldNormalizeToHalf()
    {
        DirectionAnalyzer.NormalizePan(0.375f, 0.75f).Should().BeApproximately(0.5f, 0.01f);
    }
}
