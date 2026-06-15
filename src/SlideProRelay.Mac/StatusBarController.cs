using SlideProRelay.Server;
using SlideProRelay.Server.ProPresenter.Models;

namespace SlideProRelay.Mac;

public sealed class StatusBarController : NSObject
{
    private readonly NSStatusItem _item;
    private readonly NSMenuItem _statusMenuItem;
    private readonly NSMenuItem _loginMenuItem;
    private RelayHost? _host;
    private MacSettings _settings;

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
        settingsItem.Activated += (_, _) => OpenSettings();
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
        // Null _host first so the status timer guard fires during stop/dispose,
        // preventing access to a disposed IServiceProvider.
        var old = _host;
        _host = null;
        if (old is not null)
        {
            old.RestartRequested -= OnRestartRequested;
            await old.StopAsync();
            await old.DisposeAsync();
        }

        _host = RelayHost.Create(settings.ToConfigOverrides());
        _host.RestartRequested += OnRestartRequested;
        await _host.StartAsync();
    }

    private void OnRestartRequested()
    {
        _settings = MacSettings.Load();
        _ = StartHostAsync(_settings);
    }

    // ── Status ticker ─────────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        var connected = _host?.Cache.Latest?.Connection == ConnectionState.Connected;
        _statusMenuItem.Title = connected
            ? "● ProPresenter connected"
            : "○ ProPresenter not detected";
    }

    // ── Menu actions ──────────────────────────────────────────────────────────

    private void OpenBrowser() =>
        NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl($"http://localhost:{_settings.RelayPort}"));

    private void OpenSettings() =>
        NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl($"http://localhost:{_settings.RelayPort}/settings"));

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
        image.Template = true;

        return image;
    }

    /// <summary>
    /// Stops the embedded web server on a background thread, then invokes
    /// <paramref name="onComplete"/> on the main thread.
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
            catch { }
        }).ContinueWith(_ => InvokeOnMainThread(onComplete));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _item.Dispose();
        base.Dispose(disposing);
    }
}
