using System;
using System.Runtime.InteropServices;

namespace CafeGuard
{
    public class AudioDeviceSnapshot
    {
        public string DefaultDeviceId { get; set; } = string.Empty;
        public string DefaultDeviceName { get; set; } = "Unknown Speaker";
        public int ActiveDeviceCount { get; set; }
        public float OriginalVolume { get; set; } = 0.5f;
        public bool OriginalMute { get; set; } = false;
    }

    public enum AudioCheckResult
    {
        /// <summary>Sound still goes to the speakers saved when the guard was turned on.</summary>
        Ok,
        /// <summary>Sound had moved to another device (e.g. Bluetooth earbuds). It was moved back. No alarm.</summary>
        MovedBackToSpeakers,
        /// <summary>The speakers were cut off, usually by headphones plugged into the laptop's own jack. Alarm.</summary>
        SpeakersCutOff
    }

    /// <summary>
    /// Talks to the Windows sound system (CoreAudio).
    /// Every COM object is released after use, so calling this many times a second does not leak memory.
    /// </summary>
    public static class CoreAudioController
    {
        private const uint DEVICE_STATE_ACTIVE = 0x00000001;
        private const uint CLSCTX_ALL = 23;
        private const uint STGM_READ = 0x00000000;
        private const ushort VT_LPWSTR = 31;

