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
          <div id="next"></div>

          <script>
            const statusEl  = document.getElementById('status');
            const currentEl = document.getElementById('current');
            const nextEl    = document.getElementById('next');

            function connect() {
              const es = new EventSource('/events');

              es.onmessage = (e) => {
                const d = JSON.parse(e.data);
                const connected = d.connection === 'connected';
                const text = d.current?.text;

                if (!connected) {
                  // ProPresenter unreachable (relay is up, PP is not).
                  statusEl.textContent = '● ProPresenter offline';
                  statusEl.className   = 'unavailable';
                  currentEl.textContent = '';
                  currentEl.className   = '';
                  nextEl.textContent    = '';
                  return;
                }

                statusEl.className = 'connected';
                if (text) {
                  // Something is live.
                  statusEl.textContent  = '● Live';
                  currentEl.textContent = text;
                  currentEl.className   = '';
                  nextEl.textContent    = d.next?.text ? 'Next: ' + d.next.text : '';
                } else {
                  // Connected but nothing on screen (e.g. "Clear All") — idle, not offline.
                  statusEl.textContent  = '● Live';
                  currentEl.textContent = 'Nothing on screen';
                  currentEl.className   = 'idle';
                  nextEl.textContent    = '';
                }
              };

              es.onerror = () => {
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
