using System;
using System.Windows.Forms;

namespace MicVolumeGuard;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Одно приложение в системе
        using var mutex = new Mutex(true, "MicVolumeGuard_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("MicVolumeGuard уже запущен.", "MicVolumeGuard",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new TrayApplicationContext());
        GC.KeepAlive(mutex);
    }
}
