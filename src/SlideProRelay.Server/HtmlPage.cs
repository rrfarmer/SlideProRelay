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
                statusEl.textContent = connected
                  ? (d.current?.text ? '● Live' : '● Live — no slide text')
                  : '● ProPresenter offline';
                statusEl.className   = d.connection;
                currentEl.textContent = d.current?.text ?? '';
                nextEl.textContent    = d.next?.text ? 'Next: ' + d.next.text : '';
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
