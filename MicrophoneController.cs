using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace MicVolumeGuard;

/// <summary>
/// Обёртка над одним микрофоном (endpoint) на базе NAudio (MMDevice API):
/// громкость, mute, подписка на изменения. NAudio сам корректно выполняет
/// все COM-вызовы к Core Audio API.
/// </summary>
internal sealed class MicrophoneController : IDisposable
{
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private bool _disposed;

    public string DeviceId { get; private set; } = "";
    public string FriendlyName { get; private set; } = "";

    /// <summary>Вызывается на каждое изменение громкости/mute (в т.ч. из других программ).</summary>
    public event Action<float, bool>? VolumeChanged;

    public MicrophoneController()
    {
        _enumerator = new MMDeviceEnumerator();
    }

    /// <summary>Список активных микрофонов в системе.</summary>
    public static List<(string Id, string Name)> ListCaptureDevices()
    {
        var result = new List<(string, string)>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var dev in devices)
            {
                try
                {
                    string id = dev.ID;
                    // «Микрофон (fifine  Microphone)» — двойные пробелы внутри скобок часто приходят из драйвера;
                    // схлопываем их для аккуратного отображения
                    string name = System.Text.RegularExpressions.Regex.Replace(dev.FriendlyName, @"\s{2,}", " ");
                    result.Add((id, name));
                    dev.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log($"ListCaptureDevices: устройство пропущено: {ex.Message}");
                }
            }
            Logger.Log($"ListCaptureDevices: найдено {result.Count} активных микрофонов");
        }
        catch (Exception ex)
        {
            Logger.Log($"ListCaptureDevices ОШИБКА: {ex.GetType().Name}: {ex.Message}");
        }
        return result;
    }

    /// <summary>Подключиться к микрофону по id (или к устройству по умолчанию, если id пуст).</summary>
    public void Attach(string deviceId = "")
    {
        Detach();
        try
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                // Сначала «связь» (то, что используют звонилки), затем «мультимедиа»
                try
                {
                    _device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                }
                catch
                {
                    _device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                }
            }
            else
            {
                _device = _enumerator!.GetDevice(deviceId);
            }

            DeviceId = _device.ID;
            FriendlyName = _device.FriendlyName;
            Logger.Log($"Attach: подключено '{FriendlyName}' ({DeviceId})");
            _device.AudioEndpointVolume.OnVolumeNotification += OnEndpointVolumeNotification;
        }
        catch (Exception ex)
        {
            DeviceId = "";
            FriendlyName = "";
            _device = null;
            Logger.Log($"Attach ОШИБКА (id='{deviceId}'): {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private void OnEndpointVolumeNotification(AudioVolumeNotificationData data)
    {
        try
        {
            // Сигнатура делегата NAudio: void(AudioVolumeNotificationData data),
            // data.MasterVolume — scalar 0..1, data.Muted — состояние mute
            VolumeChanged?.Invoke(data.MasterVolume, data.Muted);
        }
        catch { /* не роняем поток WASAPI */ }
    }

    public void Detach()
    {
        if (_device != null)
        {
            try { _device.AudioEndpointVolume.OnVolumeNotification -= OnEndpointVolumeNotification; } catch { }
        }
        _device = null;
        DeviceId = "";
        FriendlyName = "";
    }

    private MMDevice Device => _device ?? throw new InvalidOperationException("Микрофон не подключён");

    public float GetVolume()
    {
        try { return Device.AudioEndpointVolume.MasterVolumeLevelScalar; }
        catch (Exception ex) { Logger.Log($"GetVolume ОШИБКА: {ex.Message}"); throw; }
    }

    public bool GetMute()
    {
        try { return Device.AudioEndpointVolume.Mute; }
        catch (Exception ex) { Logger.Log($"GetMute ОШИБКА: {ex.Message}"); throw; }
    }

    public void SetVolume(float fraction)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        try
        {
            Device.AudioEndpointVolume.MasterVolumeLevelScalar = fraction;
            Logger.Debug($"SetVolume({Math.Round(fraction * 100)}%) OK");
        }
        catch (Exception ex)
        {
            Logger.Log($"SetVolume({Math.Round(fraction * 100)}%) ОШИБКА: {ex.Message}");
            throw;
        }
    }

    public void SetMute(bool mute)
    {
        try
        {
            Device.AudioEndpointVolume.Mute = mute;
            Logger.Debug($"SetMute({mute}) OK");
        }
        catch (Exception ex)
        {
            Logger.Log($"SetMute({mute}) ОШИБКА: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _device?.Dispose();
        _enumerator = null;
        GC.SuppressFinalize(this);
    }
}
