using Microsoft.Win32;

namespace MicVolumeGuard;

/// <summary>Автозапуск через HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MicVolumeGuard";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string path && File.Exists(path);
        }
        catch { return false; }
    }

    /// <summary>Регистрирует автозапуск текущего exe (или удаляет, если enable=false).</summary>
    public static void Set(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enable)
            {
                string exe = Environment.ProcessPath ?? Application.ExecutablePath;
                // Кавычки обязательны для путей с пробелами
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName);
            }
        }
        catch { }
    }
}
