using HidSharp;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace UniversalMouseHub;

public sealed record VendorBatteryResult(int Level, bool IsCharging);

public static partial class VendorBatteryReader
{
    private const int AjazzWiredVid = 0xA8A4;
    private const int AjazzDongleVid = 0xA8A5;
    private static readonly HashSet<int> AjazzNjProductIds = [0x2157, 0x2158, 0x2167, 0x2168, 0x2177, 0x2178];

    public static VendorBatteryResult? TryRead(string instanceId)
    {
        var match = VidPidRegex().Match(instanceId);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, null, out var vid)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.HexNumber, null, out var pid)) return null;

        return (vid == AjazzWiredVid || vid == AjazzDongleVid) && AjazzNjProductIds.Contains(pid)
            ? TryReadAjazzNj(vid, pid)
            : null;
    }

    private static VendorBatteryResult? TryReadAjazzNj(int vid, int pid)
    {
        var candidates = DeviceList.Local.GetHidDevices(vid, pid)
            .Where(device => device.GetMaxOutputReportLength() >= 64 && device.GetMaxInputReportLength() >= 9)
            .OrderByDescending(device => device.DevicePath.Contains("col06", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var device in candidates)
        {
            if (!device.TryOpen(out var stream)) continue;
            using (stream)
            {
                try
                {
                    stream.ReadTimeout = 750;
                    stream.WriteTimeout = 750;
                    var request = new byte[device.GetMaxOutputReportLength()];
                    request[0] = 0xF0;
                    request[1] = 0x30;
                    request[2] = 0x01;
                    stream.Write(request);

                    var deadline = DateTime.UtcNow.AddMilliseconds(750);
                    while (DateTime.UtcNow < deadline)
                    {
                        var reply = new byte[device.GetMaxInputReportLength()];
                        var read = stream.Read(reply, 0, reply.Length);
                        if (read < 9) continue;

                        var commandOffset = reply[0] == 0x30 ? 0 : reply[0] == 0xF0 && reply[1] == 0x30 ? 1 : -1;
                        if (commandOffset < 0 || read <= commandOffset + 8) continue;
                        var level = reply[commandOffset + 7];
                        if (level > 100) continue;
                        return new VendorBatteryResult(level, reply[commandOffset + 8] != 0);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (TimeoutException) { }
            }
        }
        return null;
    }

    [GeneratedRegex(@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();
}
