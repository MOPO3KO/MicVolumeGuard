using System;
using System.IO;

namespace MicVolumeGuard;

/// <summary>Простой файловый лог для диагностики: %APPDATA%\MicVolumeGuard\log.txt</summary>
internal static class Logger
{
    private static readonly object Lock = new();
    private const long MaxBytes = 512 * 1024;

    public static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                string dir = AppSettings.GetDir();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "log.txt");
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    File.Delete(path); // ротация простым удалением
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch { /* лог не критичен для работы */ }
    }
}
