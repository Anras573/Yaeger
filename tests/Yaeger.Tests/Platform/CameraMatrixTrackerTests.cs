using System.Numerics;
using Yaeger.Platform;

namespace Yaeger.Tests.Platform;

public class CameraMatrixTrackerTests
{
    [Fact]
    public void Set_ShouldFlushWithPreviousMatrix_WhenMatrixChanges()
    {
        var tracker = new CameraMatrixTracker();
        var a = Matrix4x4.CreateScale(2f);
        var b = Matrix4x4.CreateScale(3f);
        tracker.Set(a, () => { });
        Matrix4x4? matrixAtFlush = null;

        var changed = tracker.Set(b, () => matrixAtFlush = tracker.Current);

        Assert.True(changed);
        Assert.Equal(a, matrixAtFlush);
        Assert.Equal(b, tracker.Current);
    }

    [Fact]
    public void Set_ShouldNotFlush_WhenMatrixUnchanged()
    {
        var tracker = new CameraMatrixTracker();
        var flushes = 0;

        var changed = tracker.Set(Matrix4x4.Identity, () => flushes++);

        Assert.False(changed);
        Assert.Equal(0, flushes);
    }
}
