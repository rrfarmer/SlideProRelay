using ProSlideRelay.Server.Startup;

namespace ProSlideRelay.Tray;

internal sealed class StatusWindow : Form
{
    private readonly Label _statusDot;
    private readonly Label _statusText;
    private readonly LinkLabel _localUrl;
    private readonly LinkLabel _networkUrl;
    private readonly NumericUpDown _portInput;
    private readonly NumericUpDown _relayPortInput;
    private readonly Button _saveButton;
    private readonly Label _saveMsg;
    private readonly Action<TraySettings> _onSave;
    private TraySettings _settings;

    public StatusWindow(TraySettings settings, Action<TraySettings> onSave)
    {
        _settings = settings;
        _onSave = onSave;

        Text = "ProSlideRelay";
        Size = new Size(360, 360);
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
            RowCount = 5,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(30, 30, 30),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(statusGroup);
        root.Controls.Add(urlGroup);
        root.Controls.Add(settingsGroup);
        root.Controls.Add(_saveButton);
        root.Controls.Add(_saveMsg);

        Controls.Add(root);
    }

    public void UpdateStatus(bool connected, IEnumerable<string> serverUrls)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatus(connected, serverUrls)); return; }

        _statusDot.ForeColor = connected ? Color.LimeGreen : Color.OrangeRed;
        _statusText.Text = connected ? "Connected" : "Not detected";

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
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _settings.ProPresenterPort = (int)_portInput.Value;
        _settings.RelayPort = (int)_relayPortInput.Value;

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
