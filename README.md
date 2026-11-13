# Number Wheel

Scroll over a number in the active text field to adjust it. No middle-click, modifier key, or text selection is needed.

## Run

Download `NumberWheel.exe` from [Releases](../../releases) and open it on x64 Windows 10 (version 1703 or later) or Windows 11 with .NET Framework 4.x. Left-click the tray icon to open the test window. Right-click it to pause, resume, or quit.

1. Click into an editable text field.
2. Hover directly over the number. Scroll up to increase it or down to decrease it.
3. Each wheel notch changes an integer by one or changes a decimal by its last displayed digit. For example, `-0.25` scrolls up to `-0.24`.

The tool uses Windows UI Automation to locate text and replaces the number through the active editor. Text is processed locally in memory. The app has no network calls, clipboard access, or startup registration. The release executable is unsigned.

Verified with a Windows Forms text field and a Chrome HTML text input. The focused field must expose editable text and its on-screen bounds through UI Automation. Some custom editors, native parameter panels, and elevated windows may be incompatible. Wheel input passes through when the app cannot identify an editable number.

## Build

Run from the repository in PowerShell on x64 Windows. The build uses the C# compiler and assemblies included with .NET Framework.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The output is `dist\NumberWheel.exe`. The probes in `tests` can be built with `-Target NativeTest` and `-Target BrowserTest`. Run them on the interactive desktop with Number Wheel running. For the browser probe, open `tests/fixture.html` in Chrome and focus its input field. Both probes temporarily move the mouse pointer and send wheel input to their fixture.

## License

[MIT](LICENSE)
