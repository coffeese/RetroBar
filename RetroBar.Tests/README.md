# Monitor selection regression tests

Run this console test runner, which needs no additional test framework (a failure returns exit code 1):

```powershell
dotnet run --project RetroBar.Tests -c Release -f net10.0-windows
dotnet run --project RetroBar.Tests -c Release -f net6.0-windows
```

The tests compile the actual `MonitorSelection`, `Settings`, and `SettingsManager`
sources, and use the same ManagedShell version as RetroBar. Only the application-data
path extension is replaced with a private temporary directory. The user's settings
and desktop are never modified. No additional test framework package is needed.

These tests cover selection, old JSON compatibility, precedence of All, disconnect /
reconnect, primary changes, independent Start menu targets, and settings persistence. They do not create AppBar windows
or exercise the Windows shell, notification icons, DPI, or physical display changes.
