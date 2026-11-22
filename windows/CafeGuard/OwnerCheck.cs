using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace CafeGuard
{
    public enum VerifyOutcome { Verified, Refused, Unavailable }
    public enum OffDecision { TurnOff, StayOn, LockInstead }

    /// <summary>
    /// Turn OFF needs the owner: Windows' own "Verify it's you" check (PIN, fingerprint, face).
    /// If that check can't run on this laptop, the screen is locked instead and unlocking turns CafeGuard OFF.
    /// </summary>
    public static class OwnerCheck
    {
        public static OffDecision Decide(VerifyOutcome outcome) => outcome switch
        {
            VerifyOutcome.Verified => OffDecision.TurnOff,
            VerifyOutcome.Refused => OffDecision.StayOn,
            _ => OffDecision.LockInstead
        };

        /// <summary>Windows' UserConsentVerificationResult number → what it means for us.</summary>
        public static VerifyOutcome FromWindowsResult(int result) => result switch
        {
            0 => VerifyOutcome.Verified,
            4 or 5 or 6 => VerifyOutcome.Refused,      // busy, too many tries, cancelled
            _ => VerifyOutcome.Unavailable             // no PIN/fingerprint set up, turned off by policy, unknown
        };

        /// <summary>
        /// Shows "Verify it's you" for <paramref name="window"/> and waits for the answer. Blocking: call off the UI thread.
        /// Talks to Windows directly (no extra packages): UserConsentVerifier via IUserConsentVerifierInterop.
        /// </summary>
        public static VerifyOutcome AskWindows(IntPtr window, string message, TimeSpan timeout)
        {
            IntPtr className = IntPtr.Zero, text = IntPtr.Zero, factoryPtr = IntPtr.Zero, operationPtr = IntPtr.Zero;
            try
            {
                const string cls = "Windows.Security.Credentials.UI.UserConsentVerifier";
                Check(WindowsCreateString(cls, cls.Length, out className));
                Check(WindowsCreateString(message, message.Length, out text));

                Guid interopId = typeof(IUserConsentVerifierInterop).GUID;
                Check(RoGetActivationFactory(className, ref interopId, out factoryPtr));
                var interop = (IUserConsentVerifierInterop)Marshal.GetObjectForIUnknown(factoryPtr);

                Guid operationId = typeof(IAsyncOperationOfResult).GUID;
                Check(interop.RequestVerificationForWindowAsync(window, text, ref operationId, out operationPtr));
                var operation = (IAsyncOperationOfResult)Marshal.GetObjectForIUnknown(operationPtr);
                var info = (IAsyncInfo)operation;

                DateTime until = DateTime.UtcNow + timeout;
                int status;
                while (true)
                {
                    Check(info.get_Status(out status));
                    if (status != 0) break; // 0 = still waiting for the user
                    if (DateTime.UtcNow > until) { info.Cancel(); return VerifyOutcome.Refused; }
                    Thread.Sleep(100);
                }
                if (status != 1) return status == 2 ? VerifyOutcome.Refused : VerifyOutcome.Unavailable; // 2 = cancelled, 3 = error

                Check(operation.GetResults(out int result));
                return FromWindowsResult(result);
            }
            catch (Exception ex)
            {
                AppLog.Write($"Windows verification could not run: {ex.Message}");
                return VerifyOutcome.Unavailable;
            }
            finally
            {
                if (operationPtr != IntPtr.Zero) Marshal.Release(operationPtr);
                if (factoryPtr != IntPtr.Zero) Marshal.Release(factoryPtr);
                if (text != IntPtr.Zero) WindowsDeleteString(text);
                if (className != IntPtr.Zero) WindowsDeleteString(className);
            }
        }

        private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }

        [DllImport("combase.dll", CharSet = CharSet.Unicode)]
        private static extern int WindowsCreateString(string source, int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

        [ComImport, Guid("39E050C3-4E74-441A-8DC0-B81104DF949C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUserConsentVerifierInterop
        {
            void GetIids(out int count, out IntPtr ids);
            void GetRuntimeClassName(out IntPtr name);
            void GetTrustLevel(out int level);
            [PreserveSig] int RequestVerificationForWindowAsync(IntPtr appWindow, IntPtr message, ref Guid riid, out IntPtr asyncOperation);
        }

        [ComImport, Guid("00000036-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAsyncInfo
        {
            void GetIids(out int count, out IntPtr ids);
            void GetRuntimeClassName(out IntPtr name);
            void GetTrustLevel(out int level);
            [PreserveSig] int get_Id(out uint id);
            [PreserveSig] int get_Status(out int status);
            [PreserveSig] int get_ErrorCode(out int errorCode);
            [PreserveSig] int Cancel();
            [PreserveSig] int Close();
        }

        /// <summary>IAsyncOperation&lt;UserConsentVerificationResult&gt;; id = WinRtIid.ForSignature(ResultOperationSignature).</summary>
        [ComImport, Guid("fd596ffd-2318-558f-9dbe-d21df43764a5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAsyncOperationOfResult
        {
            void GetIids(out int count, out IntPtr ids);
            void GetRuntimeClassName(out IntPtr name);
            void GetTrustLevel(out int level);
            [PreserveSig] int put_Completed(IntPtr handler);
            [PreserveSig] int get_Completed(out IntPtr handler);
            [PreserveSig] int GetResults(out int result);
        }

        internal const string ResultOperationSignature =
            "pinterface({9fc2b0bb-e446-44e2-aa61-9cab8f636af2};enum(Windows.Security.Credentials.UI.UserConsentVerificationResult;i4))";
    }

    /// <summary>Windows' rule for the id of a generic interface such as IAsyncOperation&lt;T&gt; (name-based, SHA-1).</summary>
    public static class WinRtIid
    {
        private static readonly byte[] Namespace = Convert.FromHexString("11f47ad57b7342c0abae878b1e16adee");

        public static Guid ForSignature(string signature)
        {
            byte[] input = new byte[Namespace.Length + Encoding.UTF8.GetByteCount(signature)];
            Namespace.CopyTo(input, 0);
            Encoding.UTF8.GetBytes(signature, 0, signature.Length, input, Namespace.Length);
            byte[] hash = SHA1.HashData(input);
            hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            // Hash bytes are big-endian; Guid wants the first three fields little-endian.
            return new Guid(
                (hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3],
                (short)((hash[4] << 8) | hash[5]),
                (short)((hash[6] << 8) | hash[7]),
                hash[8], hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]);
        }
    }
}
