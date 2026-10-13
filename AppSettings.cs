using Newtonsoft.Json;

namespace MicVolumeGuard;

/// <summary>Настройки приложения. Хранятся в %APPDATA%\MicVolumeGuard\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Заблокированный уровень громкости, 0..100 (в процентах).</summary>
    [JsonProperty("volume_percent")]
    public int VolumePercent { get; set; } = 100;

    /// <summary>Включена ли блокировка уровня от внешних изменений.</summary>
    [JsonProperty("lock_enabled")]
    public bool LockEnabled { get; set; } = true;

    /// <summary>Также удерживать mute/unmute состояние микрофона.</summary>
    [JsonProperty("lock_mute")]
    public bool LockMute { get; set; } = false;

    [JsonProperty("lock_mute_value")]
    public bool LockMuteValue { get; set; } = false; // false = не замьючен

    /// <summary>Запускать вместе с Windows (HKCU Run).</summary>
    [JsonProperty("autostart")]
    public bool Autostart { get; set; } = true;

    /// <summary>Id микрофона ("{0.0.1.00000000}..."); пусто = устройство связи по умолчанию.</summary>
    [JsonProperty("device_id")]
    public string DeviceId { get; set; } = "";

    /// <summary>Имя выбранного микрофона (только для отображения).</summary>
    [JsonProperty("device_name")]
    public string DeviceName { get; set; } = "";

    /// <summary>Восстанавливать ли блокировку после сна/перезагрузки (watchdog-интервал, мс).</summary>
    [JsonProperty("watchdog_interval_ms")]
    public int WatchdogIntervalMs { get; set; } = 1500;

    public static AppSettings Load()
    {
        try
        {
            string path = GetFilePath();
            if (File.Exists(path))
            {
                var s = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(path));
                if (s != null) return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string dir = GetDir();
            Directory.CreateDirectory(dir);
            string path = GetFilePath();
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(this, Formatting.Indented));
            File.Delete(path);
            File.Move(tmp, path);
        }
        catch { /* настройки не критичны для работы */ }
    }

    public static string GetDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MicVolumeGuard");

    public static string GetFilePath() => Path.Combine(GetDir(), "settings.json");
}
