using AppKit;
using CoreGraphics;
using Foundation;
using SlideProRelay.Server.Startup;

namespace SlideProRelay.Mac;

public sealed class SettingsPanel : NSPanel
{
    private readonly NSTextField _statusLabel;
    private readonly NSTextField _localUrlLabel;
    private readonly NSTextField _networkUrlLabel;
    private readonly NSButton _qrButton;
    private readonly NSImageView _qrImageView;
    private readonly NSTextField _pro7PortField;
    private readonly NSTextField _relayPortField;
    private readonly NSTextField _saveMessage;
    private readonly Action<MacSettings> _onSave;
    private MacSettings _settings;
    private bool _qrVisible;

    public SettingsPanel(MacSettings settings, Action<MacSettings> onSave)
        : base(
            new CGRect(0, 0, 360, 318),
            NSWindowStyle.Titled | NSWindowStyle.Closable,
            NSBackingStore.Buffered,
            false)
    {
        _settings = settings;
        _onSave = onSave;

        Title = "SlideProRelay";
        FloatingPanel = true;
        HidesOnDeactivate = false;
        ReleasedWhenClosed = false;

        var v = ContentView!;
        var w = 320f;
        var x = 20f;

        // ── Status ────────────────────────────────────────────────────────────

        v.AddSubview(SectionLabel("PROPRESENTER STATUS", new CGRect(x, 278, w, 14)));

        _statusLabel = TextField("Not detected", new CGRect(x, 256, w, 18));
        v.AddSubview(_statusLabel);

        // ── URLs ──────────────────────────────────────────────────────────────

        v.AddSubview(SectionLabel("PHONE URL", new CGRect(x, 228, w, 14)));

        _localUrlLabel = LinkField($"http://localhost:{settings.RelayPort}", new CGRect(x, 208, w, 18));
        v.AddSubview(_localUrlLabel);

        _networkUrlLabel = LinkField("Detecting…", new CGRect(x, 188, w, 18));
        v.AddSubview(_networkUrlLabel);

        var openBtn = new NSButton(new CGRect(x, 162, 150, 22));
        openBtn.Title = "Open in Browser";
        openBtn.BezelStyle = NSBezelStyle.Rounded;
        openBtn.Activated += (_, _) =>
            NSWorkspace.SharedWorkspace.OpenUrl(
                new NSUrl($"http://localhost:{_settings.RelayPort}"));
        v.AddSubview(openBtn);

        _qrButton = new NSButton(new CGRect(x + 158, 162, 120, 22));
        _qrButton.Title = "Show QR Code";
        _qrButton.BezelStyle = NSBezelStyle.Rounded;
        _qrButton.Activated += ToggleQr;
        v.AddSubview(_qrButton);

        _qrImageView = new NSImageView(new CGRect(x, -10, 200, 200));
        _qrImageView.ImageScaling = NSImageScale.ProportionallyUpOrDown;
        _qrImageView.Hidden = true;
        v.AddSubview(_qrImageView);

        // ── Settings ──────────────────────────────────────────────────────────

        v.AddSubview(SectionLabel("SETTINGS", new CGRect(x, 132, w, 14)));

        v.AddSubview(BodyLabel("ProPresenter Port:", new CGRect(x, 110, 160, 18)));
        _pro7PortField = EditableField(settings.ProPresenterPort.ToString(), new CGRect(190, 108, 80, 22));
        v.AddSubview(_pro7PortField);

        v.AddSubview(BodyLabel("Relay Port (phone URL):", new CGRect(x, 82, 160, 18)));
        _relayPortField = EditableField(settings.RelayPort.ToString(), new CGRect(190, 80, 80, 22));
        v.AddSubview(_relayPortField);

        // ── Save ──────────────────────────────────────────────────────────────

        var saveBtn = new NSButton(new CGRect(x, 36, w, 32));
        saveBtn.Title = "Save & Restart";
        saveBtn.BezelStyle = NSBezelStyle.Rounded;
        saveBtn.KeyEquivalent = "\r";
        saveBtn.Activated += OnSave;
        v.AddSubview(saveBtn);

        _saveMessage = TextField("", new CGRect(x, 14, w, 16));
        _saveMessage.TextColor = NSColor.SystemGreen;
        _saveMessage.Font = NSFont.SystemFontOfSize(11);
        v.AddSubview(_saveMessage);
    }

