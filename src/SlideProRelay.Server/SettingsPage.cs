namespace SlideProRelay.Server;

internal static class SettingsPage
{
    internal const string Content = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>SlideProRelay — Settings</title>
          <style>
            :root {
              --bg:       #0f172a;
              --surface:  #1e293b;
              --alt:      #263248;
              --border:   #334155;
              --accent:   #818cf8;
              --accent2:  #4f46e5;
              --text:     #f1f5f9;
              --muted:    #94a3b8;
              --ok:       #34d399;
              --err:      #f87171;
            }
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

            body {
              background: var(--bg);
              color: var(--text);
              font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
              font-size: 14px;
              line-height: 1.5;
              min-height: 100vh;
            }

            /* ── Header ─────────────────────────────────────────────── */
            .hdr {
              background: var(--surface);
              border-bottom: 1px solid var(--border);
              padding: 0 24px;
              height: 54px;
              display: flex;
              align-items: center;
              gap: 12px;
              position: sticky;
              top: 0;
              z-index: 200;
            }
            .hdr-logo {
              font-size: 17px;
              font-weight: 900;
              color: var(--accent);
              letter-spacing: -0.5px;
            }
            .hdr-name {
              font-size: 13px;
              font-weight: 500;
              color: var(--muted);
            }
            .hdr-right {
              margin-left: auto;
              display: flex;
              align-items: center;
              gap: 14px;
            }
            .hdr-status {
              display: flex;
              align-items: center;
              gap: 7px;
              font-size: 12px;
              font-weight: 600;
              color: var(--muted);
            }
            .hdr-save {
              background: var(--accent2);
              color: #fff;
              border: none;
              border-radius: 9px;
              padding: 9px 22px;
              font-size: 14px;
              font-weight: 700;
              cursor: pointer;
              letter-spacing: .01em;
              display: inline-flex;
              align-items: center;
              gap: 8px;
              transition: background .15s, box-shadow .15s;
              box-shadow: 0 0 0 0 rgba(129,140,248,0);
            }
            .hdr-save:hover:not(:disabled) {
              background: var(--accent);
              box-shadow: 0 0 0 3px rgba(129,140,248,0.35);
            }
            .hdr-save:disabled { opacity: .5; cursor: not-allowed; }
            .hdr-save-msg {
              font-size: 12px;
              font-weight: 600;
              opacity: 0;
              transition: opacity .25s;
              white-space: nowrap;
            }
            .hdr-save-msg.show { opacity: 1; }
            .hdr-save-msg.ok  { color: var(--ok); }
            .hdr-save-msg.err { color: var(--err); }
            @keyframes spin { to { transform: rotate(360deg); } }
            .spin {
              display: inline-block;
              width: 13px; height: 13px;
              border: 2px solid rgba(255,255,255,.3);
              border-top-color: #fff;
              border-radius: 50%;
              animation: spin .6s linear infinite;
              vertical-align: middle;
              flex-shrink: 0;
            }
            .sdot {
              width: 8px; height: 8px;
              border-radius: 50%;
              background: var(--muted);
              flex-shrink: 0;
              transition: background 0.3s;
            }
            .sdot.live {
              background: var(--ok);
              animation: glow 2s ease-in-out infinite;
            }
            .sdot.dead { background: var(--err); }
            @keyframes glow {
              0%,100% { box-shadow: 0 0 0 0 rgba(52,211,153,.5); }
              50%      { box-shadow: 0 0 0 5px rgba(52,211,153,0); }
            }

            /* ── Tabs ───────────────────────────────────────────────── */
            .tabs {
              background: var(--surface);
              border-bottom: 1px solid var(--border);
              display: flex;
              padding: 0 20px;
              gap: 2px;
              position: sticky;
              top: 54px;
              z-index: 100;
            }
            .tab {
              background: none;
              border: none;
              border-bottom: 2px solid transparent;
              color: var(--muted);
              padding: 11px 14px;
              font-size: 13px;
              font-weight: 600;
              cursor: pointer;
              transition: color .15s, border-color .15s;
              white-space: nowrap;
            }
            .tab:hover { color: var(--text); }
            .tab.on { color: var(--accent); border-bottom-color: var(--accent); }

