using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MicVolumeGuard;

/// <summary>Главный класс: иконка в трее + контекстное меню + окно настроек.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly VolumeGuardEngine _engine;
    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private Icon? _currentIcon;

    public TrayApplicationContext()
    {
        // Колбэки WASAPI приходят из чужого потока: гарантируем наличие
        // WindowsFormsSynchronizationContext, чтобы VolumeGuardEngine маршалировал
        // события в UI-поток (AsyncOperationManager использует текущий контекст).
        if (SynchronizationContext.Current == null)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        _settings = AppSettings.Load();
        _engine = new VolumeGuardEngine(_settings);
        _engine.VolumeChangedApplied += OnVolumeApplied;
        _engine.VolumeChangedExternal += OnVolumeExternal;
        _engine.DeviceChanged += OnDeviceChanged;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть настройки...", null, (s, e) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (s, e) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(_settings.VolumePercent),
            Text = "MicVolumeGuard",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.MouseClick += OnTrayClick;
        _tray.DoubleClick += (s, e) => ShowSettings();

        _engine.AttachDevice();

        // Первый запуск: показать окно настроек, чтобы пользователь задал уровень
        if (!File.Exists(AppSettings.GetFilePath()))
        {
            _settings.LockEnabled = true; // по умолчанию блокировка включена
            ShowSettings();
        }
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) ShowSettings();
    }

    private void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, _engine);
            _settingsForm.FormClosed += (s, e) => _settingsForm = null;
        }
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private Icon MakeIcon(int percent) => TrayIconFactory.Create(percent, _settings.LockEnabled);

    private static string Truncate(string s)
    {
        // NotifyIcon.Text: лимит тултипа трея — 63 символа
        s = s.Replace("\n", " ");
        return s.Length <= 63 ? s : s[..63];
    }

    private void UpdateTray(int percent, bool muted)
    {
        var old = _currentIcon;
        _currentIcon = MakeIcon(percent);
        _tray.Icon = _currentIcon;
        old?.Dispose();
        string state = _settings.LockEnabled ? $"🔒 блок: {percent}%" : $"{percent}%";
        if (muted) state += " (mute)";
        _tray.Text = Truncate($"MicVolumeGuard — {state} — {_engine.CurrentDeviceName}");
    }

    private void OnVolumeApplied(int percent, bool muted) => UpdateTray(percent, muted);

    private void OnVolumeExternal(int percent, bool muted)
    {
        // Событие «кто-то изменил» — UpdateTray вызовется после отката, но обновим на всякий случай
        UpdateTray(percent, muted);
    }

    private void OnDeviceChanged(string name)
    {
        _tray.Text = Truncate($"MicVolumeGuard — {name}");
    }

    private void ExitApp()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _engine.Dispose();
        Application.Exit();
    }
}
