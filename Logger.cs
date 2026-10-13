using System;
using System.IO;

namespace MicVolumeGuard;

public enum LogLevel
{
    Off = 0,      // лог выключен
    Normal = 1,   // события подключения/откатов (по умолчанию)
    Verbose = 2   // всё, включая каждый SetVolume
}

/// <summary>
/// Файловый лог для диагностики: %APPDATA%\MicVolumeGuard\log.txt.
/// Ведение логов настраивается в настройках (LogEnabled + LogVerbose).
/// </summary>
internal static class Logger
{
    private static readonly object Lock = new();
    private const long MaxBytes = 512 * 1024;

    private static LogLevel _level = LogLevel.Normal;
    public static LogLevel Level
    {
        get => _level;
        set
        {
            if (_level != value)
            {
                _level = value;
                Log($"Логирование переключено: {value}");
            }
        }
    }

    public static void Configure(bool enabled, bool verbose)
    {
        Level = !enabled ? LogLevel.Off : (verbose ? LogLevel.Verbose : LogLevel.Normal);
    }

    /// <summary>Обычное сообщение (пишется при Normal и Verbose).</summary>
    public static void Log(string message) => Write(LogLevel.Normal, message);

    /// <summary>Подробное сообщение (только при Verbose, например каждый SetVolume).</summary>
    public static void Debug(string message) => Write(LogLevel.Verbose, message);

    private static void Write(LogLevel required, string message)
    {
        if (_level < required) return;
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
