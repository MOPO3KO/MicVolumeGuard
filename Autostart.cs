using Microsoft.Win32;

namespace MicVolumeGuard;

/// <summary>Автозапуск через HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MicVolumeGuard";

    /// <summary>Записан ли автозапуск в реестре (независимо от содержимого пути).</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            // Важно: значение хранится с кавычками ("C:\...\MicVolumeGuard.exe"), поэтому
            // проверять File.Exists(path) было нельзя — кавычки делали путь несуществующим.
            // Достаточно самого факта наличия непустой записи.
            return key?.GetValue(ValueName) is string v && v.Trim().Length > 0;
        }
        catch { return false; }
    }

    /// <summary>Регистрирует автозапуск текущего exe (или удаляет, если enable=false).</summary>
    public static void Set(bool enable)
    {
        try
        {
            // CreateSubKey: ключ Run всегда существует, но на некоторых системах
            // OpenSubKey(writable:true) может вернуть null из-за прав — CreateSubKey надёжнее
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enable)
            {
                string exe = Environment.ProcessPath ?? Application.ExecutablePath;
                // REG_SZ с кавычками — безопасно для путей с пробелами
                key.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
                Logger.Log($"Автозапуск включён: \"{exe}\"");
            }
            else
            {
                if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName);
                    Logger.Log("Автозапуск выключен");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Автозапуск ОШИБКА: {ex.Message}");
        }
    }
}