    public void UpdateStatus(bool connected, IReadOnlyList<string> urls)
    {
        InvokeOnMainThread(() =>
        {
            _statusLabel.StringValue = connected ? "● Connected" : "○ Not detected";
            _statusLabel.TextColor = connected ? NSColor.SystemGreen : NSColor.SystemRed;

            _localUrlLabel.StringValue = $"http://localhost:{_settings.RelayPort}";

            var lan = NetworkUrlPrinter.GetLanIp();
            _networkUrlLabel.StringValue = lan is not null
                ? $"http://{lan}:{_settings.RelayPort}"
                : "Network address not found";

            _qrImageView.Image = null;
            if (_qrVisible)
                _ = LoadQrImageAsync();
        });
    }

    private void ToggleQr(object? sender, EventArgs e)
    {
        _qrVisible = !_qrVisible;
        _qrButton.Title = _qrVisible ? "Hide QR Code" : "Show QR Code";
        _qrImageView.Hidden = !_qrVisible;

        var f = Frame;
        const float qrHeight = 220f;
        if (_qrVisible)
        {
            _qrImageView.Frame = new CGRect(20, 10, 200, 200);
            f.Y -= qrHeight;
            f.Height += qrHeight;
        }
        else
        {
            f.Y += qrHeight;
            f.Height -= qrHeight;
        }
        SetFrame(f, true);

        if (_qrVisible && _qrImageView.Image is null)
            _ = LoadQrImageAsync();
    }

    private async Task LoadQrImageAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var bytes = await client.GetByteArrayAsync($"http://localhost:{_settings.RelayPort}/api/qr");
            var data = Foundation.NSData.FromArray(bytes);
            var image = new AppKit.NSImage(data);
            InvokeOnMainThread(() => _qrImageView.Image = image);
        }
        catch { /* server not ready yet */ }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (int.TryParse(_pro7PortField.StringValue, out var pro7) && pro7 is > 0 and < 65536)
            _settings.ProPresenterPort = pro7;

        if (int.TryParse(_relayPortField.StringValue, out var relay) && relay is > 0 and < 65536)
            _settings.RelayPort = relay;

        _saveMessage.StringValue = "Restarting…";
        _onSave(_settings);
        _saveMessage.StringValue = "Saved!";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NSTextField SectionLabel(string text, CGRect frame)
    {
        var f = new NSTextField(frame);
        f.StringValue = text;
        f.Font = NSFont.SystemFontOfSize(10);
        f.TextColor = NSColor.SecondaryLabel;
        f.Bezeled = false;
        f.DrawsBackground = false;
        f.Editable = false;
        f.Selectable = false;
        return f;
    }

    private static NSTextField BodyLabel(string text, CGRect frame)
    {
        var f = new NSTextField(frame);
        f.StringValue = text;
        f.Font = NSFont.SystemFontOfSize(13);
        f.Bezeled = false;
        f.DrawsBackground = false;
        f.Editable = false;
        f.Selectable = false;
        return f;
    }

    private static NSTextField TextField(string text, CGRect frame)
    {
        var f = new NSTextField(frame);
        f.StringValue = text;
        f.Bezeled = false;
        f.DrawsBackground = false;
        f.Editable = false;
        f.Selectable = false;
        return f;
    }

    private static NSTextField LinkField(string text, CGRect frame)
    {
        var f = TextField(text, frame);
        f.TextColor = NSColor.Link;
        f.Selectable = true;
        return f;
    }

    private static NSTextField EditableField(string value, CGRect frame)
    {
        var f = new NSTextField(frame);
        f.StringValue = value;
        return f;
    }
}
