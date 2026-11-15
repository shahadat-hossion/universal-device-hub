namespace UniversalMouseHub;

public sealed record MouseDeviceInfo(string Name, string Category, string ConnectionType, int? BatteryLevel, bool? IsCharging, string InstanceId, Guid? ContainerId)
{
    public string SelectorLabel => $"{Name}  ·  {ConnectionType}";
    public string BatteryLabel => BatteryLevel is int level ? $"{level}%" : "Battery unavailable";
    public string BatteryStatusLabel => IsCharging == true ? "Charging" : BatteryLevel is < 20 ? "Low" : BatteryLevel.HasValue ? "Good" : "Unavailable";
    public string DeviceGlyph => Category switch { "Keyboard" => "⌨", "Audio" => "♫", "Bluetooth" => "ᛒ", _ => "●" };
    public string? DeviceImagePath => DeviceCatalog.Find(InstanceId)?.ImagePath
        ?? (Name.Contains("AirPod", StringComparison.OrdinalIgnoreCase) ? "Assets/airpods-pro.png" : null)
        ?? Category switch
        {
            "Mouse" => "Assets/generic-mouse.png",
            "Keyboard" => "Assets/generic-keyboard.png",
            _ => null
        };
    public bool HasDeviceImage => DeviceImagePath is not null;
}
