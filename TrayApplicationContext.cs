using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MicVolumeGuard;

/// <summary>Главный класс: иконка в трее + контекстное меню + окно настроек.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly VolumeGuardEngine _engine;
    private readonly Control _uiHost;      // скрытое окно: весь доступ к NotifyIcon — только из UI-потока
    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private ToolStripMenuItem? _miLock;
    private IntPtr _lastHicon = IntPtr.Zero;
    private int _lastTrayPercent;
    private bool _lastTrayMute;

    public TrayApplicationContext()
    {
        // Скрытый хост создаётся на UI-потоке; хэндл нужен сразу, чтобы
        // InvokeRequired корректно работал из любых потоков (WASAPI и др.)
        _uiHost = new Control();
        _ = _uiHost.Handle;

        _settings = AppSettings.Load();
        Logger.Configure(_settings.LogEnabled, _settings.LogVerbose);
        Logger.Log($"Запуск MicVolumeGuard (лог: {_settings.LogEnabled}, подробно: {_settings.LogVerbose})");
        _engine = new VolumeGuardEngine(_settings);
        // Доставка событий движка — через _uiHost (см. OnUI): детерминированный
        // BeginInvoke вместо хрупкого маршрута через SynchronizationContext
        _engine.SetMarshaller(OnUI);

        // События движка могут приходить из потоков WASAPI: каждый обработчик
        // переносим в UI-поток через _uiHost — NotifyIcon допускает обращение
        // только из потока, создавшего его окно (иначе обновления молча теряются).
        _engine.VolumeChangedApplied += (p, m) => OnUI(() => OnVolumeApplied(p, m));
        _engine.VolumeChangedExternal += (p, m) => OnUI(() => OnVolumeExternal(p, m));
        _engine.DeviceChanged += name => OnUI(() => OnDeviceChanged(name));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть настройки...", null, (s, e) => ShowSettings());
        // Быстрое переключение блокировки прямо из трея
        _miLock = new ToolStripMenuItem("Блокировка: вкл") { Checked = _settings.LockEnabled };
        _miLock.Click += (s, e) =>
        {
            _settings.LockEnabled = !_settings.LockEnabled;
            _miLock.Checked = _settings.LockEnabled;
            _miLock.Text = _settings.LockEnabled ? "Блокировка: вкл" : "Блокировка: выкл";
            if (_settings.LockEnabled) _engine.ApplyTarget(); // сразу вернуть уровень
            _settings.Save();
            Logger.Log($"Блокировка переключена из трея: {_settings.LockEnabled}");
            UpdateTray(_lastTrayPercent, _lastTrayMute); // цвет иконки зависит от блокировки
        };
        menu.Items.Add(_miLock);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (s, e) => ExitApp());

        _tray = new NotifyIcon
        {
            Text = "MicVolumeGuard",
            ContextMenuStrip = menu,
            Visible = false
        };
        // Иконка выставляется до Visible — трей сразу показывает актуальный уровень
        UpdateTray(_settings.VolumePercent, false);
        _tray.Visible = true;
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
            // Прямое обновление трея сразу после «Применить»/смены устройства —
            // не дожидаясь событий движка (гарантия: иконка меняется всегда)
            _settingsForm.SettingsApplied += OnSettingsApplied;
            _settingsForm.FormClosed += (s, e) => _settingsForm = null;
        }
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnSettingsApplied()
    {
        OnUI(() =>
        {
            SyncLockMenu();
            UpdateTray(_settings.VolumePercent, _lastTrayMute);
        });
    }

    /// <summary>Выполняет action в UI-потоке (безопасно вызывать из любого потока).</summary>
    private void OnUI(Action action)
    {
        if (_uiHost.IsDisposed) return;
        if (_uiHost.InvokeRequired)
        {
            try { _uiHost.BeginInvoke(action); }
            catch (ObjectDisposedException) { /* приложение завершается */ }
        }
        else action();
    }

    /// <summary>
    /// Заменяет иконку трея. Icon.FromHandle не владеет HICON — предыдущий
    /// хэндл уничтожаем сами (DestroyIcon), иначе GDI-хэндлы утекают и
    /// со временем иконка перестаёт обновляться до перезапуска.
    /// </summary>
    private void SetTrayIcon(int percent)
    {
        var old = _lastHicon;
        var icon = TrayIconFactory.Create(percent, _settings.LockEnabled);
        _tray.Icon = icon;
        _lastHicon = icon.Handle;
        if (old != IntPtr.Zero) DestroyIcon(old);
    }

    private static string Truncate(string s)
    {
        // NotifyIcon.Text: лимит тултипа трея — 63 символа
        s = s.Replace("\n", " ");
        return s.Length <= 63 ? s : s[..63];
    }

    private void UpdateTray(int percent, bool muted)
    {
        _lastTrayPercent = percent;
        _lastTrayMute = muted;
        SetTrayIcon(percent);
        string state = _settings.LockEnabled ? $"🔒 блок: {percent}%" : $"{percent}%";
        if (muted) state += " (mute)";
        _tray.Text = Truncate($"MicVolumeGuard — {state} — {_engine.CurrentDeviceName}");
    }

    private void OnVolumeApplied(int percent, bool muted)
    {
        SyncLockMenu();
        UpdateTray(percent, muted);
    }

    /// <summary>Синхронизирует пункт «Блокировка» в меню трея с актуальным состоянием.</summary>
    private void SyncLockMenu()
    {
        if (_miLock == null) return;
        _miLock.Checked = _settings.LockEnabled;
        _miLock.Text = _settings.LockEnabled ? "Блокировка: вкл" : "Блокировка: выкл";
    }

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
        if (_lastHicon != IntPtr.Zero)
        {
            DestroyIcon(_lastHicon);
            _lastHicon = IntPtr.Zero;
        }
        _uiHost.Dispose();
        _engine.Dispose();
        Application.Exit();
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
