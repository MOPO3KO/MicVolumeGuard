using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MicVolumeGuard;

/// <summary>
/// Окно настроек: выбор микрофона, уровень громкости, блокировка, автозапуск, логирование.
/// Вёрстка на TableLayoutPanel — контролы не перекрываются ни при каком DPI/шрифте.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly VolumeGuardEngine _engine;

    private readonly ComboBox _cmbDevice = new();
    private readonly TrackBar _slider = new();
    private readonly NumericUpDown _num = new();
    private readonly CheckBox _chkLock = new();
    private readonly CheckBox _chkLockMute = new();
    private readonly CheckBox _chkMuteValue = new();
    private readonly CheckBox _chkAutostart = new();
    private readonly CheckBox _chkLog = new();
    private readonly CheckBox _chkLogVerbose = new();
    private readonly Label _lblCurrent = new();
    private readonly Label _lblDevice = new();
    private bool _loadingDevices;

    public SettingsForm(AppSettings settings, VolumeGuardEngine engine)
    {
        _settings = settings;
        _engine = engine;

        Text = "MicVolumeGuard — настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 372);

        // --- таблица: 3 колонки (метка | контрол | хвост), строки по содержимому ---
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 9,
            Padding = new Padding(12),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));

        // Строка 1: микрофон
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(new Label { Text = "Микрофон:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 6, 0) }, 0, 0);
        _cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDevice.Dock = DockStyle.Fill;
        _cmbDevice.SelectedIndexChanged += CmbDevice_SelectedIndexChanged;
        table.Controls.Add(_cmbDevice, 1, 0);
        table.SetColumnSpan(_cmbDevice, 2);

        // Строка 2: текущее устройство
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        _lblDevice.Text = "Текущее устройство: —";
        _lblDevice.AutoSize = true;
        _lblDevice.ForeColor = Color.DimGray;
        _lblDevice.Margin = new Padding(0, 5, 0, 0);
        table.Controls.Add(_lblDevice, 0, 1);
        table.SetColumnSpan(_lblDevice, 3);

        // Строка 3: громкость (слайдер + число)
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.Controls.Add(new Label { Text = "Громкость:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 6, 0) }, 0, 2);
        var volumePanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = new Padding(0) };
        volumePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        volumePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        _slider.Minimum = 0;
        _slider.Maximum = 100;
        _slider.Dock = DockStyle.Fill;
        _slider.Value = Math.Clamp(_settings.VolumePercent, 0, 100);
        volumePanel.Controls.Add(_slider, 0, 0);
        _num.Minimum = 0;
        _num.Maximum = 100;
        _num.Dock = DockStyle.Fill;
        _num.Value = Math.Clamp(_settings.VolumePercent, 0, 100);
        volumePanel.Controls.Add(_num, 1, 0);
        table.Controls.Add(volumePanel, 1, 2);
        table.SetColumnSpan(volumePanel, 2);

        // Строка 4: блокировка
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _chkLock.Text = "🔒 Блокировать уровень (откатывать изменения от других программ)";
        _chkLock.AutoSize = true;
        _chkLock.Checked = _settings.LockEnabled;
        _chkLock.Margin = new Padding(0, 5, 0, 0);
        table.Controls.Add(_chkLock, 0, 3);
        table.SetColumnSpan(_chkLock, 3);

        // Строка 5: mute
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var mutePanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(16, 0, 0, 0), AutoSize = true };
        mutePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mutePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _chkLockMute.Text = "Удерживать состояние mute:";
        _chkLockMute.AutoSize = true;
        _chkLockMute.Checked = _settings.LockMute;
        _chkLockMute.CheckedChanged += (s, e) => _chkMuteValue.Enabled = _chkLockMute.Checked;
        mutePanel.Controls.Add(_chkLockMute, 0, 0);
        _chkMuteValue.Text = "не замьючен";
        _chkMuteValue.AutoSize = true;
        _chkMuteValue.Checked = !_settings.LockMuteValue;
        _chkMuteValue.Enabled = _settings.LockMute;
        _chkMuteValue.Margin = new Padding(12, 3, 0, 0);
        mutePanel.Controls.Add(_chkMuteValue, 1, 0);
        table.Controls.Add(mutePanel, 0, 4);
        table.SetColumnSpan(mutePanel, 3);

        // Строка 6: автозапуск — галочка отражает ФАКТИЧЕСКОЕ состояние реестра
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _chkAutostart.Text = "Запускать вместе с Windows";
        _chkAutostart.AutoSize = true;
        _chkAutostart.Checked = Autostart.IsEnabled(); // истина из реестра, не из конфига
        _chkAutostart.Margin = new Padding(0, 5, 0, 0);
        table.Controls.Add(_chkAutostart, 0, 5);
        table.SetColumnSpan(_chkAutostart, 3);

        // Строка 7: логирование
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var logPanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 0), AutoSize = true };
        logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _chkLog.Text = "Вести журнал (log.txt)";
        _chkLog.AutoSize = true;
        _chkLog.Checked = _settings.LogEnabled;
        _chkLog.CheckedChanged += (s, e) => _chkLogVerbose.Enabled = _chkLog.Checked;
        logPanel.Controls.Add(_chkLog, 0, 0);
        _chkLogVerbose.Text = "подробный";
        _chkLogVerbose.AutoSize = true;
        _chkLogVerbose.Checked = _settings.LogVerbose;
        _chkLogVerbose.Enabled = _settings.LogEnabled;
        _chkLogVerbose.Margin = new Padding(12, 3, 0, 0);
        logPanel.Controls.Add(_chkLogVerbose, 1, 0);
        table.Controls.Add(logPanel, 0, 6);
        table.SetColumnSpan(logPanel, 3);

        // Строка 8: кнопки (компактные) + справка
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        var btnHelp = new Button { Text = "Справка", AutoSize = true, MinimumSize = new Size(88, 27) };
        btnHelp.Click += (s, e) =>
        {
            using var help = new HelpForm { Owner = this };
            help.ShowDialog(this);
        };
        var btnApply = new Button { Text = "Применить", AutoSize = true, MinimumSize = new Size(88, 27) };
        btnApply.Click += (s, e) => ApplySettings();
        var btnClose = new Button { Text = "Закрыть", AutoSize = true, MinimumSize = new Size(88, 27) };
        btnClose.Click += (s, e) => Close();
        buttons.Controls.Add(btnHelp);
        buttons.Controls.Add(btnApply);
        buttons.Controls.Add(btnClose);
        table.Controls.Add(buttons, 0, 7);
        table.SetColumnSpan(buttons, 3);

        // Строка 9: статус
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _lblCurrent.Text = "";
        _lblCurrent.AutoSize = true;
        _lblCurrent.ForeColor = Color.DarkGreen;
        _lblCurrent.Margin = new Padding(0, 6, 0, 0);
        table.Controls.Add(_lblCurrent, 0, 8);
        table.SetColumnSpan(_lblCurrent, 3);

        Controls.Add(table);
        AcceptButton = btnApply;

        _engine.VolumeChangedApplied += OnApplied;
        _engine.VolumeChangedExternal += OnExternal;

        _slider.ValueChanged += (s, e) => { if (_num.Value != _slider.Value) _num.Value = _slider.Value; };
        _num.ValueChanged += (s, e) => { if (_slider.Value != (int)_num.Value) _slider.Value = (int)_num.Value; };

        LoadDevices();
        UpdateDeviceLabel();
        FormClosed += (s, e) =>
        {
            _engine.VolumeChangedApplied -= OnApplied;
            _engine.VolumeChangedExternal -= OnExternal;
        };
    }

    private void CmbDevice_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingDevices) return;
        if (_cmbDevice.SelectedItem is not DeviceItem di) return;
        if (di.Id == _settings.DeviceId) return;
        _settings.DeviceId = di.Id;
        _settings.DeviceName = di.Name;
        _engine.AttachDevice();
        SaveAndApply();
        UpdateDeviceLabel();
    }

    private void UpdateDeviceLabel()
    {
        string name = _engine.CurrentDeviceName;
        _lblDevice.Text = "Текущее устройство: " + (string.IsNullOrEmpty(name) ? "— не найдено" : name);
    }

    private void LoadDevices()
    {
        _loadingDevices = true;
        try
        {
            var devices = MicrophoneController.ListCaptureDevices();
            var items = new List<DeviceItem> { new("", "🎯 По умолчанию (связь)") };
            items.AddRange(devices.Select(d => new DeviceItem(d.Id, d.Name)));
            _cmbDevice.Items.Clear();
            _cmbDevice.Items.AddRange(items.Cast<object>().ToArray());
            int idx = items.FindIndex(d => d.Id == _settings.DeviceId);
            _cmbDevice.SelectedIndex = idx >= 0 ? idx : 0;
            if (devices.Count == 0)
                Logger.Log("LoadDevices: список микрофонов пуст — проверьте, включено ли устройство записи");
        }
        catch (Exception ex)
        {
            Logger.Log($"LoadDevices ОШИБКА: {ex.Message}");
            _cmbDevice.Items.Clear();
            _cmbDevice.Items.Add(new DeviceItem("", "Не удалось получить список"));
            _cmbDevice.SelectedIndex = 0;
        }
        finally
        {
            _loadingDevices = false;
        }
    }

    private void ApplySettings()
    {
        _settings.VolumePercent = (int)_num.Value;
        _settings.LockEnabled = _chkLock.Checked;
        _settings.LockMute = _chkLockMute.Checked;
        _settings.LockMuteValue = !_chkMuteValue.Checked; // чекбокс = "не замьючен"
        _settings.Autostart = _chkAutostart.Checked;
        _settings.LogEnabled = _chkLog.Checked;
        _settings.LogVerbose = _chkLogVerbose.Checked;
        Autostart.Set(_settings.Autostart);
        Logger.Configure(_settings.LogEnabled, _settings.LogVerbose);
        SaveAndApply();
    }

    private void SaveAndApply()
    {
        _settings.Save();
        _engine.UpdateSettings(_settings);
        _engine.ApplyTarget(); // применить уровень сразу, даже если блокировка выключена
        _lblCurrent.Text = $"Установлено: {_settings.VolumePercent}%  (блокировка: {(_settings.LockEnabled ? "вкл" : "выкл")})";
    }

    private void OnApplied(int percent, bool muted)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _lblCurrent.Text = $"Установлено: {percent}%  (mute: {(muted ? "да" : "нет")})";
        });
    }

    private void OnExternal(int percent, bool muted)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _lblCurrent.ForeColor = Color.Firebrick;
            _lblCurrent.Text = $"Перехвачено внешнее изменение ({percent}%) → откат";
        });
    }

    private sealed record DeviceItem(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