            /* ── Content ────────────────────────────────────────────── */
            main {
              max-width: 640px;
              margin: 0 auto;
              padding: 24px 20px 40px;
            }
            .pane { display: none; }
            .pane.on { display: block; }

            /* ── Cards ──────────────────────────────────────────────── */
            .card {
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 12px;
              padding: 20px;
              margin-bottom: 12px;
            }
            .card-title {
              font-size: 10px;
              font-weight: 800;
              text-transform: uppercase;
              letter-spacing: .1em;
              color: var(--muted);
              margin-bottom: 18px;
            }

            /* ── Form ───────────────────────────────────────────────── */
            .field { margin-bottom: 14px; }
            .field:last-child { margin-bottom: 0; }
            .field > label {
              display: block;
              font-size: 12px;
              font-weight: 600;
              color: var(--muted);
              text-transform: uppercase;
              letter-spacing: .06em;
              margin-bottom: 6px;
            }
            .hint { font-size: 11px; color: var(--muted); margin-top: 5px; }

            input[type="text"],
            input[type="number"],
            input[type="password"],
            select {
              background: var(--bg);
              border: 1px solid var(--border);
              color: var(--text);
              border-radius: 8px;
              padding: 9px 12px;
              font-size: 14px;
              width: 100%;
              outline: none;
              transition: border-color .15s;
              -webkit-appearance: none;
              appearance: none;
            }
            input:focus, select:focus { border-color: var(--accent); }
            select {
              background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%2394a3b8' stroke-width='2'%3E%3Cpolyline points='6 9 12 15 18 9'/%3E%3C/svg%3E");
              background-repeat: no-repeat;
              background-position: right 10px center;
              padding-right: 36px;
            }
            .irow { display: flex; gap: 8px; align-items: center; }
            .irow input { flex: 1; }
            .isuffix { font-size: 13px; color: var(--muted); flex-shrink: 0; }

            /* ── Toggle ─────────────────────────────────────────────── */
            .tog-row {
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 16px;
            }
            .tog-label { font-size: 14px; font-weight: 600; }
            .tog-desc { font-size: 12px; color: var(--muted); margin-top: 3px; }
            .tog {
              flex-shrink: 0;
              width: 46px; height: 26px;
              background: var(--border);
              border-radius: 13px;
              border: none;
              cursor: pointer;
              position: relative;
              transition: background .2s;
              outline: none;
            }
            .tog::after {
              content: '';
              position: absolute;
              width: 20px; height: 20px;
              background: #fff;
              border-radius: 50%;
              top: 3px; left: 3px;
              transition: transform .22s cubic-bezier(.4,0,.2,1);
              box-shadow: 0 1px 4px rgba(0,0,0,.4);
            }
            .tog.on { background: var(--accent2); }
            .tog.on::after { transform: translateX(20px); }

            /* ── Mode cards ─────────────────────────────────────────── */
            .mode-grid {
              display: grid;
              grid-template-columns: repeat(3, 1fr);
              gap: 10px;
            }
            .mcard {
              border: 2px solid var(--border);
              border-radius: 10px;
              padding: 18px 10px 14px;
              cursor: pointer;
              text-align: center;
              transition: border-color .15s, background .15s;
              user-select: none;
            }
            .mcard:hover { border-color: var(--muted); background: var(--alt); }
            .mcard.on { border-color: var(--accent); background: rgba(129,140,248,.09); }
            .mcard-icon { font-size: 26px; margin-bottom: 6px; }
            .mcard-label { font-size: 12px; font-weight: 700; }
            .mcard-desc { font-size: 11px; color: var(--muted); margin-top: 3px; }

