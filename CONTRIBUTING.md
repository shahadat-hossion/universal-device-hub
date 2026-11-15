# Contributing to Universal Device Hub

Thank you for helping improve Universal Device Hub.

## Development workflow

1. Fork the repository and create a focused feature branch.
2. Build with `dotnet build .\UniversalMouseHub.slnx -c Release`.
3. Test device discovery on real Windows hardware.
4. Keep unknown battery data as `null`; never insert a placeholder percentage.
5. Submit a pull request describing the hardware and verification performed.

## Adding device-protocol support

Include the vendor ID, product ID, target HID collection, report format, and evidence that the returned byte is current remaining charge. Validate that the result is between 0 and 100 and fail safely when the device does not answer.

Avoid sending configuration-changing reports in a battery-only provider. Device writes must be narrowly scoped and documented.

## Code style

- Keep the app lightweight and dependency-conscious.
- Prefer Windows-supported APIs for generic discovery.
- Keep vendor-specific logic isolated from the generic device monitor.
- Build without warnings before opening a pull request.
