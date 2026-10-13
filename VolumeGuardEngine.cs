using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MicVolumeGuard;

/// <summary>
/// Движок блокировки громкости микрофона:
///  - мгновенный откат изменений через IAudioEndpointVolumeCallback;
///  - watchdog на случай уведомлений, «съеденных» системой или программой с ручной установкой.
/// </summary>
internal sealed class VolumeGuardEngine : IDisposable
{
    private readonly MicrophoneController _mic = new();
    private readonly System.Windows.Forms.Timer _watchdog;
    private readonly SynchronizationContext? _ui;
    private AppSettings _settings;
    private bool _applying;        // защита от эхо-цикла (своё применение -> колбэк -> применение)
    private int _lastSeenPercent = -1;
    private bool _lastSeenMute;

    /// <summary>true, если последнее изменение было отброшено (восстановлено) движком.</summary>
    public event Action<int, bool>? VolumeChangedExternal;   // (проценты, mute) — от чужого изменения
    public event Action<int, bool>? VolumeChangedApplied;    // (проценты, mute) — после отката/установки
    public event Action<string>? DeviceChanged;              // имя устройства/ошибка

    public VolumeGuardEngine(AppSettings settings)
    {
        _settings = settings;
        _ui = SynchronizationContext.Current;
        _watchdog = new System.Windows.Forms.Timer { Interval = Math.Max(500, settings.WatchdogIntervalMs) };
        _watchdog.Tick += OnWatchdogTick;
        _mic.VolumeChanged += OnVolumeChanged;
    }

    public AppSettings Settings => _settings;

    public void UpdateSettings(AppSettings s)
    {
        _settings = s;
        _watchdog.Interval = Math.Max(500, s.WatchdogIntervalMs);
        if (s.LockEnabled) ApplyTarget();
    }

    public string CurrentDeviceName => _mic.FriendlyName;

    public void AttachDevice()
    {
        try
        {
            _mic.Attach(_settings.DeviceId);
            _lastSeenPercent = -1;
            _lastSeenMute = _mic.GetMute();
            if (_settings.LockEnabled) ApplyTarget();
            _watchdog.Start();
            Raise(DeviceChanged, _mic.FriendlyName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AttachDevice failed: {ex.Message}");
            Raise(DeviceChanged, "Ошибка подключения к микрофону");
        }
    }

    /// <summary>Принудительно применить целевые значения (громкость и, если нужно, mute).</summary>
    public void ApplyTarget()
    {
        if (_mic.DeviceId == "") return;
        _applying = true;
        try
        {
            float target = Math.Clamp(_settings.VolumePercent, 0, 100) / 100f;
            _mic.SetVolume(target);
            if (_settings.LockMute)
                _mic.SetMute(_settings.LockMuteValue);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ApplyTarget failed: {ex.Message}");
        }
        finally
        {
            _applying = false;
            _lastSeenPercent = _settings.VolumePercent;
            try { _lastSeenMute = _settings.LockMute ? _settings.LockMuteValue : _mic.GetMute(); }
            catch { /* устройство могло исчезнуть */ }
            Raise(VolumeChangedApplied, _lastSeenPercent, _lastSeenMute);
        }
    }

    private void OnVolumeChanged(float fraction, bool muted)
    {
        int percent = (int)Math.Round(fraction * 100f);
        if (_applying)
        {
            // Своё собственное применение — просто фиксируем
            _lastSeenPercent = percent;
            _lastSeenMute = muted;
            return;
        }
        bool changed = percent != _lastSeenPercent || muted != _lastSeenMute;
        if (!changed) return;
        _lastSeenPercent = percent;
        _lastSeenMute = muted;
        Raise(VolumeChangedExternal, percent, muted);
        if (!_settings.LockEnabled) return;
        // Откат к целевому уровню
        if (percent != Math.Clamp(_settings.VolumePercent, 0, 100) || (_settings.LockMute && muted != _settings.LockMuteValue))
        {
            ApplyTarget();
        }
    }

    private void OnWatchdogTick(object? sender, EventArgs e)
    {
        try
        {
            if (_mic.DeviceId == "" || !_settings.LockEnabled) return;
            float cur = _mic.GetVolume();
            int percent = (int)Math.Round(cur * 100f);
            bool mute = _mic.GetMute();
            if (percent != Math.Clamp(_settings.VolumePercent, 0, 100) || (_settings.LockMute && mute != _settings.LockMuteValue))
            {
                // Кто-то изменил мимо колбэка — восстанавливаем
                Raise(VolumeChangedExternal, percent, mute);
                ApplyTarget();
            }
        }
        catch { }
    }

    private void Raise(Action<int, bool>? evt, int a, bool b)
    {
        if (evt == null) return;
        if (_ui != null) _ui.Post(_ => evt(a, b), null);
        else evt(a, b);
    }

    private void Raise(Action<string>? evt, string msg)
    {
        if (evt == null) return;
        if (_ui != null) _ui.Post(_ => evt(msg), null);
        else evt(msg);
    }

    public void Dispose()
    {
        _watchdog.Stop();
        _watchdog.Dispose();
        _mic.Dispose();
    }
}