            /* ── Buttons ────────────────────────────────────────────── */
            .btn {
              background: var(--accent2);
              color: #fff;
              border: none;
              border-radius: 8px;
              padding: 10px 22px;
              font-size: 14px;
              font-weight: 700;
              cursor: pointer;
              transition: background .15s, transform .1s;
              white-space: nowrap;
              display: inline-flex;
              align-items: center;
              gap: 6px;
            }
            .btn:hover:not(:disabled) { background: var(--accent); }
            .btn:active:not(:disabled) { transform: scale(.98); }
            .btn:disabled { opacity: .45; cursor: not-allowed; }
            .btn-ghost {
              background: var(--alt);
              border: 1px solid var(--border);
              color: var(--text);
              border-radius: 8px;
              padding: 8px 14px;
              font-size: 13px;
              font-weight: 600;
              cursor: pointer;
              white-space: nowrap;
              transition: background .15s;
            }
            .btn-ghost:hover:not(:disabled) { background: var(--border); }
            .btn-ghost:disabled { opacity: .4; cursor: not-allowed; }

            /* ── URL rows ───────────────────────────────────────────── */
            .url-row {
              display: flex;
              align-items: center;
              gap: 10px;
              background: var(--bg);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 10px 14px;
              margin-bottom: 8px;
              transition: border-color .15s;
            }
            .url-row:hover { border-color: var(--muted); }
            .url-badge {
              font-size: 9px;
              font-weight: 800;
              text-transform: uppercase;
              letter-spacing: .06em;
              color: var(--muted);
              background: var(--alt);
              border-radius: 4px;
              padding: 2px 6px;
              flex-shrink: 0;
            }
            .url-text {
              font-family: 'SF Mono', 'Fira Code', Consolas, monospace;
              font-size: 13px;
              color: var(--accent);
              flex: 1;
              word-break: break-all;
            }

            /* ── QR ─────────────────────────────────────────────────── */
            .qr-wrap { display: flex; justify-content: center; padding: 12px 0 20px; }
            .qr-img {
              width: 176px; height: 176px;
              background: #fff;
              border-radius: 12px;
              padding: 10px;
              image-rendering: pixelated;
              display: block;
            }

            /* ── Status hero ────────────────────────────────────────── */
            .stat-hero {
              display: flex;
              align-items: center;
              gap: 16px;
              padding: 20px;
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 12px;
              margin-bottom: 12px;
            }
            .stat-icon {
              font-size: 28px;
              width: 52px; height: 52px;
              background: var(--alt);
              border-radius: 50%;
              display: flex;
              align-items: center;
              justify-content: center;
              flex-shrink: 0;
            }
            .stat-main { font-size: 15px; font-weight: 700; }
            .stat-sub  { font-size: 12px; color: var(--muted); margin-top: 2px; }

            /* ── API key eye ────────────────────────────────────────── */
            .eye-wrap { position: relative; }
            .eye-wrap input { padding-right: 38px; }
            .eye-btn {
              position: absolute;
              right: 0; top: 0; bottom: 0;
              width: 38px;
              background: none;
              border: none;
              cursor: pointer;
              display: flex;
              align-items: center;
              justify-content: center;
              color: var(--muted);
              font-size: 15px;
              transition: color .15s;
              border-radius: 0 8px 8px 0;
            }
            .eye-btn:hover { color: var(--text); }

            /* ── Divider ────────────────────────────────────────────── */
            .div { height: 1px; background: var(--border); margin: 18px 0; }

            /* ── Field row (label + select + button) ────────────────── */
            .frow { display: flex; gap: 8px; }
            .frow select { flex: 1; min-width: 0; }


            .link { color: var(--accent); text-decoration: none; font-size: 13px; }
            .link:hover { text-decoration: underline; }

            /* ── Info card ──────────────────────────────────────────── */
            .info-card {
              background: rgba(129,140,248,.06);
              border: 1px solid rgba(129,140,248,.18);
              border-left: 3px solid var(--accent2);
              border-radius: 10px;
              padding: 16px 18px;
              margin-bottom: 14px;
              font-size: 13px;
              line-height: 1.65;
              color: var(--muted);
            }
            .info-card p { margin-bottom: 10px; }
            .info-card p:last-child { margin-bottom: 0; }
            .info-card strong { color: var(--text); font-weight: 600; }
            .info-card ul { padding-left: 18px; margin-top: 5px; }
            .info-card li { margin-bottom: 5px; }
          </style>
        </head>
        <body>

