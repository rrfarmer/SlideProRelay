using AppKit;
using CoreGraphics;
using Foundation;
using SlideProRelay.Server;
using SlideProRelay.Server.ProPresenter;
using SlideProRelay.Server.ProPresenter.Models;
using SlideProRelay.Server.Startup;

namespace SlideProRelay.Mac;

public sealed class StatusBarController : NSObject
{
    private readonly NSStatusItem _item;
    private readonly NSMenuItem _statusMenuItem;
    private readonly NSMenuItem _loginMenuItem;
    private RelayHost? _host;
    private MacSettings _settings;
    private SettingsPanel? _settingsPanel;

    private static readonly string LaunchAgentId = "io.slidepro.relay";
    private static readonly string LaunchAgentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{LaunchAgentId}.plist");

    public StatusBarController()
    {
        _settings = MacSettings.Load();

        _item = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
        _item.Button.Image = BuildMenuBarImage();
        _item.Button.ImagePosition = NSCellImagePosition.ImageLeft;

        // ── Menu ─────────────────────────────────────────────────────────────

        var menu = new NSMenu();

        _statusMenuItem = new NSMenuItem("Starting…") { Enabled = false };
        menu.AddItem(_statusMenuItem);

        menu.AddItem(NSMenuItem.SeparatorItem);

        var openItem = new NSMenuItem("Open in Browser");
        openItem.Activated += (_, _) => OpenBrowser();
        menu.AddItem(openItem);

        var settingsItem = new NSMenuItem("Settings…");
        settingsItem.Activated += (_, _) => ShowSettings();
        menu.AddItem(settingsItem);

        menu.AddItem(NSMenuItem.SeparatorItem);

        _loginMenuItem = new NSMenuItem("Start at Login");
        _loginMenuItem.Activated += (_, _) => ToggleLoginItem();
        RefreshLoginMenuState();
        menu.AddItem(_loginMenuItem);

        menu.AddItem(NSMenuItem.SeparatorItem);

        var quitItem = new NSMenuItem("Quit SlideProRelay");
        quitItem.Activated += (_, _) => NSApplication.SharedApplication.Terminate(this);
        menu.AddItem(quitItem);

        _item.Menu = menu;

        // ── Start relay + status timer ────────────────────────────────────────

        _ = StartHostAsync(_settings);

        var timer = NSTimer.CreateRepeatingTimer(1.0, _ => UpdateStatus());
        NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
    }

    // ── Host lifecycle ────────────────────────────────────────────────────────

    private async Task StartHostAsync(MacSettings settings)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            await _host.DisposeAsync();
            _host = null;
        }

        _host = RelayHost.Create(settings.ToConfigOverrides());
        await _host.StartAsync();
    }

    // ── Status ticker ─────────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        var connected = _host?.Cache.Latest?.Connection == ConnectionState.Connected;

        _statusMenuItem.Title = connected
            ? "● ProPresenter connected"
            : "○ ProPresenter not detected";

        _settingsPanel?.UpdateStatus(connected, _host?.Urls ?? []);
    }

    // ── Menu actions ──────────────────────────────────────────────────────────

    private void OpenBrowser()
    {
        var url = $"http://localhost:{_settings.RelayPort}";
        NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl(url));
    }

    private void ShowSettings()
    {
        if (_settingsPanel is null || !_settingsPanel.IsVisible)
            _settingsPanel = new SettingsPanel(_settings, OnSaveSettings);

        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
        _settingsPanel.MakeKeyAndOrderFront(null);
        _settingsPanel.Center();
    }

    private void OnSaveSettings(MacSettings newSettings)
    {
        _settings = newSettings;
        MacSettings.Save(newSettings);
        _ = StartHostAsync(newSettings);
    }

    // ── Start at Login (LaunchAgent) ──────────────────────────────────────────

    private void ToggleLoginItem()
    {
        if (File.Exists(LaunchAgentPath))
            DisableLoginItem();
        else
            EnableLoginItem();

        RefreshLoginMenuState();
    }

    private void EnableLoginItem()
    {
        // Write a LaunchAgent plist that points to the installed app location
        var plist = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{LaunchAgentId}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>/Applications/SlideProRelay.app/Contents/MacOS/SlideProRelay</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
                <key>KeepAlive</key>
                <false/>
            </dict>
            </plist>
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(LaunchAgentPath)!);
        File.WriteAllText(LaunchAgentPath, plist);

        using var p = System.Diagnostics.Process.Start("launchctl", $"load \"{LaunchAgentPath}\"");
        p?.WaitForExit();
    }

    private void DisableLoginItem()
    {
        using var p = System.Diagnostics.Process.Start("launchctl", $"unload \"{LaunchAgentPath}\"");
        p?.WaitForExit();

        if (File.Exists(LaunchAgentPath))
            File.Delete(LaunchAgentPath);
    }

    private void RefreshLoginMenuState()
    {
        _loginMenuItem.State = File.Exists(LaunchAgentPath)
            ? NSCellStateValue.On
            : NSCellStateValue.Off;
    }

    // ── Menu bar icon ─────────────────────────────────────────────────────────

    private static NSImage BuildMenuBarImage()
    {
        var image = new NSImage(new CGSize(20, 18));
        image.LockFocus();

        var attrs = new NSMutableDictionary();
        attrs[NSStringAttributeKey.Font] = NSFont.BoldSystemFontOfSize(11);
        attrs[NSStringAttributeKey.ForegroundColor] = NSColor.Label;

        var str = new NSAttributedString("P7", attrs);
        str.DrawAtPoint(new CGPoint(2, 2));

        image.UnlockFocus();
        image.Template = true; // Adapts automatically to light/dark menu bar

        return image;
    }

    /// <summary>
    /// Stops the embedded web server on a background thread, then invokes
    /// <paramref name="onComplete"/> on the main thread. Never blocks the
    /// caller — the app delegate uses this to quit without deadlocking.
    /// </summary>
    public void BeginShutdown(Action onComplete)
    {
        var host = _host;
        _host = null;

        Task.Run(async () =>
        {
            try
            {
                if (host is not null)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await host.StopAsync(cts.Token);
                    await host.DisposeAsync();
                }
            }
            catch { /* best effort — we're quitting anyway */ }
        }).ContinueWith(_ => InvokeOnMainThread(onComplete));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _item.Dispose();
        base.Dispose(disposing);
    }
}
