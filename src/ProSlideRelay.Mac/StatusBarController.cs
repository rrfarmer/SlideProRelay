using AppKit;
using CoreGraphics;
using Foundation;
using ProSlideRelay.Server;
using ProSlideRelay.Server.ProPresenter;
using ProSlideRelay.Server.ProPresenter.Models;
using ProSlideRelay.Server.Startup;

namespace ProSlideRelay.Mac;

public sealed class StatusBarController : NSObject
{
    private readonly NSStatusItem _item;
    private readonly NSMenuItem _statusMenuItem;
    private readonly NSMenuItem _loginMenuItem;
    private RelayHost? _host;
    private MacSettings _settings;
    private SettingsPanel? _settingsPanel;

    private static readonly string LaunchAgentId = "com.prosliderlay.app";
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

        var quitItem = new NSMenuItem("Quit ProSlideRelay");
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
                    <string>/Applications/ProSlideRelay.app/Contents/MacOS/ProSlideRelay</string>
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.StopAsync().GetAwaiter().GetResult();
            _host?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _item.Dispose();
        }
        base.Dispose(disposing);
    }
}
