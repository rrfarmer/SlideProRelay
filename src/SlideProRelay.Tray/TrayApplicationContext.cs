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
    private readonly SynchronizationContext _syncContext =
        SynchronizationContext.Current ?? new SynchronizationContext();
    private RelayHost? _host;
    private StatusWindow? _window;
    private TraySettings _settings;

    public TrayApplicationContext()
    {
        _settings = TraySettings.Load();

        _iconConnected = BuildIcon(connected: true);
        _iconDisconnected = BuildIcon(connected: false);

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open in Browser", null, OnOpenBrowser);
        _menu.Items.Add("Settings / Status", null, OnShowWindow);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, OnExit);

        _tray = new NotifyIcon
        {
            Icon = _iconDisconnected,
            Visible = true,
            Text = "SlideProRelay — starting…",
            ContextMenuStrip = _menu,
        };
        _tray.DoubleClick += (_, _) => OnShowWindow(null, EventArgs.Empty);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
        _timer.Start();

        _ = StartHostAsync(_settings);
        _ = CheckForUpdateAsync();
    }

    // ── Host lifecycle ────────────────────────────────────────────────────────

    private async Task StartHostAsync(TraySettings settings)
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

    // ── Timer ─────────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        if (_host is null) return;

        var cache = _host.Cache;
        var connected = cache?.Latest?.Connection == ConnectionState.Connected;

        _tray.Icon = connected ? _iconConnected : _iconDisconnected;
        _tray.Text = connected
            ? "SlideProRelay — ProPresenter connected"
            : "SlideProRelay — ProPresenter not detected";

        var activePort = _host?.ActiveProPresenterPort ?? _settings.ProPresenterPort;
        _window?.UpdateStatus(connected, _host?.Urls ?? [], activePort);
    }

    // ── Menu handlers ─────────────────────────────────────────────────────────

    private void OnOpenBrowser(object? sender, EventArgs e)
    {
        var url = $"http://localhost:{_settings.RelayPort}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private void OnShowWindow(object? sender, EventArgs e)
    {
        if (_window is null || _window.IsDisposed)
            _window = new StatusWindow(_settings, OnSaveSettings);

        if (!_window.Visible)
            _window.Show();

        _window.BringToFront();
    }

    private void OnSaveSettings(TraySettings newSettings)
    {
        _settings = newSettings;
        TraySettings.Save(newSettings);
        _ = StartHostAsync(newSettings);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private async Task CheckForUpdateAsync()
    {
        var info = await UpdateChecker.CheckAsync();
        if (info is null) return;
        _syncContext.Post(_ => OnUpdateFound(info), null);
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
                _syncContext.Post(_ => item.Text = $"Downloading… {pct}%", null));
            // Application.Exit() is called inside DownloadAndInstallAsync on success.
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

        // Dark rounded background
        using var bg = new SolidBrush(Color.FromArgb(45, 45, 48));
        g.FillRectangle(bg, 0, 0, 32, 32);

        // "P7" label
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var textRect = new RectangleF(0, 4, 24, 18);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("P7", font, textBrush, textRect, sf);

        // Status dot
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
