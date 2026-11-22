namespace CafeGuard.Tests;

/// <summary>While ON, opening Task Manager sounds the alarm (owner decision 2026-11-22).</summary>
public class TaskManagerDetectorTests
{
    [Fact]
    public void LooksForTaskManagersWindow()
    {
        string? asked = null;
        Assert.True(TaskManagerDetector.IsOpen(cls => { asked = cls; return true; }));
        Assert.Equal("TaskManagerWindow", asked);
        Assert.False(TaskManagerDetector.IsOpen(_ => false));
    }

    [Fact]
    public void RealCheck_DoesNotCrash() => _ = TaskManagerDetector.IsOpen();
}
