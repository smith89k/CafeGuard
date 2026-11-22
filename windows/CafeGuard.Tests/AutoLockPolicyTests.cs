namespace CafeGuard.Tests;

public class AutoLockPolicyTests
{
    private static readonly DateTime Lock = new(2026, 11, 22, 15, 0, 0);
    private static readonly DateTime Now = Lock.AddSeconds(2);

    private static bool Arm(bool armOnLock = true, GuardState state = GuardState.Off, bool lidOpen = true, bool charger = true,
        DateTime? lidClosedAt = null, DateTime? suspendAt = null, DateTime? resumeAt = null) =>
        AutoLockPolicy.ShouldArmAfterLock(armOnLock, state, lidOpen, charger, Lock, Now, lidClosedAt, suspendAt, resumeAt);

    [Fact] public void Arms_ForAPlainWinL() => Assert.True(Arm());
    [Fact] public void DoesNotArm_WhenSwitchOff() => Assert.False(Arm(armOnLock: false));
    [Fact] public void DoesNotArm_WhenAlreadyOn() => Assert.False(Arm(state: GuardState.On));
    [Fact] public void DoesNotArm_WhenAlarm() => Assert.False(Arm(state: GuardState.Alarm));
    [Fact] public void DoesNotArm_WhenLidClosed() => Assert.False(Arm(lidOpen: false));
    [Fact] public void DoesNotArm_WithoutCharger() => Assert.False(Arm(charger: false));

    // Windows may report the lock AFTER the lid-close / sleep that caused it.
    [Fact] public void DoesNotArm_WhenLidClosedJustBeforeLock() => Assert.False(Arm(lidClosedAt: Lock.AddSeconds(-1)));
    [Fact] public void DoesNotArm_WhenSleepStartedJustBeforeLock() => Assert.False(Arm(suspendAt: Lock.AddSeconds(-3)));
    [Fact] public void DoesNotArm_WhenSleepStartedDuringWait() => Assert.False(Arm(suspendAt: Lock.AddSeconds(1)));
    [Fact] public void DoesNotArm_WhenJustWokeUp() => Assert.False(Arm(resumeAt: Lock.AddSeconds(-2)));
    [Fact] public void Arms_WhenLidClosedLongAgo() => Assert.True(Arm(lidClosedAt: Lock.AddMinutes(-10), resumeAt: Lock.AddMinutes(-9)));

    [Theory]
    [InlineData(GuardState.On, true)]
    [InlineData(GuardState.Alarm, true)]
    [InlineData(GuardState.Off, false)]
    public void Disarms_OnUnlock(GuardState state, bool expected) =>
        Assert.Equal(expected, AutoLockPolicy.ShouldDisarmOnUnlock(state));
}
