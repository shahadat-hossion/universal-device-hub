using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace UniversalMouseHub;

public sealed class MouseMonitor
{
    private static readonly Guid MouseClassGuid = new("4D36E96F-E325-11CE-BFC1-08002BE10318");
    private static readonly Guid KeyboardClassGuid = new("4D36E96B-E325-11CE-BFC1-08002BE10318");
    private static readonly DevPropKey DeviceDesc = new(new("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);
    private static readonly DevPropKey BusReportedDesc = new(new("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"), 4);
    private static readonly DevPropKey FriendlyName = new(new("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    private static readonly DevPropKey InstanceId = new(new("78C34FC8-104A-4ACA-9EA4-524D52996E57"), 256);
    private static readonly DevPropKey BatteryLife = new(new("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 10);
    private static readonly DevPropKey IsPresent = new(new("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"), 5);
    private static readonly DevPropKey ContainerId = new(new("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);

    public Task<IReadOnlyList<MouseDeviceInfo>> GetDevicesAsync() => Task.Run<IReadOnlyList<MouseDeviceInfo>>(() =>
    {
        var devices = EnumerateInputClass(MouseClassGuid, "Mouse")
            .Concat(EnumerateInputClass(KeyboardClassGuid, "Keyboard"))
            .Concat(EnumerateBluetoothDevices())
            .GroupBy(device => device.ContainerId?.ToString() ?? device.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(device => device.BatteryLevel.HasValue)
                .ThenBy(device => device.Category == "Bluetooth")
                .First())
            .OrderByDescending(device => device.BatteryLevel.HasValue)
            .ThenBy(device => device.Category)
            .ThenBy(device => device.Name)
            .ToList();
        return devices;
    });

    private static IReadOnlyList<MouseDeviceInfo> EnumerateInputClass(Guid classGuid, string category)
    {
        using var set = SetupDiGetClassDevs(ref classGuid, null, IntPtr.Zero, 0x2);
        if (set.IsInvalid) return [];
        var devices = new List<MouseDeviceInfo>();
        for (uint index = 0; ; index++)
        {
            var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
            if (!SetupDiEnumDeviceInfo(set, index, ref data)) { if (Marshal.GetLastWin32Error() == 259) break; continue; }
            if (ReadBool(set, data, IsPresent) == false) continue;
            var id = ReadString(set, data, InstanceId) ?? string.Empty;
            var containerId = ReadGuid(set, data, ContainerId);
            if (id.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase)) continue;
            var genericName = ReadString(set, data, BusReportedDesc) ?? ReadString(set, data, FriendlyName) ?? ReadString(set, data, DeviceDesc) ?? category;
            var resolved = ResolveParentIdentity(data.DevInst, id, genericName);
            var name = DeviceCatalog.Find(resolved.DevicePath)?.DisplayName ?? resolved.Name;
            if (category == "Mouse" && (name.Contains("touchpad", StringComparison.OrdinalIgnoreCase) || name.Contains("trackpad", StringComparison.OrdinalIgnoreCase))) continue;
            var vendorBattery = category == "Mouse" ? VendorBatteryReader.TryRead(id) : null;
            var battery = vendorBattery?.Level ?? ReadInt(set, data, BatteryLife) ?? resolved.BatteryLevel ?? FindContainerBattery(containerId);
            if (battery is < 0 or > 100) battery = null;
            devices.Add(new(name, category, ClassifyConnection(resolved.DevicePath, name), battery, vendorBattery?.IsCharging, id, containerId));
        }
        return devices
            .GroupBy(d => d.ContainerId?.ToString() ?? d.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(d => d.BatteryLevel.HasValue).First())
            .OrderByDescending(d => d.BatteryLevel.HasValue)
            .ThenBy(d => d.Name)
            .ToList();
    }

    private static IReadOnlyList<MouseDeviceInfo> EnumerateBluetoothDevices()
    {
        using var set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 0x2 | 0x4);
        if (set.IsInvalid) return [];
        var devices = new List<MouseDeviceInfo>();
        for (uint index = 0; ; index++)
        {
            var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
            if (!SetupDiEnumDeviceInfo(set, index, ref data))
            {
                if (Marshal.GetLastWin32Error() == 259) break;
                continue;
            }
            var id = ReadString(set, data, InstanceId) ?? string.Empty;
            if (!id.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase)
                && !id.StartsWith("BTHLEDEVICE\\", StringComparison.OrdinalIgnoreCase)) continue;
            var name = ReadString(set, data, FriendlyName) ?? ReadString(set, data, BusReportedDesc) ?? ReadString(set, data, DeviceDesc);
            if (string.IsNullOrWhiteSpace(name) || !IsProductName(name)) continue;
            var containerId = ReadGuid(set, data, ContainerId);
            var battery = ReadInt(set, data, BatteryLife) ?? FindContainerBattery(containerId);
            if (battery is < 0 or > 100) battery = null;
            devices.Add(new(name, ClassifyBluetoothCategory(name), "Bluetooth", battery, null, id, containerId));
        }
        return devices;
    }

    private static string ClassifyBluetoothCategory(string name)
    {
        string[] audioTerms = ["airpod", "headphone", "headset", "earbud", "buds", "speaker", "audio", "hands-free"];
        return audioTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)) ? "Audio" : "Bluetooth";
    }
    private static string ClassifyConnection(string id, string name)
    {
        if (id.Contains("BTH", StringComparison.OrdinalIgnoreCase) || name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)) return "Bluetooth";
        if (id.Contains("USB", StringComparison.OrdinalIgnoreCase))
            return name.Contains("receiver", StringComparison.OrdinalIgnoreCase)
                || name.Contains("wireless", StringComparison.OrdinalIgnoreCase)
                || name.Contains("2.4G", StringComparison.OrdinalIgnoreCase)
                ? "Wireless receiver" : "USB";
        return "Wireless / HID";
    }

    private static ResolvedIdentity ResolveParentIdentity(uint childDevInst, string childId, string fallbackName)
    {
        var names = new List<string> { fallbackName };
        var devicePath = childId;
        int? battery = null;
        var current = childDevInst;

        for (var depth = 0; depth < 6; depth++)
        {
            var id = ReadDevNodeString(current, InstanceId);
            var busName = ReadDevNodeString(current, BusReportedDesc);
            var friendlyName = ReadDevNodeString(current, FriendlyName);
            var description = ReadDevNodeString(current, DeviceDesc);
            battery ??= ReadDevNodeInt(current, BatteryLife);

            if (!string.IsNullOrWhiteSpace(id)) devicePath += "|" + id;
            if (!string.IsNullOrWhiteSpace(busName)) names.Add(busName);
            if (!string.IsNullOrWhiteSpace(friendlyName)) names.Add(friendlyName);
            if (!string.IsNullOrWhiteSpace(description)) names.Add(description);

            if (CM_Get_Parent(out var parent, current, 0) != 0) break;
            current = parent;
        }

        var bestName = names.FirstOrDefault(IsProductName) ?? fallbackName;
        return new ResolvedIdentity(bestName, devicePath, battery is >= 0 and <= 100 ? battery : null);
    }

    private static bool IsProductName(string name)
    {
        string[] genericNames =
        [
            "HID-compliant mouse", "HID Keyboard Device", "USB Input Device", "USB Composite Device", "Generic USB Hub",
            "USB Root Hub", "Bluetooth Low Energy GATT compliant HID device", "Mouse", "HID-compliant device"
        ];
        return !string.IsNullOrWhiteSpace(name)
            && !genericNames.Any(generic => name.Contains(generic, StringComparison.OrdinalIgnoreCase))
            && !name.Contains("Host Controller", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadDevNodeString(uint devInst, DevPropKey key)
    {
        var bytes = ReadDevNodeProperty(devInst, key);
        return bytes is null ? null : Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private static int? ReadDevNodeInt(uint devInst, DevPropKey key)
    {
        var bytes = ReadDevNodeProperty(devInst, key);
        return bytes is { Length: >= 4 } ? BitConverter.ToInt32(bytes) : null;
    }

    private static int? FindContainerBattery(Guid? containerId)
    {
        if (containerId is null) return null;
        using var allDevices = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 0x2 | 0x4);
        if (allDevices.IsInvalid) return null;
        for (uint index = 0; ; index++)
        {
            var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
            if (!SetupDiEnumDeviceInfo(allDevices, index, ref data))
            {
                if (Marshal.GetLastWin32Error() == 259) break;
                continue;
            }
            if (ReadGuid(allDevices, data, ContainerId) != containerId) continue;
            var level = ReadInt(allDevices, data, BatteryLife);
            if (level is >= 0 and <= 100) return level;
        }
        return null;
    }

    private static byte[]? ReadDevNodeProperty(uint devInst, DevPropKey key)
    {
        uint size = 0;
        CM_Get_DevNode_Property(devInst, ref key, out _, null, ref size, 0);
        if (size == 0) return null;
        var buffer = new byte[size];
        return CM_Get_DevNode_Property(devInst, ref key, out _, buffer, ref size, 0) == 0 ? buffer : null;
    }
    private static string? ReadString(SafeDeviceInfoSetHandle set, SpDevInfoData data, DevPropKey key) { var bytes = ReadProperty(set, data, key); return bytes is null ? null : Encoding.Unicode.GetString(bytes).TrimEnd('\0'); }
    private static int? ReadInt(SafeDeviceInfoSetHandle set, SpDevInfoData data, DevPropKey key) { var bytes = ReadProperty(set, data, key); return bytes is { Length: >= 4 } ? BitConverter.ToInt32(bytes) : null; }
    private static bool? ReadBool(SafeDeviceInfoSetHandle set, SpDevInfoData data, DevPropKey key) { var bytes = ReadProperty(set, data, key); return bytes is { Length: >= 1 } ? bytes[0] != 0 : null; }
    private static Guid? ReadGuid(SafeDeviceInfoSetHandle set, SpDevInfoData data, DevPropKey key) { var bytes = ReadProperty(set, data, key); return bytes is { Length: >= 16 } ? new Guid(bytes.AsSpan(0, 16)) : null; }
    private static byte[]? ReadProperty(SafeDeviceInfoSetHandle set, SpDevInfoData data, DevPropKey key)
    {
        SetupDiGetDeviceProperty(set, ref data, ref key, out _, null, 0, out var required, 0);
        if (required == 0) return null;
        var buffer = new byte[required];
        return SetupDiGetDeviceProperty(set, ref data, ref key, out _, buffer, required, out _, 0) ? buffer : null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DevPropKey { public Guid FormatId; public uint PropertyId; public DevPropKey(Guid f, uint p) { FormatId = f; PropertyId = p; } }
    [StructLayout(LayoutKind.Sequential)] private struct SpDevInfoData { public uint Size; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
    private sealed record ResolvedIdentity(string Name, string DevicePath, int? BatteryLevel);
    private sealed class SafeDeviceInfoSetHandle : SafeHandleZeroOrMinusOneIsInvalid { private SafeDeviceInfoSetHandle() : base(true) { } protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle); }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeDeviceInfoSetHandle SetupDiGetClassDevs(ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")] private static extern SafeDeviceInfoSetHandle SetupDiGetClassDevs(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInfo(SafeDeviceInfoSetHandle set, uint index, ref SpDevInfoData data);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceProperty(SafeDeviceInfoSetHandle set, ref SpDevInfoData data, ref DevPropKey key, out uint propertyType, byte[]? buffer, uint bufferSize, out uint requiredSize, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parentDevInst, uint devInst, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_DevNode_PropertyW")]
    private static extern uint CM_Get_DevNode_Property(uint devInst, ref DevPropKey propertyKey, out uint propertyType, byte[]? propertyBuffer, ref uint propertyBufferSize, uint flags);
}
