using System.ComponentModel;
using System.Diagnostics;

namespace CafeGuard.Tests;

/// <summary>While ON, a normal Task Manager "End task" must get "Access denied" (owner decision 2026-11-22).</summary>
public class ProcessGuardTests
{
    [Fact]
    public void DenyTerminate_BlocksNormalKill_AndAllowPutsItBack()
    {
        using var victim = Process.Start(new ProcessStartInfo("ping", "-n 30 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            Assert.True(ProcessGuard.DenyTerminate(victim.Handle));
            using (var outsider = Process.GetProcessById(victim.Id))   // a fresh handle, like Task Manager would open
                Assert.Throws<Win32Exception>(() => outsider.Kill());
            Assert.False(victim.HasExited);

            Assert.True(ProcessGuard.AllowTerminate(victim.Handle));
            using (var outsider = Process.GetProcessById(victim.Id))
            {
                outsider.Kill();
                Assert.True(outsider.WaitForExit(5000));
            }
        }
        finally
        {
            ProcessGuard.AllowTerminate(victim.Handle);
            try { victim.Kill(); } catch { }
        }
    }

    [Fact]
    public void CenterOf_WorkArea() =>
        Assert.Equal(new System.Drawing.Point(960, 520), ProcessGuard.CenterOf(new System.Drawing.Rectangle(0, 0, 1920, 1040)));
}
