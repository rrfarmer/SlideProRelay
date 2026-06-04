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

            /* Idle: connected but nothing live (e.g. "Clear All"). Kept dim and
               understated so the phone stays mostly black for the audience. */
            #current.idle {
              font-size: clamp(0.9rem, 2.5vw, 1.25rem);
              font-weight: 400;
              color: #555;
              letter-spacing: 0.04em;
            }

            /* EXPERIMENTAL: current-slide image (e.g. an image slide with no text). */
            #slide {
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
          </style>
        </head>
        <body>
          <div id="status">Connecting…</div>
          <div id="current"></div>
          <img id="slide" alt="">
          <div id="next"></div>

          <script>
            const statusEl  = document.getElementById('status');
            const currentEl = document.getElementById('current');
            const slideEl   = document.getElementById('slide');
            const nextEl    = document.getElementById('next');

            function hideImage() {
              slideEl.style.display = 'none';
              slideEl.dataset.key = '';
            }

            function showIdle() {
              hideImage();
              currentEl.style.display = 'flex';
              currentEl.className     = 'idle';
              currentEl.textContent   = 'Nothing on screen';
            }

            // EXPERIMENTAL: try to show the current slide image; fall back to idle.
            // Re-fetch only when the slide identity (key) changes — not every poll.
            function showSlideImage(key) {
              if (slideEl.dataset.key === key) return;
              slideEl.dataset.key = key;
              slideEl.onload  = () => { currentEl.style.display = 'none'; slideEl.style.display = 'block'; };
              slideEl.onerror = () => showIdle();
              slideEl.src = '/api/slide-image?cb=' + encodeURIComponent(key);
            }

            function connect() {
              const es = new EventSource('/events');

              es.onmessage = (e) => {
                const d = JSON.parse(e.data);
                const connected = d.connection === 'connected';
                const text = d.current?.text;

                if (!connected) {
                  // ProPresenter unreachable (relay is up, PP is not).
                  hideImage();
                  statusEl.textContent = '● ProPresenter offline';
                  statusEl.className   = 'unavailable';
                  currentEl.style.display = 'flex';
                  currentEl.textContent = '';
                  currentEl.className   = '';
                  nextEl.textContent    = '';
                  return;
                }

                statusEl.className = 'connected';
                if (text) {
                  // Something with text is live.
                  hideImage();
                  statusEl.textContent  = '● Live';
                  currentEl.style.display = 'flex';
                  currentEl.textContent = text;
                  currentEl.className   = '';
                  nextEl.textContent    = d.next?.text ? 'Next: ' + d.next.text : '';
                } else if (d.current) {
                  // A slide is live but has no text — likely an image slide.
                  // EXPERIMENTAL: show its thumbnail (falls back to idle on failure).
                  statusEl.textContent = '● Live — image';
                  nextEl.textContent   = '';
                  showSlideImage(d.current.uuid || d.updatedAt);
                } else {
                  // Nothing live at all (e.g. "Clear All") — idle, not offline.
                  statusEl.textContent = '● Live';
                  nextEl.textContent   = '';
                  showIdle();
                }
              };

              es.onerror = () => {
                hideImage();
                currentEl.style.display = 'flex';
                statusEl.textContent = 'Reconnecting…';
                statusEl.className   = 'unavailable';
                es.close();
                setTimeout(connect, 3000);
              };
            }

            connect();
          </script>
        </body>
        </html>
        """;
}