        <!-- Header -->
        <div class="hdr">
          <span class="hdr-logo">P7</span>
          <span class="hdr-name">SlideProRelay</span>
          <div class="hdr-right">
            <div class="hdr-status">
              <div class="sdot" id="sdot"></div>
              <span id="stext">Checking…</span>
            </div>
            <span class="hdr-save-msg" id="saveMsg"></span>
            <button class="hdr-save" id="saveBtn" onclick="saveSettings()">Save</button>
          </div>
        </div>

        <!-- Tabs -->
        <div class="tabs" id="tabBar">
          <button class="tab on" data-tab="overview">Overview</button>
          <button class="tab"    data-tab="connection">Connection</button>
          <button class="tab"    data-tab="capture">Capture</button>
          <button class="tab"    data-tab="slidepro">API Integration</button>
        </div>

        <main>

          <!-- ── OVERVIEW ──────────────────────────────────── -->
          <div class="pane on" id="pane-overview">
            <div class="stat-hero">
              <div class="stat-icon" id="ppIcon">○</div>
              <div>
                <div class="stat-main" id="ppMain">Checking…</div>
                <div class="stat-sub">ProPresenter connection</div>
              </div>
              <a href="/" target="_blank" class="btn-ghost" style="margin-left:auto">Open Display ↗</a>
            </div>

            <div class="card">
              <div class="card-title">Phone URL — scan or share with your audience</div>
              <div class="qr-wrap">
                <img class="qr-img" id="qrImg" src="/api/qr" alt="QR Code">
              </div>
              <div id="networkRow" class="url-row" style="display:none">
                <span class="url-badge">Network</span>
                <span class="url-text" id="networkUrl"></span>
                <button class="btn-ghost" onclick="openUrl('net')">Open</button>
              </div>
              <div class="url-row">
                <span class="url-badge">Local</span>
                <span class="url-text" id="localUrl">http://localhost:5174</span>
                <button class="btn-ghost" onclick="openUrl('local')">Open</button>
              </div>
            </div>
          </div>

          <!-- ── CONNECTION ────────────────────────────────── -->
          <div class="pane" id="pane-connection">
            <div class="card">
              <div class="card-title">ProPresenter</div>
              <div class="field">
                <label>Host</label>
                <input type="text" id="host" value="localhost" spellcheck="false" autocomplete="off">
              </div>
              <div class="field">
                <label>Port</label>
                <input type="number" id="proPresenterPort" min="1" max="65535" value="50001">
                <div class="hint" id="portHint"></div>
              </div>
              <div class="field">
                <label>Polling Interval</label>
                <div class="irow">
                  <input type="number" id="pollingIntervalMs" min="100" max="10000" value="500">
                  <span class="isuffix">ms</span>
                </div>
                <div class="hint">How often to ask ProPresenter for the current slide</div>
              </div>
            </div>
            <div class="card">
              <div class="card-title">Relay Server</div>
              <div class="field">
                <label>Relay Port</label>
                <input type="number" id="relayPort" min="1" max="65535" value="5174">
                <div class="hint">The port number in your phone URL — e.g. http://…:<strong>5174</strong></div>
              </div>
            </div>
          </div>

          <!-- ── CAPTURE ───────────────────────────────────── -->
          <div class="pane" id="pane-capture">
            <div class="card">
              <div class="tog-row">
                <div>
                  <div class="tog-label">Capture output screen</div>
                  <div class="tog-desc">Take a screenshot on each slide change and serve it at /api/screen-capture</div>
                </div>
                <button class="tog on" id="screenCapture" role="switch" aria-checked="true" onclick="onToggle(this)"></button>
              </div>
              <div id="displaySection">
                <div class="div"></div>
                <div class="field">
                  <label>Capture Display</label>
                  <select id="displaySelect">
                    <option value="0">Auto — match audience</option>
                  </select>
                </div>
              </div>
            </div>
          </div>

