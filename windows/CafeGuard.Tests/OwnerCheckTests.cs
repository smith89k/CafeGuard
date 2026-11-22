namespace CafeGuard.Tests;

/// <summary>Turn OFF asks Windows "Verify it's you" (PIN / fingerprint). Owner decision 2026-11-22.</summary>
public class OwnerCheckTests
{
    [Fact] public void Verified_TurnsOff() => Assert.Equal(OffDecision.TurnOff, OwnerCheck.Decide(VerifyOutcome.Verified));
    [Fact] public void Refused_StaysOn() => Assert.Equal(OffDecision.StayOn, OwnerCheck.Decide(VerifyOutcome.Refused));
    // No PIN / fingerprint set up, or the prompt can't open: lock the screen; unlocking turns it OFF.
    [Fact] public void Unavailable_LocksInstead() => Assert.Equal(OffDecision.LockInstead, OwnerCheck.Decide(VerifyOutcome.Unavailable));

    [Theory]
    [InlineData(0, VerifyOutcome.Verified)]     // Verified
    [InlineData(1, VerifyOutcome.Unavailable)]  // DeviceNotPresent
    [InlineData(2, VerifyOutcome.Unavailable)]  // NotConfiguredForUser
    [InlineData(3, VerifyOutcome.Unavailable)]  // DisabledByPolicy
    [InlineData(4, VerifyOutcome.Refused)]      // DeviceBusy
    [InlineData(5, VerifyOutcome.Refused)]      // RetriesExhausted
    [InlineData(6, VerifyOutcome.Refused)]      // Canceled
    [InlineData(99, VerifyOutcome.Unavailable)]
    public void WindowsResult_Maps(int result, VerifyOutcome expected) => Assert.Equal(expected, OwnerCheck.FromWindowsResult(result));

    [Fact]
    public void WinRtIid_MatchesKnownWindowsValue() =>
        // IAsyncOperation<bool>, a published Windows interface id.
        Assert.Equal(new Guid("cdb5efb3-5788-509d-9be1-71ccb8a3362a"),
            WinRtIid.ForSignature("pinterface({9fc2b0bb-e446-44e2-aa61-9cab8f636af2};b1)"));

    [Fact]
    public void ResultOperationId_IsComputedCorrectly() =>
        Assert.Equal(new Guid("fd596ffd-2318-558f-9dbe-d21df43764a5"), WinRtIid.ForSignature(OwnerCheck.ResultOperationSignature));
}
