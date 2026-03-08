using FluentAssertions;
using SoundRadar.Analysis;

namespace SoundRadar.Tests;

public class AngleMappingTests
{
    [Fact]
    public void PanZero_ShouldMapToZeroDegrees()
    {
        DirectionAnalyzer.PanToAngle(0f).Should().BeApproximately(0f, 0.1f);
    }

    [Fact]
    public void PanPlusOne_ShouldMapToPlus90Degrees()
    {
        DirectionAnalyzer.PanToAngle(1f).Should().BeApproximately(90f, 0.1f);
    }

    [Fact]
    public void PanMinusOne_ShouldMapToMinus90Degrees()
    {
        DirectionAnalyzer.PanToAngle(-1f).Should().BeApproximately(-90f, 0.1f);
    }

    [Fact]
    public void Mapping_ShouldBeMonotone()
    {
        float[] pans = { -1f, -0.7f, -0.3f, 0f, 0.3f, 0.7f, 1f };
        for (int i = 1; i < pans.Length; i++)
        {
            DirectionAnalyzer.PanToAngle(pans[i])
                .Should().BeGreaterThan(DirectionAnalyzer.PanToAngle(pans[i - 1]));
        }
    }
}
