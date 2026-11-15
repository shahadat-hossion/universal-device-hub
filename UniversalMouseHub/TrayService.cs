using System.Drawing;
using System.Windows.Forms;

namespace UniversalMouseHub;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly MainWindow _window;
    public TrayService(MainWindow window)
    {
        _window = window;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => _window.Dispatcher.Invoke(_window.ShowFromTray));
        menu.Items.Add("Exit", null, (_, _) => _window.Dispatcher.Invoke(Exit));
        var appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
        _icon = new NotifyIcon { Icon = appIcon, Text = "Universal Device Hub — monitoring devices", ContextMenuStrip = menu, Visible = true };
        _icon.Click += (_, e) => { if (e is MouseEventArgs { Button: MouseButtons.Left }) _window.Dispatcher.Invoke(_window.ShowFromTray); };
        _window.DeviceUpdated += OnDeviceUpdated;
    }
    private void OnDeviceUpdated(object? sender, MouseDeviceInfo? device)
    {
        var status = device?.BatteryLevel is int level ? $"{level}% remaining" : "battery unavailable";
        var text = $"{device?.Name ?? "No device detected"} — {status}";
        _icon.Text = text.Length <= 63 ? text : text[..62] + "…";
    }
    private void Exit() { _window.PermitClose(); _window.Close(); ((App)System.Windows.Application.Current).ExitApplication(); }
    public void Dispose() { _window.DeviceUpdated -= OnDeviceUpdated; _icon.Visible = false; _icon.Dispose(); }
}
