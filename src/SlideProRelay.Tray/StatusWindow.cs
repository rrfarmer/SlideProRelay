using SlideProRelay.Server.Startup;
using System.Net.Http;
using System.Text.Json;

namespace SlideProRelay.Tray;

internal sealed class StatusWindow : Form
{
    private readonly Label _statusDot;
    private readonly Label _statusText;
    private readonly LinkLabel _localUrl;
    private readonly LinkLabel _networkUrl;
    private readonly PictureBox _qrBox;
    private readonly NumericUpDown _portInput;
    private readonly NumericUpDown _relayPortInput;
    private readonly CheckBox _captureEnabled;
    private readonly ComboBox _displayCombo;
    private readonly CheckBox _slideProEnabled;
    private readonly TextBox _apiKeyInput;
    private readonly ComboBox _presentationCombo;
    private readonly Button _refreshPresentationsButton;
    private readonly Button _saveButton;
    private readonly Label _saveMsg;
    private readonly Action<TraySettings> _onSave;
    private TraySettings _settings;
    private string? _lastQrUrl;

    public StatusWindow(TraySettings settings, Action<TraySettings> onSave)
    {
        _settings = settings;
        _onSave = onSave;

        Text = "SlideProRelay";
        Size = new Size(360, 800);
        MinimumSize = Size;
        MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;

        var padding = new Padding(20, 16, 20, 0);

        // ── Status ───────────────────────────────────────────
        var statusGroup = Section("ProPresenter Status", 12);

        var statusRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0),
        };

        _statusDot = new Label
        {
            Text = "●",
            ForeColor = Color.OrangeRed,
            AutoSize = true,
            Font = new Font("Segoe UI", 11),
            Padding = new Padding(0, 0, 6, 0),
        };

        _statusText = new Label
        {
            Text = "Not detected",
            AutoSize = true,
            Font = new Font("Segoe UI", 10),
        };

        statusRow.Controls.Add(_statusDot);
        statusRow.Controls.Add(_statusText);
        statusGroup.Controls.Add(statusRow);

        // ── URLs ─────────────────────────────────────────────
        var urlGroup = Section("Phone URL  (open on any device on your Wi-Fi)", 8);

        _localUrl = MakeLink("http://localhost:5174");
        _networkUrl = MakeLink("Detecting…");
        urlGroup.Controls.Add(_localUrl);
        urlGroup.Controls.Add(_networkUrl);

        _qrBox = new PictureBox
        {
            Size = new Size(200, 200),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Margin = new Padding(0, 8, 0, 0),
            Visible = true,
        };
        urlGroup.Controls.Add(_qrBox);

        // ── Settings ──────────────────────────────────────────
        var settingsGroup = Section("Settings", 8);

        settingsGroup.Controls.Add(FieldRow("ProPresenter Port:",
            _portInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = settings.ProPresenterPort,
                Width = 90,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
            }));

        settingsGroup.Controls.Add(FieldRow("Relay Port (phone URL):",
            _relayPortInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = settings.RelayPort,
                Width = 90,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
            }));

        _captureEnabled = new CheckBox
        {
            Text = "Capture output screen on slide change",
            Checked = settings.ScreenCaptureEnabled,
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 0),
        };
        settingsGroup.Controls.Add(_captureEnabled);

        _displayCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        // Placeholder until /api/displays is queried when the window is shown.
        _displayCombo.Items.Add(new DisplayChoice(0, "Auto — match audience"));
        _displayCombo.SelectedIndex = 0;
        settingsGroup.Controls.Add(FieldRow("Capture display:", _displayCombo));

        // ── SlidePro ──────────────────────────────────────────
        var slideProGroup = Section("SlidePro.io Relay", 12);

        _slideProEnabled = new CheckBox
        {
            Text = "Relay to SlidePro.io on slide change",
            Checked = settings.SlideProEnabled,
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0),
        };
        slideProGroup.Controls.Add(_slideProEnabled);

        _apiKeyInput = new TextBox
        {
            Text = settings.SlideProApiKey,
            Width = 220,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };
        slideProGroup.Controls.Add(FieldRow("API Key:", _apiKeyInput));

        var presentationRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent,
        };
        presentationRow.Controls.Add(new Label
        {
            Text = "Presentation:",
            AutoSize = false,
            Width = 180,
            TextAlign = ContentAlignment.MiddleLeft,
        });
        _presentationCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        _presentationCombo.Items.Add(new PresentationChoice("", "— click Refresh —"));
        _presentationCombo.SelectedIndex = 0;
        presentationRow.Controls.Add(_presentationCombo);

        _refreshPresentationsButton = new Button
        {
            Text = "Refresh",
            Width = 65,
            Height = 23,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0),
        };
        _refreshPresentationsButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        _refreshPresentationsButton.Click += (_, _) => _ = LoadPresentationsAsync();
        presentationRow.Controls.Add(_refreshPresentationsButton);
        slideProGroup.Controls.Add(presentationRow);

        // ── Save ──────────────────────────────────────────────
        _saveButton = new Button
        {
            Text = "Save & Restart",
            Dock = DockStyle.Fill,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 12, 0, 0),
        };
        _saveButton.FlatAppearance.BorderSize = 0;
        _saveButton.Click += OnSave;

        _saveMsg = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 200, 100),
            Margin = new Padding(0, 4, 0, 0),
        };

        // ── Root layout ───────────────────────────────────────
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(30, 30, 30),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(statusGroup);
        root.Controls.Add(urlGroup);
        root.Controls.Add(settingsGroup);
        root.Controls.Add(slideProGroup);
        root.Controls.Add(_saveButton);
        root.Controls.Add(_saveMsg);

        Controls.Add(root);
    }

    public void UpdateStatus(bool connected, IEnumerable<string> serverUrls, int activeProPresenterPort)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatus(connected, serverUrls, activeProPresenterPort)); return; }

        _statusDot.ForeColor = connected ? Color.LimeGreen : Color.OrangeRed;
        _statusText.Text = connected ? "Connected" : "Not detected";

        // Show the live auto-detected ProPresenter port unless the user is editing the field.
        if (!_portInput.Focused && (int)_portInput.Value != activeProPresenterPort)
            _portInput.Value = activeProPresenterPort;

        var relayPort = (int)_relayPortInput.Value;
        _localUrl.Text = $"http://localhost:{relayPort}";
        _localUrl.Links.Clear();
        _localUrl.Links.Add(0, _localUrl.Text.Length, _localUrl.Text);

        var lan = NetworkUrlPrinter.GetLanIp();
        if (lan is not null)
        {
            _networkUrl.Text = $"http://{lan}:{relayPort}";
            _networkUrl.Links.Clear();
            _networkUrl.Links.Add(0, _networkUrl.Text.Length, _networkUrl.Text);
        }

        RefreshQrIfNeeded(relayPort);
    }

    /// <summary>
    /// Reloads the QR image only when the URL has changed or the previous
    /// load failed, preventing the constant clear-reload flash on every tick.
    /// </summary>
    private void RefreshQrIfNeeded(int relayPort)
    {
        var lan = NetworkUrlPrinter.GetLanIp();
        var url = lan is not null
            ? $"http://{lan}:{relayPort}"
            : $"http://localhost:{relayPort}";

        // Same URL and image already loaded — nothing to do.
        if (url == _lastQrUrl && _qrBox.Image is not null) return;

        _lastQrUrl = url;
        _qrBox.Image?.Dispose();
        _qrBox.Image = null;
        _ = LoadQrImageAsync(relayPort);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _settings.ProPresenterPort = (int)_portInput.Value;
        _settings.RelayPort = (int)_relayPortInput.Value;
        _settings.ScreenCaptureEnabled = _captureEnabled.Checked;
        _settings.CaptureDisplayIndex = (_displayCombo.SelectedItem as DisplayChoice)?.Index ?? 0;
        _settings.SlideProEnabled = _slideProEnabled.Checked;
        _settings.SlideProApiKey = _apiKeyInput.Text.Trim();
        _settings.SlideProPresentationId = (_presentationCombo.SelectedItem as PresentationChoice)?.Id ?? "";

        _saveButton.Enabled = false;
        _saveMsg.Text = "Restarting…";

        _onSave(_settings);

        _saveMsg.Text = "Saved!";
        _saveButton.Enabled = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    // Refresh the capture-display list each time the window opens (display
    // topology may have changed since last time).
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            _ = LoadDisplaysAsync((int)_relayPortInput.Value);
            if (!string.IsNullOrEmpty(_settings.SlideProApiKey))
                _ = LoadPresentationsAsync();
        }
    }

    private async Task LoadDisplaysAsync(int relayPort)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = await client.GetStringAsync($"http://localhost:{relayPort}/api/displays");
            if (IsDisposed) return;

            var choices = ParseDisplayChoices(json);

            void Apply()
            {
                _displayCombo.Items.Clear();
                foreach (var c in choices)
                    _displayCombo.Items.Add(c);

                var match = choices.FirstOrDefault(c => c.Index == _settings.CaptureDisplayIndex) ?? choices[0];
                _displayCombo.SelectedItem = match;
            }

            if (InvokeRequired) Invoke(Apply); else Apply();
        }
        catch { /* server not ready yet — keep the placeholder Auto item */ }
    }

    private async Task LoadPresentationsAsync()
    {
        var apiKey = _apiKeyInput.Text.Trim();
        if (string.IsNullOrEmpty(apiKey)) return;

        var relayPort = (int)_relayPortInput.Value;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"http://localhost:{relayPort}/api/slidepro/presentations?apiKey={Uri.EscapeDataString(apiKey)}";
            var json = await client.GetStringAsync(url);
            if (IsDisposed) return;

            var choices = ParsePresentationChoices(json);

            void Apply()
            {
                _presentationCombo.Items.Clear();
                foreach (var c in choices)
                    _presentationCombo.Items.Add(c);

                if (choices.Count > 0)
                {
                    var match = choices.FirstOrDefault(c => c.Id == _settings.SlideProPresentationId) ?? choices[0];
                    _presentationCombo.SelectedItem = match;
                }
            }

            if (InvokeRequired) Invoke(Apply); else Apply();
        }
        catch { /* server not ready or bad key — leave placeholder */ }
    }

    private static List<PresentationChoice> ParsePresentationChoices(string json)
    {
        var choices = new List<PresentationChoice>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in doc.RootElement.EnumerateArray())
            {
                var id = p.GetProperty("presentationId").GetString() ?? "";
                var title = p.TryGetProperty("title", out var t) ? t.GetString() ?? id : id;
                choices.Add(new PresentationChoice(id, title));
            }
        }
        return choices;
    }

    private static List<DisplayChoice> ParseDisplayChoices(string json)
    {
        var choices = new List<DisplayChoice> { new(0, "Auto — match audience") };

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("displays", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in arr.EnumerateArray())
            {
                var idx = d.GetProperty("index").GetInt32();
                var w = d.GetProperty("width").GetInt32();
                var h = d.GetProperty("height").GetInt32();
                var primary = d.GetProperty("isPrimary").GetBoolean();
                var audience = d.TryGetProperty("matchesAudience", out var m) && m.GetBoolean();
                var tag = primary ? " (primary)" : audience ? " (audience)" : "";
                choices.Add(new DisplayChoice(idx, $"Display {idx} — {w}×{h}{tag}"));
            }
        }

        return choices;
    }

    private sealed record DisplayChoice(int Index, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record PresentationChoice(string Id, string Title)
    {
        public override string ToString() => Title;
    }

    private async Task LoadQrImageAsync(int relayPort)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var bytes = await client.GetByteArrayAsync($"http://localhost:{relayPort}/api/qr");
            if (IsDisposed) return;
            using var ms = new System.IO.MemoryStream(bytes);
            var img = Image.FromStream(ms);
            if (InvokeRequired)
                Invoke(() => { _qrBox.Image?.Dispose(); _qrBox.Image = img; });
            else
            { _qrBox.Image?.Dispose(); _qrBox.Image = img; }
        }
        catch { /* server not ready yet; image stays blank */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FlowLayoutPanel Section(string title, int topMargin)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Top,
            Margin = new Padding(0, topMargin, 0, 0),
            BackColor = Color.Transparent,
        };

        panel.Controls.Add(new Label
        {
            Text = title.ToUpperInvariant(),
            AutoSize = true,
            ForeColor = Color.FromArgb(140, 140, 140),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
            Margin = new Padding(0, 0, 0, 3),
        });

        return panel;
    }

    private static FlowLayoutPanel FieldRow(string label, Control input)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.Transparent,
        };

        row.Controls.Add(new Label
        {
            Text = label,
            AutoSize = false,
            Width = 180,
            TextAlign = ContentAlignment.MiddleLeft,
        });
        row.Controls.Add(input);

        return row;
    }

    private LinkLabel MakeLink(string text)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            BackColor = Color.Transparent,
            LinkColor = Color.FromArgb(100, 180, 255),
            ActiveLinkColor = Color.White,
            Margin = new Padding(0, 2, 0, 0),
        };
        link.Links.Add(0, text.Length, text);
        link.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        };
        return link;
    }
}
