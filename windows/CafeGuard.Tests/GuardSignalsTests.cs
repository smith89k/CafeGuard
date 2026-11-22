using CafeGuard.Shared;

namespace CafeGuard.Tests;

public class GuardSignalsTests
{
    [Fact]
    public void IsGuarding_FollowsSetGuarding()
    {
        GuardSignals.NameSuffix = "_test_" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.False(GuardSignals.IsGuarding()); // app not running → event does not exist
            GuardSignals.SetGuarding(true);
            Assert.True(GuardSignals.IsGuarding());
            GuardSignals.SetGuarding(false);
            Assert.False(GuardSignals.IsGuarding());
        }
        finally
        {
            GuardSignals.NameSuffix = string.Empty;
        }
    }
}
