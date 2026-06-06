using SlideProRelay.Server.Startup;
using System.Text.Json;

namespace SlideProRelay.Mac;

public sealed class SettingsPanel : NSPanel
{
    private readonly NSTextField _statusLabel;
    private readonly NSTextField _localUrlLabel;
    private readonly NSTextField _networkUrlLabel;
    private readonly NSImageView _qrImageView;
    private readonly NSTextField _pro7PortField;
    private readonly NSTextField _relayPortField;
    private readonly NSButton _captureCheckbox;
    private readonly NSPopUpButton _displayPopup;
    private List<int> _displayIndices = [0];
    private readonly NSTextField _saveMessage;
    private readonly Action<MacSettings> _onSave;
    private MacSettings _settings;

    // The URL the currently displayed QR encodes. Used to avoid reloading (and
    // flickering) the QR on every status tick — we only refetch when it changes.
    private string? _qrKey;
    private bool _qrLoading;

    public SettingsPanel(MacSettings settings, Action<MacSettings> onSave)
        : base(
            new CGRect(0, 0, 360, 604),
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

        v.AddSubview(SectionLabel("PROPRESENTER STATUS", new CGRect(x, 576, w, 14)));

        _statusLabel = TextField("Not detected", new CGRect(x, 554, w, 18));
        v.AddSubview(_statusLabel);

        // ── URLs ──────────────────────────────────────────────────────────────

        v.AddSubview(SectionLabel("PHONE URL", new CGRect(x, 528, w, 14)));

        _localUrlLabel = LinkField($"http://localhost:{settings.RelayPort}", new CGRect(x, 504, w, 18));
        v.AddSubview(_localUrlLabel);

        _networkUrlLabel = LinkField("Detecting…", new CGRect(x, 484, w, 18));
        v.AddSubview(_networkUrlLabel);

        var openBtn = new NSButton(new CGRect(x, 454, 150, 22));
        openBtn.Title = "Open in Browser";
        openBtn.BezelStyle = NSBezelStyle.Rounded;
        openBtn.Activated += (_, _) =>
            NSWorkspace.SharedWorkspace.OpenUrl(
                new NSUrl($"http://localhost:{_settings.RelayPort}"));
        v.AddSubview(openBtn);

        // QR code — always visible, centered, scan to open the phone URL.
        _qrImageView = new NSImageView(new CGRect(80, 244, 200, 200));
        _qrImageView.ImageScaling = NSImageScale.ProportionallyUpOrDown;
        v.AddSubview(_qrImageView);

        // ── Settings ──────────────────────────────────────────────────────────

        v.AddSubview(SectionLabel("SETTINGS", new CGRect(x, 214, w, 14)));

        v.AddSubview(BodyLabel("ProPresenter Port:", new CGRect(x, 188, 160, 18)));
        _pro7PortField = EditableField(settings.ProPresenterPort.ToString(), new CGRect(190, 185, 80, 22));
        v.AddSubview(_pro7PortField);

        v.AddSubview(BodyLabel("Relay Port (phone URL):", new CGRect(x, 160, 160, 18)));
        _relayPortField = EditableField(settings.RelayPort.ToString(), new CGRect(190, 157, 80, 22));
        v.AddSubview(_relayPortField);

        _captureCheckbox = new NSButton(new CGRect(x, 128, w, 20));
        _captureCheckbox.SetButtonType(NSButtonType.Switch);
        _captureCheckbox.Title = "Capture output screen on slide change";
        _captureCheckbox.State = settings.ScreenCaptureEnabled ? NSCellStateValue.On : NSCellStateValue.Off;
        v.AddSubview(_captureCheckbox);

        v.AddSubview(SectionLabel("CAPTURE DISPLAY", new CGRect(x, 104, w, 14)));
        _displayPopup = new NSPopUpButton(new CGRect(x, 76, w, 26), false);
        _displayPopup.AddItem("Auto — match audience");
        v.AddSubview(_displayPopup);

        // ── Save ──────────────────────────────────────────────────────────────

        var saveBtn = new NSButton(new CGRect(x, 38, w, 32));
        saveBtn.Title = "Save & Restart";
        saveBtn.BezelStyle = NSBezelStyle.Rounded;
        saveBtn.KeyEquivalent = "\r";
        saveBtn.Activated += OnSave;
        v.AddSubview(saveBtn);

        _saveMessage = TextField("", new CGRect(x, 14, w, 16));
        _saveMessage.TextColor = NSColor.SystemGreen;
        _saveMessage.Font = NSFont.SystemFontOfSize(11);
        v.AddSubview(_saveMessage);

        // Kick off the initial QR + display loads so they appear without waiting.
        RefreshQr();
        _ = LoadDisplaysAsync();
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

            RefreshQr();
        });
    }

    // Reload the QR only when the encoded URL actually changes — never clears the
    // existing image, so the QR stays rock-steady (no per-tick flicker).
    private void RefreshQr()
    {
        var lan = NetworkUrlPrinter.GetLanIp();
        var key = lan is not null
            ? $"http://{lan}:{_settings.RelayPort}"
            : $"http://localhost:{_settings.RelayPort}";

        if (key == _qrKey || _qrLoading)
            return;

        _ = LoadQrImageAsync(key);
    }

    private async Task LoadQrImageAsync(string key)
    {
        _qrLoading = true;
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var bytes = await client.GetByteArrayAsync($"http://localhost:{_settings.RelayPort}/api/qr");
            var data = Foundation.NSData.FromArray(bytes);
            var image = new AppKit.NSImage(data);
            InvokeOnMainThread(() =>
            {
                _qrImageView.Image = image;
                _qrKey = key; // only mark success once the image is actually set
            });
        }
        catch { /* server not ready yet — RefreshQr will retry on the next tick */ }
        finally { _qrLoading = false; }
    }

    // Fetch the capturable displays from the running relay so the user can pick
    // which screen (e.g. ProPresenter's audience output) to capture.
    private async Task LoadDisplaysAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = await client.GetStringAsync($"http://localhost:{_settings.RelayPort}/api/displays");
            var (titles, indices) = ParseDisplays(json);

            InvokeOnMainThread(() =>
            {
                _displayPopup.RemoveAllItems();
                _displayPopup.AddItems([.. titles]);
                _displayIndices = indices;

                var sel = indices.IndexOf(_settings.CaptureDisplayIndex);
                _displayPopup.SelectItem(sel >= 0 ? sel : 0);
            });
        }
        catch { /* server not ready yet — the placeholder Auto item stays */ }
    }

    private static (List<string> Titles, List<int> Indices) ParseDisplays(string json)
    {
        var titles = new List<string> { "Auto — match audience" };
        var indices = new List<int> { 0 };

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("displays", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in arr.EnumerateArray())
            {
                var idx = d.GetProperty("index").GetInt32();
                var width = d.GetProperty("width").GetInt32();
                var height = d.GetProperty("height").GetInt32();
                var primary = d.GetProperty("isPrimary").GetBoolean();
                var audience = d.TryGetProperty("matchesAudience", out var m) && m.GetBoolean();
                var tag = primary ? " (primary)" : audience ? " (audience)" : "";
                titles.Add($"Display {idx} — {width}×{height}{tag}");
                indices.Add(idx);
            }
        }

        return (titles, indices);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (int.TryParse(_pro7PortField.StringValue, out var pro7) && pro7 is > 0 and < 65536)
            _settings.ProPresenterPort = pro7;

        if (int.TryParse(_relayPortField.StringValue, out var relay) && relay is > 0 and < 65536)
            _settings.RelayPort = relay;

        _settings.ScreenCaptureEnabled = _captureCheckbox.State == NSCellStateValue.On;

        var sel = (int)_displayPopup.IndexOfSelectedItem;
        _settings.CaptureDisplayIndex = sel >= 0 && sel < _displayIndices.Count ? _displayIndices[sel] : 0;

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
