using System.Text.RegularExpressions;

namespace UniversalMouseHub;

/// <summary>Maps stable hardware IDs to bundled product artwork.</summary>
public static partial class DeviceCatalog
{
    private static readonly IReadOnlyDictionary<string, CatalogDevice> Devices =
        new Dictionary<string, CatalogDevice>(StringComparer.OrdinalIgnoreCase)
        {
            ["046D:C08B"] = new("Logitech G502 HERO", "Assets/DeviceImages/logitech-g502-hero.png")
        };

    public static CatalogDevice? Find(string hardwarePath)
    {
        var match = UsbIdRegex().Match(hardwarePath);
        if (!match.Success) return null;

        return Devices.GetValueOrDefault($"{match.Groups[1].Value}:{match.Groups[2].Value}");
    }

    [GeneratedRegex(@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex UsbIdRegex();
}

public sealed record CatalogDevice(string DisplayName, string ImagePath);