          <!-- ── SLIDEPRO ──────────────────────────────────── -->
          <div class="pane" id="pane-slidepro">

            <div class="info-card">
              <p><strong>What is this?</strong> When your ProPresenter machine is behind a firewall, VLAN, or complex network config, phones and remote viewers on other networks can't reach your local relay directly. SlidePro integration pushes your slide data to the cloud so anyone can follow along — no network changes needed.</p>
              <p><strong>SlidePro.io</strong> is a companion platform for presenting, note-taking, and audience engagement. Connect your relay to stream live slide content to any device, anywhere.</p>
              <p>
                <strong>Choosing a mode:</strong>
                <ul>
                  <li><strong>Text</strong> — lightning-fast text-only updates. Best for lyrics and worship where speed matters.</li>
                  <li><strong>Full Slide</strong> — sends a screenshot on each slide change. Richer visuals, great for sermons and teaching.</li>
                </ul>
              </p>
              <p>Create a free account at <a href="https://slidepro.io" class="link" target="_blank" rel="noopener">slidepro.io</a>, then copy your API key from <strong>Profile Settings</strong> inside your account.</p>
            </div>

            <div class="card">
              <div class="field">
                <label>API Key</label>
                <div class="eye-wrap">
                  <input type="password" id="apiKey" placeholder="Enter your SlidePro API key" autocomplete="off">
                  <button class="eye-btn" id="eyeBtn" onclick="toggleEye()" title="Show / hide">👁</button>
                </div>
              </div>

              <div class="div"></div>
              <div class="card-title">Integration Mode</div>

              <div class="mode-grid">
                <div class="mcard on" data-mode="off" onclick="selectMode(this)">
                  <div class="mcard-icon">⊘</div>
                  <div class="mcard-label">Off</div>
                  <div class="mcard-desc">Disabled</div>
                </div>
                <div class="mcard" data-mode="text" onclick="selectMode(this)">
                  <div class="mcard-icon">📝</div>
                  <div class="mcard-label">Text</div>
                  <div class="mcard-desc">Great for lyrics</div>
                </div>
                <div class="mcard" data-mode="screencapture" onclick="selectMode(this)">
                  <div class="mcard-icon">🖼️</div>
                  <div class="mcard-label">Full Slide</div>
                  <div class="mcard-desc">For presentations</div>
                </div>
              </div>

              <div id="spDetails" style="display:none">
                <div class="div"></div>
                <div class="field">
                  <label>Presentation</label>
                  <div class="frow">
                    <select id="presentationSelect">
                      <option value="">— click Refresh —</option>
                    </select>
                    <button class="btn-ghost" id="refreshBtn" onclick="loadPresentations()">Refresh</button>
                  </div>
                </div>
              </div>
            </div>
            <p style="margin-top:12px">
              <a href="https://slidepro.io" class="link" target="_blank" rel="noopener">
                Get your API key at SlidePro.io ↗
              </a>
            </p>
          </div>

        </main>


        <script>
        // ── State ─────────────────────────────────────────────────────────────
        let _settings = null;
        let _networkUrl = null;

        // ── Init ──────────────────────────────────────────────────────────────
        (async function init() {
          // Load settings from server
          try {
            const res = await fetch('/api/settings');
            if (res.ok) {
              _settings = await res.json();
              applySettings(_settings);
            } else {
              showMsg('Settings not available in standalone mode.', 'err');
              document.getElementById('saveBtn').disabled = true;
            }
          } catch {
            showMsg('Could not load settings.', 'err');
          }

          // Live ProPresenter status via SSE
          const es = new EventSource('/events');
          es.onmessage = e => updateStatus(JSON.parse(e.data).connection);
          es.onerror   = () => updateStatus('unavailable');

          // URLs
          loadUrls();

          // Displays
          loadDisplays();

          // Presentations (if API key already set)
          if (_settings?.slideProApiKey) loadPresentations();
        })();

