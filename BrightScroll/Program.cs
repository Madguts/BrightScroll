using BrightScroll.Core;
using BrightScroll.Windows;

namespace BrightScroll;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instanceMutex = new Mutex(initiallyOwned: true, "Local\\Madguts.BrightScroll", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrightScroll",
            "settings.json");

        try
        {
            using var context = new TrayApplicationContext(AppSettings.Load(settingsPath), settingsPath);
            Application.Run(context);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"BrightScroll could not start: {exception.Message}",
                "BrightScroll",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
