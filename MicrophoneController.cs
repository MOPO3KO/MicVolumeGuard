using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MicVolumeGuard;

/// <summary>Обёртка над одним микрофоном (endpoint): громкость, mute, подписка на изменения.</summary>
internal sealed class MicrophoneController : IDisposable
{
    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private object? _endpointObj;           // IAudioEndpointVolume как object (для Activator.CreateInstance-надёжности)
    private VolumeCallback? _callback;
    private bool _disposed;

    public string DeviceId { get; private set; } = "";
    public string FriendlyName { get; private set; } = "";

    /// <summary>Вызывается на каждое изменение громкости/mute (в т.ч. из других программ).</summary>
    public event Action<float, bool>? VolumeChanged;

    public MicrophoneController()
    {
        _enumerator = CreateEnumerator();
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        // Type.GetTypeFromCLSID + Activator.CreateInstance создаёт RCW с IUnknown;
        // приведение к COM-интерфейсу через cast работает для ComImport-типов.
        var clsid = new Guid(CoreAudioGuids.MMDeviceEnumerator);
        var type = Type.GetTypeFromCLSID(clsid)!;
        var obj = Activator.CreateInstance(type)!;
        return (IMMDeviceEnumerator)obj;
    }

    /// <summary>Список активных микрофонов в системе.</summary>
    public static List<(string Id, string Name)> ListCaptureDevices()
    {
        var result = new List<(string, string)>();
        try
        {
            var enumerator = CreateEnumerator();
            int hr = enumerator.EnumAudioEndpoints(CoreAudioConstants.eCapture, CoreAudioConstants.DEVICE_STATE_ACTIVE, out var collection);
            Marshal.ThrowExceptionForHR(hr);
            hr = collection.GetCount(out uint count);
            Marshal.ThrowExceptionForHR(hr);
            for (uint i = 0; i < count; i++)
            {
                hr = collection.Item(i, out var dev);
                if (hr != 0) continue;
                var (id, name) = GetDeviceInfo(dev);
                if (!string.IsNullOrEmpty(id)) result.Add((id, name));
            }
        }
        catch { /* нет микрофонов или COM-сбой */ }
        return result;
    }

