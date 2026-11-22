using System.Runtime.InteropServices;

namespace CafeGuard.Shared
{
    /// <summary>
    /// Is this copy the Microsoft Store version? Store apps run inside a "package", and a few things work
    /// differently there (Windows keeps private copies of some settings, and nothing runs on uninstall).
    /// </summary>
    public static class PackageInfo
    {
        private const int AppModelErrorNoPackage = 15700;
        private const int ErrorInsufficientBuffer = 122;

        public static bool IsPackaged { get; } = Detect();

        private static bool Detect()
        {
            try
            {
                int length = 0;
                int result = GetCurrentPackageFullName(ref length, null);
                return result != AppModelErrorNoPackage && (result == ErrorInsufficientBuffer || result == 0);
            }
            catch
            {
                return false; // very old Windows without this function: never a Store package
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
    }
}