        // ── Tab switching ─────────────────────────────────────────────────────
        document.getElementById('tabBar').addEventListener('click', e => {
          const btn = e.target.closest('.tab');
          if (!btn) return;
          document.querySelectorAll('.tab').forEach(t => t.classList.remove('on'));
          document.querySelectorAll('.pane').forEach(p => p.classList.remove('on'));
          btn.classList.add('on');
          document.getElementById('pane-' + btn.dataset.tab).classList.add('on');
        });

        // ── Status ────────────────────────────────────────────────────────────
        function updateStatus(conn) {
          const live = conn === 'connected';
          const dot  = document.getElementById('sdot');
          const txt  = document.getElementById('stext');
          dot.className = 'sdot ' + (live ? 'live' : conn === 'unavailable' ? 'dead' : '');
          txt.textContent = live ? 'ProPresenter Live' : 'ProPresenter offline';

          document.getElementById('ppIcon').textContent = live ? '●' : '○';
          document.getElementById('ppIcon').style.color = live ? 'var(--ok)' : 'var(--err)';
          document.getElementById('ppMain').textContent = live ? 'Connected' : 'Not detected';
        }

        // ── URLs ──────────────────────────────────────────────────────────────
        async function loadUrls() {
          try {
            const d = await fetch('/api/network-url').then(r => r.json());
            document.getElementById('localUrl').textContent = d.local;
            if (d.network) {
              _networkUrl = d.network;
              document.getElementById('networkUrl').textContent = d.network;
              document.getElementById('networkRow').style.display = 'flex';
            }
          } catch {}
        }

        function openUrl(which) {
          const url = which === 'net'
            ? (_networkUrl || document.getElementById('networkUrl').textContent)
            : document.getElementById('localUrl').textContent;
          window.open(url, '_blank');
        }

        // ── Apply loaded settings to form ─────────────────────────────────────
        function applySettings(s) {
          setVal('host', s.host);
          setVal('proPresenterPort', s.proPresenterPort);
          setVal('pollingIntervalMs', s.pollingIntervalMs);
          setVal('relayPort', s.relayPort);
          setToggle('screenCapture', s.screenCaptureEnabled);
          setMode(s.slideProMode);
          setVal('apiKey', s.slideProApiKey);
          // displaySelect and presentationSelect populated after their async loads
          window._pendingDisplayIndex = s.captureDisplayIndex;
          window._pendingPresentationId = s.slideProPresentationId;
        }

        // ── Displays ─────────────────────────────────────────────────────────
        async function loadDisplays() {
          try {
            const d = await fetch('/api/displays').then(r => r.json());
            const sel = document.getElementById('displaySelect');
            sel.innerHTML = '<option value="0">Auto — match audience</option>';
            for (const disp of d.displays || []) {
              const tag = disp.isPrimary ? ' (primary)' : disp.matchesAudience ? ' (audience)' : '';
              sel.appendChild(new Option(`Display ${disp.index} — ${disp.width}×${disp.height}${tag}`, disp.index));
            }
            if (window._pendingDisplayIndex != null) sel.value = window._pendingDisplayIndex;
          } catch {}
        }

        // ── Presentations ─────────────────────────────────────────────────────
        async function loadPresentations() {
          const key = document.getElementById('apiKey').value.trim();
          if (!key) return;
          const btn = document.getElementById('refreshBtn');
          btn.innerHTML = '<span class="spin" style="border-top-color:var(--text)"></span>';
          btn.disabled = true;
          try {
            const url = '/api/slidepro/presentations?apiKey=' + encodeURIComponent(key);
            const list = await fetch(url).then(r => r.ok ? r.json() : Promise.reject());
            const sel = document.getElementById('presentationSelect');
            sel.innerHTML = '';
            for (const p of list)
              sel.appendChild(new Option(p.title || p.presentationId, p.presentationId));
            const want = window._pendingPresentationId || (_settings && _settings.slideProPresentationId);
            if (want) sel.value = want;
          } catch {
            /* bad key or network — leave as-is */
          } finally {
            btn.textContent = 'Refresh';
            btn.disabled = false;
          }
        }