    private static (string Id, string Name) GetDeviceInfo(IMMDevice dev)
    {
        try
        {
            int hr = dev.GetId(out string id);
            if (hr != 0) return ("", "");
            hr = dev.OpenPropertyStore(CoreAudioNative.STGM_READ, out var store);
            if (hr != 0) return (id, "(без имени)");
            var key = new CoreAudioNative.PropertyKey
            {
                fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), // PKEY_Device_FriendlyName fmtid
                pid = CoreAudioNative.PKEY_Device_FriendlyName_Pid
            };
            hr = store.GetValue(ref key, out var pv);
            string name = "(без имени)";
            if (hr == 0 && pv.vt == 31 /* VT_LPWSTR */ && pv.p != IntPtr.Zero)
            {
                name = Marshal.PtrToStringUni(pv.p) ?? name;
                CoTaskMemFreeIfNeeded(pv.p);
            }
            return (id, name);
        }
        catch { return ("", ""); }
    }

    private static void CoTaskMemFreeIfNeeded(IntPtr p) { /* PROPVARIANT со строкой из property store освобождать не требуется: IPropertyStore сама владеет памятью */ }

    /// <summary>Подключиться к микрофону по id (или к устройству по умолчанию, если id пуст).</summary>
    public void Attach(string deviceId = "")
    {
        Detach();
        _device = null;
        if (string.IsNullOrEmpty(deviceId))
        {
            int hr = _enumerator!.GetDefaultAudioEndpoint(CoreAudioConstants.eCapture, CoreAudioConstants.eCommunications, out var def);
            if (hr != 0)
                hr = _enumerator.GetDefaultAudioEndpoint(CoreAudioConstants.eCapture, CoreAudioConstants.eMultimedia, out def);
            Marshal.ThrowExceptionForHR(hr);
            _device = def;
        }
        else
        {
            // IMDeviceEnumerator не имеет OpenById в нашей обёртке — добавим через отдельный ComImport
            _device = OpenDeviceById(deviceId);
        }
        DeviceId = deviceId;
        var (id, name) = GetDeviceInfo(_device);
        DeviceId = id;
        FriendlyName = name;
        ActivateEndpoint();
        RegisterCallback();
    }

    private IMMDevice OpenDeviceById(string deviceId)
    {
        // GetDevice есть у того же enumerator'а — используем расширенную обёртку
        var clsid = new Guid(CoreAudioGuids.MMDeviceEnumerator);
        var type = Type.GetTypeFromCLSID(clsid)!;
        var obj = Activator.CreateInstance(type)!;
        var full = (IMMDeviceEnumeratorRegister)obj;
        int hr = full.GetDevice(deviceId, out var dev);
        Marshal.ThrowExceptionForHR(hr);
        return dev;
    }

    private void ActivateEndpoint()
    {
        var iidEndpoint = new Guid(CoreAudioGuids.IAudioEndpointVolume);
        int hr = _device!.Activate(ref iidEndpoint, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out _endpointObj);
        Marshal.ThrowExceptionForHR(hr);
    }

    private void RegisterCallback()
    {
        _callback = new VolumeCallback(this);
        int hr = Endpoint.RegisterControlChangeNotify(_callback);
        Marshal.ThrowExceptionForHR(hr);
    }

    public void Detach()
    {
        if (_callback != null && _endpointObj != null)
        {
            try { Endpoint.UnregisterControlChangeNotify(_callback); } catch { }
        }
        _callback = null;
        _endpointObj = null;
        _device = null;
        DeviceId = "";
        FriendlyName = "";
    }

    private IAudioEndpointVolume Endpoint => (IAudioEndpointVolume)_endpointObj!;

    public float GetVolume()
    {
        int hr = Endpoint.GetMasterVolumeLevelScalar(out float v);
        Marshal.ThrowExceptionForHR(hr);
        return v;
    }

    public bool GetMute()
    {
        int hr = Endpoint.GetMute(out bool m);
        Marshal.ThrowExceptionForHR(hr);
        return m;
    }

    private static readonly Guid NoEventCtx = new("00000000-0000-0000-0000-000000000000");

    public void SetVolume(float fraction)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        var ctx = Guid.Empty;
        int hr = Endpoint.SetMasterVolumeLevelScalar(fraction, ref ctx);
        Marshal.ThrowExceptionForHR(hr);
    }

    public void SetMute(bool mute)
    {
        var ctx = Guid.Empty;
        int hr = Endpoint.SetMute(mute, ref ctx);
        Marshal.ThrowExceptionForHR(hr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _enumerator = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Внутренний колбэк COM. Вызывается из потока WASAPI — нельзя блокировать надолго.</summary>
    private sealed class VolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly MicrophoneController _owner;
        public VolumeCallback(MicrophoneController owner) { _owner = owner; }

        public int OnNotify(IntPtr pNotify)
        {
            try
            {
                // AUDIO_VOLUME_NOTIFICATION_DATA: DWORD nVolumeMutexCount? Нет:
                // struct { GUID guidEventContext; BOOL bMuted; float fMasterVolume; UINT nChannels; float afChannelVolumes[1]; }
                var ctx = Marshal.PtrToStructure<NotifyData>(pNotify);
                _owner.VolumeChanged?.Invoke(ctx.fMasterVolume, ctx.bMuted != 0);
            }
            catch { /* не роняем COM-поток */ }
            return 0; // S_OK
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyData
    {
        public Guid guidEventContext;
        public int bMuted;
        public float fMasterVolume;
        public uint nChannels;
        public float afChannelVolumes; // первый канал (нас интересует только master)
    }
}
