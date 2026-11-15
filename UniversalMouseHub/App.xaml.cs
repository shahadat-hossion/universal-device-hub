using System.Windows;

namespace UniversalMouseHub;

public partial class App : System.Windows.Application
{
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        _tray = new TrayService(window);
        if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)) window.Show();
    }

    public void ExitApplication()
    {
        _tray?.Dispose();
        Shutdown();
    }
}
