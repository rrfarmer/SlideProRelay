using SlideProRelay.Server;
using SlideProRelay.Server.ProPresenter.Models;

namespace SlideProRelay.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Icon _iconConnected;
    private readonly Icon _iconDisconnected;
    private RelayHost? _host;
    private TraySettings _settings;

    public TrayApplicationContext()
    {
        _settings = TraySettings.Load();

        _iconConnected    = BuildIcon(connected: true);
        _iconDisconnected = BuildIcon(connected: false);

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open in Browser", null, OnOpenBrowser);
        _menu.Items.Add("Settings…", null, OnOpenSettings);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, OnExit);

        _tray = new NotifyIcon
        {
            Icon = _iconDisconnected,
            Visible = true,
            Text = "SlideProRelay — starting…",
            ContextMenuStrip = _menu,
        };
        _tray.DoubleClick += (_, _) => OnOpenSettings(null, EventArgs.Empty);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
        _timer.Start();

        _ = StartHostAsync(_settings);
        _ = CheckForUpdateAsync();
    }

    // ── Host lifecycle ────────────────────────────────────────────────────────

    private async Task StartHostAsync(TraySettings settings)
    {
        // Null _host first so the timer guard (`if (_host is null) return`) fires
        // during the entire stop/dispose window, preventing access to a disposed IServiceProvider.
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
        _settings = TraySettings.Load();
        _ = StartHostAsync(_settings);
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_host is null) return;
        var connected = _host.Cache?.Latest?.Connection == ConnectionState.Connected;
        _tray.Icon = connected ? _iconConnected : _iconDisconnected;
        _tray.Text = connected
            ? "SlideProRelay — ProPresenter connected"
            : "SlideProRelay — ProPresenter not detected";
    }

    // ── Menu handlers ─────────────────────────────────────────────────────────

    private void OnOpenBrowser(object? sender, EventArgs e) =>
        OpenUrl($"http://localhost:{_settings.RelayPort}");

    private void OnOpenSettings(object? sender, EventArgs e) =>
        OpenUrl($"http://localhost:{_settings.RelayPort}/settings");

    private static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });

    // ── Update ────────────────────────────────────────────────────────────────

    private async Task CheckForUpdateAsync()
    {
        var info = await UpdateChecker.CheckAsync();
        if (info is null) return;
        SynchronizationContext.Current?.Post(_ => OnUpdateFound(info), null);
    }

    private void OnUpdateFound(UpdateChecker.UpdateInfo info)
    {
        var item = new ToolStripMenuItem($"Update to {info.Version}…")
        {
            Font = new Font(_menu.Font, FontStyle.Bold),
        };
        item.Click += async (_, _) => await OnUpdateClickedAsync(info, item);

        _menu.Items.Insert(0, new ToolStripSeparator());
        _menu.Items.Insert(0, item);

        _tray.ShowBalloonTip(
            10_000,
            "Update available",
            $"SlideProRelay {info.Version} is ready. Open the tray menu to install.",
            ToolTipIcon.Info);
    }

    private async Task OnUpdateClickedAsync(UpdateChecker.UpdateInfo info, ToolStripMenuItem item)
    {
        item.Enabled = false;
        item.Text = "Downloading…";

        try
        {
            await UpdateChecker.DownloadAndInstallAsync(info, pct =>
                SynchronizationContext.Current?.Post(_ => item.Text = $"Downloading… {pct}%", null));
        }
        catch
        {
            item.Enabled = true;
            item.Text = $"Update to {info.Version}… (retry)";
            _tray.ShowBalloonTip(
                5_000,
                "Update failed",
                "Could not download the update. Check your connection and try again.",
                ToolTipIcon.Error);
        }
    }

    private async void OnExit(object? sender, EventArgs e)
    {
        _tray.Visible = false;
        _timer.Stop();

        if (_host is not null)
        {
            await _host.StopAsync();
            await _host.DisposeAsync();
        }

        Application.Exit();
    }

    // ── Icon ──────────────────────────────────────────────────────────────────

    private static Icon BuildIcon(bool connected)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bg = new SolidBrush(Color.FromArgb(45, 45, 48));
        g.FillRectangle(bg, 0, 0, 32, 32);

        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var textRect = new RectangleF(0, 4, 24, 18);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("P7", font, textBrush, textRect, sf);

        using var dot = new SolidBrush(connected ? Color.FromArgb(100, 220, 100) : Color.FromArgb(220, 80, 60));
        g.FillEllipse(dot, 20, 20, 11, 11);

        var hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        NativeMethods.DestroyIcon(hIcon);
        return icon;
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
            _iconConnected.Dispose();
            _iconDisconnected.Dispose();
        }
        base.Dispose(disposing);
    }
}
