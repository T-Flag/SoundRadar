using FluentAssertions;
using SoundRadar.Analysis;

namespace SoundRadar.Tests;

public class SelfSoundFilterTests
{
    [Fact]
    public void SurroundAngleZero_FilterOn_ShouldFilter()
    {
        SelfSoundFilter.ShouldFilter(0f, isSurround: true, filterEnabled: true, filterAngle: 30f)
            .Should().BeTrue();
    }

    [Fact]
    public void SurroundAngle25_FilterOn_Threshold30_ShouldFilter()
    {
        SelfSoundFilter.ShouldFilter(25f, isSurround: true, filterEnabled: true, filterAngle: 30f)
            .Should().BeTrue();
    }

    [Fact]
    public void SurroundAngle45_FilterOn_Threshold30_ShouldNotFilter()
    {
        SelfSoundFilter.ShouldFilter(45f, isSurround: true, filterEnabled: true, filterAngle: 30f)
            .Should().BeFalse();
    }

    [Fact]
    public void SurroundAngleMinus90_FilterOn_ShouldNotFilter()
    {
        SelfSoundFilter.ShouldFilter(-90f, isSurround: true, filterEnabled: true, filterAngle: 30f)
            .Should().BeFalse();
    }

    [Fact]
    public void SurroundAngleZero_FilterOff_ShouldNotFilter()
    {
        SelfSoundFilter.ShouldFilter(0f, isSurround: true, filterEnabled: false, filterAngle: 30f)
            .Should().BeFalse();
    }

    [Fact]
    public void StereoEvent_FilterOn_ShouldNotFilter()
    {
        SelfSoundFilter.ShouldFilter(0f, isSurround: false, filterEnabled: true, filterAngle: 30f)
            .Should().BeFalse();
    }

    [Fact]
    public void SurroundAngleExactlyAtThreshold_ShouldFilter()
    {
        SelfSoundFilter.ShouldFilter(30f, isSurround: true, filterEnabled: true, filterAngle: 30f)
            .Should().BeTrue();
    }

    [Fact]
    public void SurroundAngleNegative25_FilterOn_ShouldFilter()
    {
        SelfSoundFilter.ShouldFilter(-25f, isSurround: true, filterEnabled: true, filterAngle: 30f)
            .Should().BeTrue();
    }
}
