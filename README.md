# Number Wheel · 数字滚轮

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

## 中文

在 [Releases](../../releases) 下载并运行 `NumberWheel.exe`。左键点击托盘图标可打开试用窗口，右键可暂停、恢复或退出。

点击输入框，再把鼠标指针放到数字上直接滚动。整数每格增减 1，小数按末位增减。指向文字或空白时，滚轮照常工作。已验证 Windows Forms 输入框和 Chrome 网页文本框。部分自绘控件和管理员权限窗口可能无法使用。

## License

[MIT](LICENSE)
