using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MicVolumeGuard;

/// <summary>Идентификаторы интерфейсов Core Audio API (проверены по headers mmdeviceapi.idl / audioclient.idl).</summary>
internal static class CoreAudioGuids
{
    public const string MMDeviceEnumerator = "BCDE0395-E52F-467C-8E3D-C4579291692E"; // CLSID_MMDeviceEnumerator
    public const string IMMDeviceEnumerator = "A95664D2-9614-4F35-A746-DE8DB63617E6";
    public const string IMMDeviceCollection = "0BD7A1BE-7A1A-44DB-8397-CC5392387B5E";
    public const string IMMDevice = "D996753C-704E-4FCB-8C04-A5B2F8AFBE36";
    public const string IAudioEndpointVolume = "5CDF2C82-841E-4546-9722-0CF74078229A";
    public const string IAudioEndpointVolumeCallback = "657804FA-D6AD-4496-B881-54973AC43383";
    public const string IPropertyStore = "886d8eeb-8cf2-4446-8d02-cdba1dbdcf99";
    public const string IMMNotificationClient = "7991EEC9-7E89-4D85-8390-6C703CEC60C0";
}

internal static class CoreAudioNative
{
    [DllImport("ole32.dll", PreserveSig = false)]
    public static extern void CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    [DllImport("propsys.dll", PreserveSig = false)]
    public static extern void PSPropertyKeyFromString([MarshalAs(UnmanagedType.LPWStr)] string psz, out PropertyKey key);

    public const uint CLSCTX_ALL = 0x17;
    public const uint STGM_READ = 0x00000000;

    // PROPERTYKEY данных устройства
    public const uint PKEY_Device_FriendlyName_FmtId = 0xa45c254e;
    public const int PKEY_Device_FriendlyName_Pid = 14;

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1, wReserved2, wReserved3;
        public IntPtr p;       // union: lpwstr / blob / etc.
        public int int1, int2; // для разных типов
    }
}

/// <summary>Callback уровня громкости конечной точки (микрофона).</summary>
[ComImport]
[Guid(CoreAudioGuids.IAudioEndpointVolumeCallback)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig]
    int OnNotify(IntPtr pNotify); // AUDIO_VOLUME_NOTIFICATION_DATA*
}

[ComImport]
[Guid(CoreAudioGuids.IAudioEndpointVolume)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
    [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
    [PreserveSig] int GetChannelCount(out uint pnChannelCount);
    [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
    [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
    [PreserveSig] int SetMute(bool bMute, ref Guid pguidEventContext);
    [PreserveSig] int GetMute(out bool pbMute);
    // дальше методы GetVolumeStepInfo/VolumeStepUp/Down/QueryHardwareSupport/GetVolumeRange не нужны
}

[ComImport]
[Guid(CoreAudioGuids.IPropertyStore)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint cProps);
    [PreserveSig] int GetAt(uint iProp, out CoreAudioNative.PropertyKey pkey);
    [PreserveSig] int GetValue(ref CoreAudioNative.PropertyKey key, out CoreAudioNative.PROPVARIANT pv);
    [PreserveSig] int SetValue(ref CoreAudioNative.PropertyKey key, ref CoreAudioNative.PROPVARIANT pv);
    [PreserveSig] int Commit();
}

[ComImport]
[Guid(CoreAudioGuids.IMMDevice)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig] int GetState(out uint pdwState);
}

[ComImport]
[Guid(CoreAudioGuids.IMMDeviceCollection)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint pcDevices);
    [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
}

[ComImport]
[Guid(CoreAudioGuids.IMMDeviceEnumerator)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(uint dataFlow, uint dwStateMask, out IMMDeviceCollection ppDevices);
    [PreserveSig] int GetDefaultAudioEndpoint(uint dataFlow, uint role, out IMMDevice ppEndpoint);
    // остальные методы не нужны
}

internal static class CoreAudioConstants
{
    public const uint eCapture = 1;          // data flow: capture (микрофоны)
    public const uint DEVICE_STATE_ACTIVE = 0x1;
    public const uint eCommunications = 0;   // роль: коммуникации (то, что используется звонилками)
    public const uint eMultimedia = 1;       // роль: мультимедиа
}

[ComImport]
[Guid(CoreAudioGuids.IMMNotificationClient)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, uint dwNewState);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    [PreserveSig] int OnDefaultDeviceChanged(uint dataFlow, uint role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, CoreAudioNative.PropertyKey key);
}

[ComImport]
[Guid(CoreAudioGuids.IMMDeviceEnumerator)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumeratorRegister
{
    [PreserveSig] int EnumAudioEndpoints(uint dataFlow, uint dwStateMask, out IMMDeviceCollection ppDevices);
    [PreserveSig] int GetDefaultAudioEndpoint(uint dataFlow, uint role, out IMMDevice ppEndpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
}
