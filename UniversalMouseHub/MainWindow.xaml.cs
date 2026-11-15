using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace UniversalMouseHub;

public partial class MainWindow : System.Windows.Window
{
    private readonly MouseMonitor _monitor = new();
    private readonly DispatcherTimer _timer;
    private bool _allowClose;
    private bool _loadingStartupSetting = true;
    private IReadOnlyList<MouseDeviceInfo> _devices = [];
    private MouseDeviceInfo? _selectedDevice;
    public event EventHandler<MouseDeviceInfo?>? DeviceUpdated;

    public MainWindow()
    {
        InitializeComponent();
        StartupCheckBox.IsChecked = StartupManager.IsEnabled();
        _loadingStartupSetting = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        _timer.Tick += async (_, _) => await RefreshDeviceAsync();
        Loaded += async (_, _) => { _timer.Start(); await RefreshDeviceAsync(); };
    }

    public void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); Topmost = true; Topmost = false; }
    public void PermitClose() => _allowClose = true;
    protected override void OnClosing(CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); return; } base.OnClosing(e); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshDeviceAsync();

    private async Task RefreshDeviceAsync()
    {
        var selectedId = _selectedDevice?.InstanceId;
        try { _devices = await _monitor.GetDevicesAsync(); } catch { _devices = []; }
        DeviceCards.ItemsSource = _devices;
        _selectedDevice = _devices.FirstOrDefault(d => d.InstanceId == selectedId) ?? _devices.FirstOrDefault();
        if (DetailPanel.Visibility == Visibility.Visible) Render(_selectedDevice);
        DeviceUpdated?.Invoke(this, _selectedDevice);
    }

    private void Render(MouseDeviceInfo? device)
    {
        if (device is null)
        {
            DeviceNameText.Text = "No connected device detected"; ConnectionTypeText.Text = "Connect a mouse, keyboard, or Bluetooth device"; ConnectionStatusText.Text = "Disconnected"; ConnectionStatusText.Foreground = BrushFrom("#FF9A9A"); StatusDot.Fill = BrushFrom("#FF6B6B"); SetUnknownBattery(); UpdatedText.Text = $"Checked {DateTime.Now:t}"; return;
        }
        DeviceNameText.Text = device.Name; ConnectionTypeText.Text = device.ConnectionType; ConnectionStatusText.Text = "Connected"; ConnectionStatusText.Foreground = BrushFrom("#7DE9B8"); StatusDot.Fill = BrushFrom("#54E3A5"); UpdatedText.Text = $"Updated {DateTime.Now:t}";
        if (device.BatteryLevel is not int level) { SetUnknownBattery(); return; }
        BatteryPercentText.Text = $"{level}%"; BatteryMessageText.Text = "Current remaining charge"; BatteryFill.Width = 230 * level / 100d; BatteryStatusText.Text = device.IsCharging == true ? "Charging" : level < 20 ? "Low" : "Good";
        var color = device.IsCharging == true ? "#5BD8FF" : level < 20 ? "#FF6B6B" : "#54E3A5"; BatteryFill.Background = BrushFrom(color); BatteryStatusText.Foreground = BrushFrom(color);
    }

    private void SetUnknownBattery() { BatteryPercentText.Text = "—"; BatteryMessageText.Text = "Battery information unavailable"; BatteryFill.Width = 0; BatteryStatusText.Text = "Unavailable"; BatteryStatusText.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"); }
    private static SolidColorBrush BrushFrom(string color) => new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    private void Configure_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: MouseDeviceInfo device }) return;
        _selectedDevice = device;
        Render(device);
        HomePanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DeviceUpdated?.Invoke(this, device);
    }
    private async void AddDevice_Click(object sender, RoutedEventArgs e) { HomePanel.Visibility = Visibility.Visible; DetailPanel.Visibility = Visibility.Collapsed; await RefreshDeviceAsync(); }
    private void Back_Click(object sender, RoutedEventArgs e) { DetailPanel.Visibility = Visibility.Collapsed; HomePanel.Visibility = Visibility.Visible; }
    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e) { if (!_loadingStartupSetting) StartupManager.SetEnabled(StartupCheckBox.IsChecked == true); }
}
