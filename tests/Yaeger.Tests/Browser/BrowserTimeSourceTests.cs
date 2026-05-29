using Yaeger.Browser;

namespace Yaeger.Tests.Browser;

public class BrowserTimeSourceTests
{
    [Fact]
    public void Advance_FirstTick_ShouldSetZeroDeltaTime()
    {
        // Arrange
        var timeSource = new BrowserTimeSource();

        // Act
        timeSource.Advance(1234.0);

        // Assert
        Assert.Equal(0f, timeSource.DeltaTime);
        Assert.Equal(0d, timeSource.TotalTime, 6);
    }

    [Fact]
    public void Advance_ForwardTimestamps_ShouldAccumulateDeltaAndTotal()
    {
        // Arrange
        var timeSource = new BrowserTimeSource(maxDeltaTimeSeconds: 1f);
        timeSource.Advance(1000.0);

        // Act
        timeSource.Advance(1125.0);

        // Assert
        Assert.Equal(0.125f, timeSource.DeltaTime, 6);
        Assert.Equal(0.125d, timeSource.TotalTime, 6);
    }

    [Fact]
    public void Advance_BackwardTimestamp_ShouldKeepDeltaAtZero()
    {
        // Arrange
        var timeSource = new BrowserTimeSource(maxDeltaTimeSeconds: 1f);
        timeSource.Advance(1000.0);
        timeSource.Advance(1016.0);

        // Act
        timeSource.Advance(1000.0);

        // Assert
        Assert.Equal(0f, timeSource.DeltaTime);
        Assert.Equal(0.016d, timeSource.TotalTime, 6);
    }

    [Fact]
    public void Advance_LargeFrameGap_ShouldClampDeltaTime()
    {
        // Arrange
        const float maxDeltaSeconds = 0.05f;
        var timeSource = new BrowserTimeSource(maxDeltaTimeSeconds: maxDeltaSeconds);
        timeSource.Advance(1000.0);

        // Act
        timeSource.Advance(1250.0);

        // Assert
        Assert.Equal(maxDeltaSeconds, timeSource.DeltaTime, 6);
        Assert.Equal(maxDeltaSeconds, timeSource.TotalTime, 6);
    }
}
