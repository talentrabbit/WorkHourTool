Kiosk App

This is a minimal Windows kiosk-style native app that shows an embedded browser window and prevents accidental closure. It reads configuration from config.json and stores a URL to open.

Features:
- Single borderless window containing an embedded WebView2 control.
- Configurable startup URL from config.json.
- Hides the address bar.
- Disables the close (X) button and Alt+F4.
- Adds a system tray icon with context menu: Open, Minimize (Hide), Exit.
- Double-click tray icon or choose Open will show the window.
- Selecting Minimize will hide the window to tray.

Implementation notes:
- Built as a .NET 9 Windows Desktop app (WinForms) using Microsoft.Web.WebView2.
- Requires WebView2 runtime installed on the target system.
- config.json example provided below.

Run:
- Build with `dotnet build` and run.
- Requires Visual Studio or dotnet SDK and Windows OS.
