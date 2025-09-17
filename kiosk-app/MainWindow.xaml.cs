using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;

namespace KioskApp;

public partial class MainWindow : Window
{
    private readonly WinForms.NotifyIcon _trayIcon;
    private readonly KioskConfig _config;

    public MainWindow()
    {
        InitializeComponent();

        // set initial window to fit primary work area (screen minus taskbar/status bar)
        // this.WindowStartupLocation = WindowStartupLocation.Manual;
        // this.Left = SystemParameters.WorkArea.Left;
        // this.Top = SystemParameters.WorkArea.Top;
        // this.Width = SystemParameters.WorkArea.Width; // e.g., 1920
        // this.Height = SystemParameters.WorkArea.Height; // e.g., 1200 minus taskbar

        var cfgPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        var cfg = new KioskConfig("http://shai571a:5173/", false);
        if (File.Exists(cfgPath))
        {
            try { cfg = JsonSerializer.Deserialize<KioskConfig>(File.ReadAllText(cfgPath)) ?? cfg; } catch { }
        }
        _config = cfg;

        _trayIcon = new WinForms.NotifyIcon();
        _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (s, e) => ShowWindow();

        var cm = new WinForms.ContextMenuStrip();
        var openItem = new WinForms.ToolStripMenuItem("Open");
        openItem.Click += (s, e) => ShowWindow();
        var hideItem = new WinForms.ToolStripMenuItem("Hide");
        hideItem.Click += (s, e) => HideWindow();
        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => { _trayIcon.Visible = false; System.Windows.Application.Current.Shutdown(); };
        cm.Items.Add(openItem);
        cm.Items.Add(hideItem);
        cm.Items.Add(new WinForms.ToolStripSeparator());
        cm.Items.Add(exitItem);
        _trayIcon.ContextMenuStrip = cm;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            if (!string.IsNullOrEmpty(_config.Url)) WebView.CoreWebView2.Navigate(_config.Url);
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("WebView init failed: " + ex.Message);
        }

        // pure-WPF: do not modify window styles via Win32. Keep standard titlebar buttons visible
        // but intercept close actions in Closing handler to hide to tray instead of exiting.

        // intercept routed close commands (e.g., system menu) as well
        CommandBindings.Add(new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Close, (s, ea) => { ea.Handled = true; HideWindow(); }));

        // hook non-client double-click (title bar double click) to toggle maximize/restore via WndProc hook
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var src = HwndSource.FromHwnd(hwnd);
        if (src != null) src.AddHook(WndProc);

        if (_config.StartMinimized)
        {
            HideWindow();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCLBUTTONDBLCLK = 0x00A3;
        if (msg == WM_NCLBUTTONDBLCLK)
        {
            ToggleMaximizeRestore();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleMaximizeRestore()
    {
        if (WindowState == WindowState.Normal)
        {
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            // ensure restored size fits work area
            this.Left = SystemParameters.WorkArea.Left;
            this.Top = SystemParameters.WorkArea.Top;
            this.Width = SystemParameters.WorkArea.Width;
            this.Height = SystemParameters.WorkArea.Height;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // prevent user from closing via X or Alt+F4
        if (e != null)
        {
            e.Cancel = true;
            HideWindow();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // suppress Alt+F4
        if (e.Key == Key.F4 && (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)))
        {
            e.Handled = true;
            HideWindow();
        }
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideWindow()
    {
        Hide();
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

public record KioskConfig(string Url, bool StartMinimized);
