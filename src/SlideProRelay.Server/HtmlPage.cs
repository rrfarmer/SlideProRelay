namespace SlideProRelay.Server;

internal static class HtmlPage
{
    internal const string Content = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>SlideProRelay</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

            body {
              background: #000;
              color: #fff;
              font-family: system-ui, sans-serif;
              display: flex;
              flex-direction: column;
              min-height: 100dvh;
              padding: 1.5rem;
            }

            #status {
              font-size: 0.75rem;
              color: #888;
              text-align: right;
              margin-bottom: 1rem;
              min-height: 1rem;
              letter-spacing: 0.03em;
            }
            #status.unavailable { color: #c44; }
            #status.connected   { color: #4c4; }

            #current {
              flex: 1;
              display: flex;
              align-items: center;
              justify-content: center;
              text-align: center;
              font-size: clamp(1.5rem, 5vw, 3rem);
              font-weight: 600;
              line-height: 1.3;
              white-space: pre-wrap;
              word-break: break-word;
              padding: 1rem;
            }

            /* Idle / informational: connected but nothing live, or a status note
               (e.g. waiting for a screen capture). Dim and understated. */
            #current.idle {
              font-size: clamp(0.9rem, 2.5vw, 1.25rem);
              font-weight: 400;
              color: #555;
              letter-spacing: 0.04em;
            }

            /* EXPERIMENTAL: current-slide thumbnail (text mode, image-only slide). */
            #slide,
            /* Live screen-capture image (screen mode). */
            #screen {
              flex: 1;
              min-height: 0;
              width: 100%;
              object-fit: contain;
              display: none;
            }

            #next {
              text-align: center;
              font-size: clamp(0.85rem, 2.5vw, 1.25rem);
              color: #555;
              padding: 1rem;
              min-height: 3rem;
              white-space: pre-wrap;
              word-break: break-word;
            }

            /* Segmented Text / Screen toggle (bottom-left). */
            #view-toggle {
              position: fixed;
              bottom: 1rem;
              left: 1rem;
              display: flex;
              background: #222;
              border: 1px solid #444;
              border-radius: 8px;
              overflow: hidden;
              z-index: 10;
              user-select: none;
            }
            #view-toggle button {
              background: transparent;
              border: 0;
              color: #888;
              font-size: 0.75rem;
              padding: 0.4rem 0.8rem;
              cursor: pointer;
            }
            #view-toggle button.active { background: #0078d4; color: #fff; }

            #qr-toggle {
              position: fixed;
              bottom: 1rem;
              right: 1rem;
              background: #222;
              border: 1px solid #444;
              border-radius: 8px;
              color: #888;
              font-size: 0.75rem;
              padding: 0.4rem 0.7rem;
              cursor: pointer;
              z-index: 10;
              user-select: none;
            }

            #qr-overlay {
              display: none;
              position: fixed;
              inset: 0;
              background: rgba(0,0,0,0.85);
              z-index: 20;
              align-items: center;
              justify-content: center;
              flex-direction: column;
              gap: 1rem;
            }
            #qr-overlay.open { display: flex; }

            #qr-overlay img {
              width: min(80vw, 80vh);
              height: min(80vw, 80vh);
              image-rendering: pixelated;
              border-radius: 8px;
            }

            #qr-overlay p {
              color: #aaa;
              font-size: 0.8rem;
            }
          </style>
        </head>
        <body>
          <div id="status">Connecting…</div>
          <div id="current"></div>
          <img id="slide" alt="">
          <img id="screen" alt="">
          <div id="next"></div>

          <div id="view-toggle">
            <button data-mode="text">Text</button>
            <button data-mode="screen">Screen</button>
          </div>

          <button id="qr-toggle" title="Show QR code">QR</button>

          <div id="qr-overlay">
            <img id="qr-img" src="/api/qr" alt="QR code">
            <p>Scan to open on another device</p>
          </div>

          <script>
            const statusEl  = document.getElementById('status');
            const currentEl = document.getElementById('current');
            const slideEl   = document.getElementById('slide');
            const screenEl  = document.getElementById('screen');
            const nextEl    = document.getElementById('next');

            // 'text' (relayed slide text) or 'screen' (live screen capture).
            let viewMode    = localStorage.getItem('viewMode') === 'screen' ? 'screen' : 'text';
            let lastData    = null;
            let screenToken = 0; // bumped to cancel an in-flight screen refresh loop

            const sleep = ms => new Promise(r => setTimeout(r, ms));

            // ── visibility helpers ──────────────────────────────────────────────
            function hideThumb()    { slideEl.style.display = 'none'; slideEl.dataset.key = ''; }
            function hideScreenEl() { screenEl.style.display = 'none'; screenEl.dataset.key = ''; }
            function cancelScreen() { screenToken++; hideScreenEl(); } // also stops any loop
            function showTextArea() { currentEl.style.display = 'flex'; }
            function hideTextArea() { currentEl.style.display = 'none'; }

            function idle(msg) {
              hideThumb(); hideScreenEl();
              showTextArea();
              currentEl.className   = 'idle';
              currentEl.textContent = msg || 'Nothing on screen';
              nextEl.textContent    = '';
            }

            // ── TEXT mode ────────────────────────────────────────────────────────
            function showThumb(key) {
              if (slideEl.dataset.key === key) return;
              slideEl.dataset.key = key;
              slideEl.onload  = () => { hideTextArea(); slideEl.style.display = 'block'; };
              slideEl.onerror = () => idle('Nothing on screen');
              slideEl.src = '/api/slide-image?cb=' + encodeURIComponent(key);
            }

            function renderText(d) {
              cancelScreen();
              const text = d.current && d.current.text;
              if (text) {
                hideThumb();
                statusEl.textContent  = '● Live';
                showTextArea();
                currentEl.textContent = text;
                currentEl.className   = '';
                nextEl.textContent    = d.next && d.next.text ? 'Next: ' + d.next.text : '';
              } else if (d.current) {
                statusEl.textContent = '● Live — image';
                nextEl.textContent   = '';
                showThumb(d.current.uuid || d.updatedAt);
              } else {
                statusEl.textContent = '● Live';
                idle('Nothing on screen');
              }
            }

            // ── SCREEN mode (live capture) ───────────────────────────────────────
            function screenMessage(msg) {
              hideScreenEl();
              showTextArea();
              currentEl.className   = 'idle';
              currentEl.textContent = msg;
              nextEl.textContent    = '';
            }

            function setScreenSrc(key) {
              if (screenEl.dataset.key === key && screenEl.style.display === 'block') return;
              screenEl.dataset.key = key;
              screenEl.onload  = () => { hideThumb(); hideTextArea(); screenEl.style.display = 'block'; };
              screenEl.onerror = () => screenMessage('Screen capture unavailable');
              screenEl.src = '/api/screen-capture?cb=' + encodeURIComponent(key);
            }

            // Wait until the cached capture matches the live slide, then show it.
            // The capture is produced shortly after the slide-change event, so we
            // poll the cheap key endpoint instead of risking a stale frame.
            async function showScreenFor(targetKey) {
              const token = ++screenToken;
              const deadline = Date.now() + 5000;
              while (Date.now() < deadline && token === screenToken) {
                try {
                  const r = await fetch('/api/screen-capture/key?cb=' + Date.now());
                  if (r.ok) {
                    const j = await r.json();
                    if (token !== screenToken) return;
                    if (!targetKey || j.key === targetKey) {
                      setScreenSrc(j.key || ('t' + Date.now()));
                      return;
                    }
                  }
                } catch (_) { /* retry */ }
                // Only show a waiting hint if nothing is on screen yet; otherwise
                // keep the previous frame visible until the new one is ready.
                if (screenEl.style.display === 'none') screenMessage('Waiting for screen…');
                await sleep(250);
              }
              if (token === screenToken && screenEl.style.display === 'none')
                screenMessage('Screen capture unavailable');
            }

            function renderScreen(d) {
              hideThumb();
              statusEl.textContent = '● Live — screen';
              nextEl.textContent   = '';
              if (d.current) {
                showScreenFor(d.current.uuid || '');
              } else {
                cancelScreen();
                idle('Nothing on screen');
              }
            }

            // ── dispatcher ───────────────────────────────────────────────────────
            function render(d) {
              lastData = d;
              if (d.connection !== 'connected') {
                cancelScreen(); hideThumb();
                statusEl.textContent = '● ProPresenter offline';
                statusEl.className   = 'unavailable';
                showTextArea();
                currentEl.textContent = '';
                currentEl.className   = '';
                nextEl.textContent    = '';
                return;
              }
              statusEl.className = 'connected';
              if (viewMode === 'screen') renderScreen(d);
              else renderText(d);
            }

            // ── view toggle ──────────────────────────────────────────────────────
            const toggle = document.getElementById('view-toggle');
            function applyToggleUI() {
              toggle.querySelectorAll('button').forEach(b =>
                b.classList.toggle('active', b.dataset.mode === viewMode));
            }
            toggle.addEventListener('click', (e) => {
              const b = e.target.closest('button');
              if (!b) return;
              viewMode = b.dataset.mode === 'screen' ? 'screen' : 'text';
              localStorage.setItem('viewMode', viewMode);
              applyToggleUI();
              cancelScreen();                 // stop any pending screen loop
              if (lastData) render(lastData); // re-render immediately
            });
            applyToggleUI();

            // ── SSE ──────────────────────────────────────────────────────────────
            function connect() {
              const es = new EventSource('/events');
              es.onmessage = (e) => render(JSON.parse(e.data));
              es.onerror = () => {
                cancelScreen(); hideThumb();
                showTextArea();
                statusEl.textContent = 'Reconnecting…';
                statusEl.className   = 'unavailable';
                es.close();
                setTimeout(connect, 3000);
              };
            }
            connect();

            const qrToggle  = document.getElementById('qr-toggle');
            const qrOverlay = document.getElementById('qr-overlay');
            qrToggle.addEventListener('click', () => qrOverlay.classList.add('open'));
            qrOverlay.addEventListener('click', () => qrOverlay.classList.remove('open'));
          </script>
        </body>
        </html>
        """;
}
