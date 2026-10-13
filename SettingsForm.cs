using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MicVolumeGuard;

/// <summary>Окно настроек: выбор микрофона, уровень громкости, блокировка, автозапуск.</summary>
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
    private readonly Label _lblCurrent = new();
    private readonly Label _lblDevice = new();

    public SettingsForm(AppSettings settings, VolumeGuardEngine engine)
    {
        _settings = settings;
        _engine = engine;

        Text = "MicVolumeGuard — настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 320);

        int y = 15;
        Controls.Add(new Label
        {
            Text = "Микрофон:",
            Location = new Point(15, y + 3),
            AutoSize = true
        });
        _cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDevice.Location = new Point(120, y);
        _cmbDevice.Width = 320;
        _cmbDevice.SelectedIndexChanged += (s, e) =>
        {
            if (_cmbDevice.SelectedItem is DeviceItem di && di.Id != _settings.DeviceId)
            {
                _settings.DeviceId = di.Id;
                _settings.DeviceName = di.Name;
                _engine.AttachDevice();
                SaveAndApply();
            }
        };
        Controls.Add(_cmbDevice);
        y += 38;

        _lblDevice.Text = "Текущее устройство: " + (_engine.CurrentDeviceName is { Length: > 0 } n ? n : "—");
        _lblDevice.Location = new Point(15, y);
        _lblDevice.AutoSize = true;
        _lblDevice.ForeColor = Color.DimGray;
        Controls.Add(_lblDevice);
        y += 30;

        Controls.Add(new Label { Text = "Громкость:", Location = new Point(15, y + 3), AutoSize = true });
        _slider.Minimum = 0; _slider.Maximum = 100;
        _slider.Location = new Point(120, y);
        _slider.Width = 240;
        _slider.Value = _settings.VolumePercent;
        _slider.Scroll += (s, e) => { _num.Value = _slider.Value; };
        _slider.ValueChanged += (s, e) => { if (_num.Value != _slider.Value) _num.Value = _slider.Value; };
        Controls.Add(_slider);

        _num.Minimum = 0; _num.Maximum = 100;
        _num.Location = new Point(370, y - 2);
        _num.Width = 70;
        _num.Value = _settings.VolumePercent;
        _num.ValueChanged += (s, e) => { if (_slider.Value != (int)_num.Value) _slider.Value = (int)_num.Value; };
        Controls.Add(_num);
        y += 40;

        _chkLock.Text = "🔒 Блокировать уровень (откатывать изменения от других программ)";
        _chkLock.AutoSize = true;
        _chkLock.Location = new Point(15, y);
        _chkLock.Checked = _settings.LockEnabled;
        Controls.Add(_chkLock);
        y += 30;

        _chkLockMute.Text = "Удерживать состояние mute:";
        _chkLockMute.AutoSize = true;
        _chkLockMute.Location = new Point(35, y);
        _chkLockMute.Checked = _settings.LockMute;
        _chkLockMute.CheckedChanged += (s, e) => { _chkMuteValue.Enabled = _chkLockMute.Checked; };
        Controls.Add(_chkLockMute);

        _chkMuteValue.Text = "не замьючен";
        _chkMuteValue.AutoSize = true;
        _chkMuteValue.Location = new Point(250, y);
        _chkMuteValue.Checked = !_settings.LockMuteValue;
        _chkMuteValue.Enabled = _settings.LockMute;
        Controls.Add(_chkMuteValue);
        y += 32;

        _chkAutostart.Text = "Запускать вместе с Windows";
        _chkAutostart.AutoSize = true;
        _chkAutostart.Location = new Point(15, y);
        _chkAutostart.Checked = _settings.Autostart && Autostart.IsEnabled();
        Controls.Add(_chkAutostart);
        y += 32;

        var btnApply = new Button
        {
            Text = "Применить",
            Location = new Point(200, y),
            Size = new Size(110, 30)
        };
        btnApply.Click += (s, e) => ApplySettings();
        Controls.Add(btnApply);

        var btnClose = new Button
        {
            Text = "Закрыть",
            Location = new Point(320, y),
            Size = new Size(110, 30)
        };
        btnClose.Click += (s, e) => Close();
        Controls.Add(btnClose);
        y += 40;

        _lblCurrent.Text = "";
        _lblCurrent.Location = new Point(15, y);
        _lblCurrent.AutoSize = true;
        _lblCurrent.ForeColor = Color.DarkGreen;
        Controls.Add(_lblCurrent);

        _engine.VolumeChangedApplied += OnApplied;
        _engine.VolumeChangedExternal += OnExternal;

        LoadDevices();
        FormClosed += (s, e) =>
        {
            _engine.VolumeChangedApplied -= OnApplied;
            _engine.VolumeChangedExternal -= OnExternal;
        };
    }

    private void LoadDevices()
    {
        try
        {
            var devices = MicrophoneController.ListCaptureDevices();
            var items = new List<DeviceItem> { new("", "🎯 По умолчанию (связь)") };
            items.AddRange(devices.Select(d => new DeviceItem(d.Id, d.Name)));
            _cmbDevice.Items.Clear();
            _cmbDevice.Items.AddRange(items.Cast<object>().ToArray());
            int idx = items.FindIndex(d => d.Id == _settings.DeviceId);
            _cmbDevice.SelectedIndex = idx >= 0 ? idx : 0;
        }
        catch
        {
            _cmbDevice.Items.Clear();
            _cmbDevice.Items.Add(new DeviceItem("", "Не удалось получить список"));
            _cmbDevice.SelectedIndex = 0;
        }
    }

    private void ApplySettings()
    {
        _settings.VolumePercent = (int)_num.Value;
        _settings.LockEnabled = _chkLock.Checked;
        _settings.LockMute = _chkLockMute.Checked;
        _settings.LockMuteValue = !_chkMuteValue.Checked; // чекбокс = "не замьючен"
        _settings.Autostart = _chkAutostart.Checked;
        Autostart.Set(_settings.Autostart);
        SaveAndApply();
    }

    private void SaveAndApply()
    {
        _settings.Save();
        _engine.UpdateSettings(_settings);
        if (_engine.Settings.LockEnabled || true) _engine.ApplyTarget();
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
