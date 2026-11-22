using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CafeGuard
{
    /// <summary>
    /// While ON, a normal Task Manager "End task" gets "Access denied": the process tells Windows that nobody may
    /// terminate it (a "deny terminate" rule for Everyone). Someone with administrator rights can still force it —
    /// Windows always allows that. When OFF, the original rules are put back so closing works normally.
    /// </summary>
    public static class ProcessGuard
    {
        private const int SeKernelObject = 6;
        private const int DaclSecurityInformation = 4;
        private const uint ProcessTerminate = 0x0001;
        private const int DenyAccess = 3;
        private const int TrusteeIsSid = 0;
        private const int TrusteeIsWellKnownGroup = 5;

        private static readonly Dictionary<IntPtr, byte[]> SavedDacls = new();
        private static readonly object Gate = new();

        /// <summary>The running Cafe Guard process.</summary>
        public static IntPtr Self => GetCurrentProcess();

        public static bool DenyTerminate(IntPtr process)
        {
            lock (Gate)
            {
                if (SavedDacls.ContainsKey(process)) return true;
                IntPtr sd = IntPtr.Zero, newAcl = IntPtr.Zero, everyone = IntPtr.Zero;
                try
                {
                    if (GetSecurityInfo(process, SeKernelObject, DaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, out IntPtr dacl, IntPtr.Zero, out sd) != 0)
                        return false;

                    // Keep a copy of the current rules so AllowTerminate can put them back exactly.
                    byte[] saved = Array.Empty<byte>();
                    if (dacl != IntPtr.Zero)
                    {
                        saved = new byte[(ushort)Marshal.ReadInt16(dacl, 2)]; // ACL.AclSize
                        Marshal.Copy(dacl, saved, 0, saved.Length);
                    }

                    if (!ConvertStringSidToSid("S-1-1-0", out everyone)) return false; // Everyone
                    var rule = new ExplicitAccess
                    {
                        AccessPermissions = ProcessTerminate,
                        AccessMode = DenyAccess,
                        Inheritance = 0,
                        Trustee = new Trustee { Form = TrusteeIsSid, Type = TrusteeIsWellKnownGroup, Name = everyone }
                    };
                    if (SetEntriesInAcl(1, new[] { rule }, dacl, out newAcl) != 0) return false;
                    if (SetSecurityInfo(process, SeKernelObject, DaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, newAcl, IntPtr.Zero) != 0)
                        return false;

                    SavedDacls[process] = saved;
                    return true;
                }
                catch (Exception ex)
                {
                    AppLog.Write($"Could not protect against End task: {ex.Message}");
                    return false;
                }
                finally
                {
                    if (newAcl != IntPtr.Zero) LocalFree(newAcl);
                    if (everyone != IntPtr.Zero) LocalFree(everyone);
                    if (sd != IntPtr.Zero) LocalFree(sd);
                }
            }
        }

        public static bool AllowTerminate(IntPtr process)
        {
            lock (Gate)
            {
                if (!SavedDacls.TryGetValue(process, out byte[]? saved)) return true; // never protected
                IntPtr acl = IntPtr.Zero;
                try
                {
                    if (saved.Length > 0)
                    {
                        acl = Marshal.AllocHGlobal(saved.Length);
                        Marshal.Copy(saved, 0, acl, saved.Length);
                    }
                    if (SetSecurityInfo(process, SeKernelObject, DaclSecurityInformation, IntPtr.Zero, IntPtr.Zero, acl, IntPtr.Zero) != 0)
                        return false;
                    SavedDacls.Remove(process);
                    return true;
                }
                finally
                {
                    if (acl != IntPtr.Zero) Marshal.FreeHGlobal(acl);
                }
            }
        }

        /// <summary>Middle of a screen area (where Windows' PIN window should appear).</summary>
        public static System.Drawing.Point CenterOf(System.Drawing.Rectangle area) =>
            new(area.Left + area.Width / 2, area.Top + area.Height / 2);

        [StructLayout(LayoutKind.Sequential)]
        private struct Trustee
        {
            public IntPtr MultipleTrustee;
            public int MultipleTrusteeOperation;
            public int Form;
            public int Type;
            public IntPtr Name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExplicitAccess
        {
            public uint AccessPermissions;
            public int AccessMode;
            public uint Inheritance;
            public Trustee Trustee;
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr mem);

        [DllImport("advapi32.dll")]
        private static extern int GetSecurityInfo(IntPtr handle, int objectType, int securityInfo,
            IntPtr owner, IntPtr group, out IntPtr dacl, IntPtr sacl, out IntPtr securityDescriptor);

        [DllImport("advapi32.dll")]
        private static extern int SetSecurityInfo(IntPtr handle, int objectType, int securityInfo,
            IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetEntriesInAcl(int count, ExplicitAccess[] entries, IntPtr oldAcl, out IntPtr newAcl);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSidToSid(string sid, out IntPtr psid);
    }
}