        // ── Mode cards ────────────────────────────────────────────────────────
        function selectMode(card) {
          document.querySelectorAll('.mcard').forEach(c => c.classList.remove('on'));
          card.classList.add('on');
          document.getElementById('spDetails').style.display =
            card.dataset.mode === 'off' ? 'none' : 'block';
        }

        function setMode(mode) {
          const card = document.querySelector(`.mcard[data-mode="${mode}"]`) ||
                       document.querySelector('.mcard[data-mode="off"]');
          selectMode(card);
        }

        function getMode() {
          return document.querySelector('.mcard.on')?.dataset.mode ?? 'off';
        }

        // ── Toggle ────────────────────────────────────────────────────────────
        function onToggle(btn) {
          const on = !btn.classList.contains('on');
          setToggle(btn.id, on);
        }

        function setToggle(id, on) {
          const el = document.getElementById(id);
          el.classList.toggle('on', on);
          el.setAttribute('aria-checked', on.toString());
          if (id === 'screenCapture')
            document.getElementById('displaySection').style.display = on ? 'block' : 'none';
        }

        // ── API key eye ───────────────────────────────────────────────────────
        let _eyeOn = false;
        function toggleEye() {
          _eyeOn = !_eyeOn;
          document.getElementById('apiKey').type = _eyeOn ? 'text' : 'password';
          document.getElementById('eyeBtn').textContent = _eyeOn ? '🔒' : '👁';
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        function setVal(id, v) {
          const el = document.getElementById(id);
          if (el) el.value = v ?? '';
        }

        function getNum(id, def) {
          const v = parseInt(document.getElementById(id)?.value);
          return isNaN(v) ? def : v;
        }

        let _msgTimer = null;
        function showMsg(text, cls) {
          const el = document.getElementById('saveMsg');
          el.textContent = text;
          el.className = 'hdr-save-msg show' + (cls ? ' ' + cls : '');
          clearTimeout(_msgTimer);
          if (cls === 'ok') _msgTimer = setTimeout(() => { el.className = 'hdr-save-msg'; }, 2500);
        }

        function delay(ms) { return new Promise(r => setTimeout(r, ms)); }

        // ── Save ──────────────────────────────────────────────────────────────
        async function saveSettings() {
          const btn = document.getElementById('saveBtn');
          btn.disabled = true;
          btn.innerHTML = '<span class="spin"></span> Saving…';
          showMsg('');

          const settings = {
            host:                document.getElementById('host').value.trim() || 'localhost',
            proPresenterPort:    getNum('proPresenterPort', 50001),
            pollingIntervalMs:   getNum('pollingIntervalMs', 500),
            relayPort:           getNum('relayPort', 5174),
            screenCaptureEnabled: document.getElementById('screenCapture').classList.contains('on'),
            captureDisplayIndex: getNum('displaySelect', 0),
            slideProMode:        getMode(),
            slideProApiKey:      document.getElementById('apiKey').value.trim(),
            slideProPresentationId: document.getElementById('presentationSelect').value,
          };

          try {
            const res = await fetch('/api/settings', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(settings),
            });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const { newPort, restarting } = await res.json();

            if (!restarting) {
              // Hot-reload: settings applied in-place, no restart needed.
              btn.innerHTML = 'Save';
              btn.disabled = false;
              showMsg('Saved!', 'ok');
              return;
            }

            // Port changed — need a full restart.
            btn.innerHTML = '<span class="spin"></span> Restarting…';
            const base = 'http://localhost:' + newPort;
            await delay(600);
            for (let i = 0; i < 50; i++) {
              try {
                const h = await fetch(base + '/api/health');
                if (h.ok) { window.location.href = base + '/settings'; return; }
              } catch {}
              const dots = '.'.repeat((i % 3) + 1);
              btn.innerHTML = '<span class="spin"></span> Restarting' + dots;
              await delay(400);
            }
            window.location.href = base + '/settings';
          } catch (e) {
            btn.innerHTML = 'Save';
            btn.disabled = false;
            showMsg('Error: ' + (e.message || 'unknown'), 'err');
          }
        }
        </script>
        </body>
        </html>
        """;
}
