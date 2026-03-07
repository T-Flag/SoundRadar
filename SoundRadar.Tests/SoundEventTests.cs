using FluentAssertions;
using SoundRadar.Models;

namespace SoundRadar.Tests;

public class SoundEventTests
{
    [Fact]
    public void FreshEvent_GetDecayFactor_ShouldBeApproximatelyOne()
    {
        var evt = new SoundEvent
        {
            Pan = 0f,
            Intensity = 0.5f,
            DominantFrequency = 440f,
            Timestamp = DateTime.UtcNow
        };

        evt.GetDecayFactor().Should().BeApproximately(1.0f, 0.05f);
    }

    [Fact]
    public void ExpiredEvent_IsExpired_ShouldBeTrue()
    {
        var evt = new SoundEvent
        {
            Pan = 0f,
            Intensity = 0.5f,
            DominantFrequency = 440f,
            Timestamp = DateTime.UtcNow.AddMilliseconds(-600),
            DisplayDuration = TimeSpan.FromMilliseconds(500)
        };

        evt.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void GetDecayFactor_ShouldNeverBeLessThanZero()
    {
        var evt = new SoundEvent
        {
            Pan = 0f,
            Intensity = 0.5f,
            DominantFrequency = 440f,
            Timestamp = DateTime.UtcNow.AddSeconds(-10),
            DisplayDuration = TimeSpan.FromMilliseconds(500)
        };

        evt.GetDecayFactor().Should().BeGreaterThanOrEqualTo(0f);
    }

    [Fact]
    public void GetDecayFactor_ShouldNeverBeGreaterThanOne()
    {
        var evt = new SoundEvent
        {
            Pan = 0f,
            Intensity = 0.5f,
            DominantFrequency = 440f,
            Timestamp = DateTime.UtcNow
        };

        evt.GetDecayFactor().Should().BeLessThanOrEqualTo(1f);
    }
}
