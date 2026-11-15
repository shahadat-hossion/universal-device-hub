# Universal Device Hub

**Universal Device Hub is a lightweight, open-source Windows device manager for monitoring mice, keyboards, Bluetooth accessories, USB devices, wireless receivers, and battery levels from one modern desktop dashboard.**

Built with C#, .NET, and WPF, the app automatically discovers connected peripherals, resolves their real product names through the Windows Plug and Play device tree, and displays accurate battery information whenever Windows or a validated hardware protocol makes it available.

## Why Universal Device Hub?

Windows users often need a different vendor application for every wireless mouse, keyboard, headset, or Bluetooth accessory. Universal Device Hub provides one clean Windows 11-inspired interface for the essential information you need without background bloat, advertisements, accounts, or fabricated battery data.

## Features

- Automatically detects connected mice and keyboards.
- Discovers active Bluetooth devices and accessories.
- Recognizes USB, Bluetooth, HID, and 2.4 GHz wireless-receiver connections.
- Resolves real product names from parent and device-container information instead of showing only generic HID labels.
- Displays current remaining battery charge when the hardware exposes a trustworthy value.
- Includes a validated AJAZZ/NACODEX NJ07 and NJ08 battery provider.
- Clearly displays **Battery information unavailable** when no reliable value exists.
- Supports multiple connected devices with dynamic dashboard cards.
- Organizes mice and keyboards separately from headphones, audio, and Bluetooth accessories.
- Uses model-specific artwork when available and polished generic mouse or keyboard artwork as a fallback.
- Includes dedicated AirPods Pro artwork for matching Bluetooth devices.
- Provides per-device detail pages, low-battery warnings, and charging status when available.
- Continues monitoring from the Windows system tray.
- Offers optional automatic startup with Windows.
- Uses a lightweight native WPF interface with no web runtime.

## Supported devices

Universal Device Hub uses standard Windows SetupAPI and HID information, so general detection works with a broad range of peripherals:

| Device type | Detection | Battery support |
| --- | --- | --- |
| USB mice | Yes | When Windows or a supported protocol exposes it |
| Wireless receiver mice | Yes | When Windows or a supported protocol exposes it |
| Bluetooth mice | Yes | When Windows exposes the battery property |
| USB and wireless keyboards | Yes | When Windows exposes the battery property |
| Bluetooth accessories | Yes | When Windows exposes the battery property |
| AJAZZ/NACODEX NJ07 and NJ08 family | Yes | Direct validated HID battery query |

Battery percentages are never guessed. Vendor-specific devices may require an additional protocol provider before their charge level can be read.

## Download and run

Download the latest Windows x64 release, extract it if necessary, and launch `UniversalDeviceHub.exe`. The self-contained portable build does not require a separate .NET installation.

Windows 10 and Windows 11 are supported where the underlying Windows device APIs provide the required information.

## Build from source

### Requirements

- Windows 10 or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022+ with the .NET desktop workload, or the `dotnet` CLI

### Build

```powershell
git clone https://github.com/shahadat-hossion/universal-device-hub.git
cd universal-device-hub
dotnet build .\UniversalMouseHub.slnx -c Release
dotnet run --project .\UniversalMouseHub\UniversalMouseHub.csproj
```

### Create the self-contained portable executable

```powershell
dotnet publish .\UniversalMouseHub\UniversalMouseHub.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\artifacts\portable\win-x64
```

## How battery monitoring works

The application checks battery sources in this order:

1. A validated vendor protocol provider for the detected hardware.
2. The standard Windows device battery-life property on the device node.
3. Battery properties exposed by sibling nodes in the same physical device container.
4. A clear unavailable state if no trustworthy percentage is returned.

This prevents false battery readings and keeps current remaining charge separate from battery health or capacity degradation.

## Architecture

- **WPF and .NET:** native Windows desktop UI and application lifecycle.
- **Windows SetupAPI and Configuration Manager:** connected-device enumeration, PnP hierarchy traversal, device-container grouping, and connection classification.
- **HidSharp:** guarded vendor HID communication for supported peripherals.
- **System.Windows.Forms NotifyIcon:** Windows notification-area integration.

## Contributing

Contributions are welcome, especially tested battery providers for additional mouse, keyboard, headset, and Bluetooth accessory models. See [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a pull request.

When adding a vendor protocol, include hardware evidence, identify the exact VID/PID and HID collection, validate the percentage range, and preserve the rule that unknown data must remain unknown.

## Privacy

Universal Device Hub runs locally. It does not require an account, upload device information, or include analytics. Device data stays on your Windows computer.

## License

Universal Device Hub is available under the [MIT License](LICENSE).

## Keywords

Windows device manager, Bluetooth battery monitor, wireless mouse battery percentage, keyboard battery monitor, USB device dashboard, Windows peripheral manager, HID device monitor, system tray battery app, open-source Windows utility, AJAZZ NJ07 battery.