        private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
        private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IMMDeviceCollection ppDevices);
            [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint pcDevices);
            [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            [PreserveSig] int GetState(out uint pdwState);
        }

        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint cProps);
            [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant propvar);
            [PreserveSig] int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
        }

        // Full size of a Windows PROPVARIANT (24 bytes on 64-bit), so Windows never writes past the end.
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pszVal;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int GetChannelCount(out uint pnChannelCount);
            [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        // Undocumented-but-stable Windows interface used to set the default playback device.
        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class PolicyConfigClientComObject { }

        [Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, IntPtr ppFormat);
            [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bVisible);
        }

        // One enumerator for the whole app (created and used on the UI thread only).
        private static IMMDeviceEnumerator? _enumerator;
        private static IMMDeviceEnumerator Enumerator =>
            _enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

        private static void Release(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
                Marshal.ReleaseComObject(comObject);
        }

        // ---------- Small helpers ----------

        private static IMMDevice? GetDefaultDevice()
        {
            return Enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice dev) == 0 ? dev : null;
        }

        private static string GetId(IMMDevice device)
        {
            return device.GetId(out string id) == 0 && id != null ? id : string.Empty;
        }

        private static string GetDeviceFriendlyName(IMMDevice device)
        {
            const string fallback = "Audio Output";
            IPropertyStore? store = null;
            try
            {
                if (device.OpenPropertyStore(STGM_READ, out store) != 0 || store == null) return fallback;

                var friendlyNameKey = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
                if (store.GetValue(ref friendlyNameKey, out PropVariant prop) != 0) return fallback;
                try
                {
                    if (prop.vt == VT_LPWSTR && prop.pszVal != IntPtr.Zero)
                        return Marshal.PtrToStringUni(prop.pszVal) ?? fallback;
                }
                finally
                {
                    PropVariantClear(ref prop);
                }
            }
            catch { }
            finally
            {
                Release(store);
            }
            return fallback;
        }

        /// <summary>Runs an action on the device's volume control, then releases it.</summary>
        private static void WithVolume(IMMDevice device, Action<IAudioEndpointVolume> action)
        {
            object? volObj = null;
            try
            {
                Guid iidVol = typeof(IAudioEndpointVolume).GUID;
                if (device.Activate(ref iidVol, CLSCTX_ALL, IntPtr.Zero, out volObj) == 0 && volObj is IAudioEndpointVolume vol)
                    action(vol);
            }
            catch { }
            finally
            {
                Release(volObj);
            }
        }

        private static int CountActiveOutputs()
        {
            IMMDeviceCollection? collection = null;
            try
            {
                if (Enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out collection) == 0 && collection != null
                    && collection.GetCount(out uint count) == 0)
                    return (int)count;
            }
            catch { }
            finally
            {
                Release(collection);
            }
            return 0;
        }

        private static void SetDefaultOutput(string deviceId)
        {
            object? policy = null;
            try
            {
                policy = new PolicyConfigClientComObject();
                var config = (IPolicyConfig)policy;
                config.SetDefaultEndpoint(deviceId, ERole.eConsole);
                config.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                config.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            }
            catch { }
            finally
            {
                Release(policy);
            }
        }

        // ---------- Public API ----------

        /// <summary>Saves which speakers are in use and the current volume, at the moment the guard is turned on.</summary>
        public static AudioDeviceSnapshot TakeSnapshot()
        {
            var snap = new AudioDeviceSnapshot();
            IMMDevice? device = null;
            try
            {
                device = GetDefaultDevice();
                if (device != null)
                {
                    snap.DefaultDeviceId = GetId(device);
                    snap.DefaultDeviceName = GetDeviceFriendlyName(device);
                    WithVolume(device, vol =>
                    {
                        if (vol.GetMasterVolumeLevelScalar(out float level) == 0) snap.OriginalVolume = level;
                        if (vol.GetMute(out bool muted) == 0) snap.OriginalMute = muted;
                    });
                }
                snap.ActiveDeviceCount = CountActiveOutputs();
            }
            catch { }
            finally
            {
                Release(device);
            }
            return snap;
        }

        /// <summary>
        /// Checks that sound will still come out of the saved speakers.
        /// - Another device took over (Bluetooth earbuds in your bag, USB headset): quietly switch back, no alarm.
        /// - The speakers themselves were cut off (headphones in the laptop's jack): alarm.
        /// </summary>
        public static AudioCheckResult CheckAudio(AudioDeviceSnapshot armedSnapshot)
        {
            if (armedSnapshot == null || string.IsNullOrEmpty(armedSnapshot.DefaultDeviceId))
                return AudioCheckResult.Ok;

            IMMDevice? speakers = null;
            IMMDevice? current = null;
            try
            {
                // 1. Are the saved speakers still usable?
                if (Enumerator.GetDevice(armedSnapshot.DefaultDeviceId, out speakers) != 0 || speakers == null
                    || speakers.GetState(out uint state) != 0 || state != DEVICE_STATE_ACTIVE)
                {
                    return AudioCheckResult.SpeakersCutOff;
                }

                // 2. Same device, but it renamed itself (some combo jacks switch "Speakers" to "Headphones").
                string name = GetDeviceFriendlyName(speakers);
                if (!string.Equals(name, armedSnapshot.DefaultDeviceName, StringComparison.OrdinalIgnoreCase))
                    return AudioCheckResult.SpeakersCutOff;

                // 3. Did something else become the default output? Move sound back to the speakers.
                current = GetDefaultDevice();
                if (current != null && !string.Equals(GetId(current), armedSnapshot.DefaultDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    SetDefaultOutput(armedSnapshot.DefaultDeviceId);
                    return AudioCheckResult.MovedBackToSpeakers;
                }
            }
            catch { }
            finally
            {
                Release(current);
                Release(speakers);
            }
            return AudioCheckResult.Ok;
        }

        /// <summary>
        /// Forces sound back to the saved speakers, and sets every active output to 100% and unmuted.
        /// </summary>
        public static void EnforceMaxVolumeAndAntiHijack(AudioDeviceSnapshot? armedSnapshot = null)
        {
            IMMDevice? current = null;
            IMMDeviceCollection? collection = null;
            try
            {
                Guid empty = Guid.Empty;

                if (armedSnapshot != null && !string.IsNullOrEmpty(armedSnapshot.DefaultDeviceId))
                {
                    current = GetDefaultDevice();
                    if (current != null && !string.Equals(GetId(current), armedSnapshot.DefaultDeviceId, StringComparison.OrdinalIgnoreCase))
                        SetDefaultOutput(armedSnapshot.DefaultDeviceId);
                }

                if (Enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out collection) == 0 && collection != null
                    && collection.GetCount(out uint count) == 0)
                {
                    for (uint i = 0; i < count; i++)
                    {
                        if (collection.Item(i, out IMMDevice dev) != 0 || dev == null) continue;
                        try
                        {
                            WithVolume(dev, vol =>
                            {
                                vol.SetMute(false, ref empty);
                                vol.SetMasterVolumeLevelScalar(1.0f, ref empty);
                            });
                        }
                        finally
                        {
                            Release(dev);
                        }
                    }
                }
            }
            catch { }
            finally
            {
                Release(collection);
                Release(current);
            }
        }

        /// <summary>Puts the volume back to a comfortable level after the guard is turned off.</summary>
        public static void RestoreVolume(AudioDeviceSnapshot? snapshot)
        {
            if (snapshot == null) return;
            IMMDevice? device = null;
            try
            {
                device = GetDefaultDevice();
                if (device == null) return;
                Guid empty = Guid.Empty;
                float safeVol = Math.Clamp(snapshot.OriginalVolume, 0.15f, 0.85f);
                WithVolume(device, vol =>
                {
                    vol.SetMasterVolumeLevelScalar(safeVol, ref empty);
                    vol.SetMute(snapshot.OriginalMute, ref empty);
                });
            }
            catch { }
            finally
            {
                Release(device);
            }
        }

        /// <summary>Current volume (0-100), mute and device name. Used by the self-test.</summary>
        public static (int VolumePercent, bool IsMuted, string DeviceName) GetCurrentAudioStatus()
        {
            IMMDevice? device = null;
            try
            {
                device = GetDefaultDevice();
                if (device != null)
                {
                    string name = GetDeviceFriendlyName(device);
                    int pct = 100;
                    bool muted = false;
                    WithVolume(device, vol =>
                    {
                        if (vol.GetMasterVolumeLevelScalar(out float level) == 0) pct = (int)Math.Round(level * 100);
                        if (vol.GetMute(out bool m) == 0) muted = m;
                    });
                    return (pct, muted, name);
                }
            }
            catch { }
            finally
            {
                Release(device);
            }
            return (100, false, "Default Speakers");
        }
    }
}
