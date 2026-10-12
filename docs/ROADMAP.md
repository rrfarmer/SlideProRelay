# SlideProRelay Roadmap

Status as of October 2026.

## Project Purpose

Build a cross-platform local relay for ProPresenter 7 that runs on the same machine as ProPresenter, reads the currently presented slide text, and serves a browser page for phones or local devices. It should stay small, reliable, and easy to run during a live service or event.

## Working Assumptions

- Runtime: .NET 10.
- Server: ASP.NET Core, hosted inside a Windows tray app or a macOS menu bar app (or run on its own from source).
- Operating systems: Windows 10+ and macOS 13+.
- ProPresenter API: official HTTP API, default host `localhost`, default port `50001`, with the real port auto-detected from ProPresenter's preferences.
- Slide data source: `GET /v1/status/slide`, polled — not ProPresenter's websocket.
- Default relay port: `5174`.
- Client experience: a live text feed, with an optional screen view showing a capture of the output display. Not video.
- Browser updates are pushed with Server-Sent Events.

## Phase 0: Discovery And Skeleton

Status: complete.

Delivered:

- Git repository.
- .NET solution.
- ASP.NET Core server project.
- Roadmap document.

Questions raised here and how they were settled:

- Desktop tray/config UI, or config-file/browser based? Both: a tray / menu bar app hosts the relay, and settings live on a browser page at `/settings`.
- Default relay port? `5174`.
- Do phone clients need authentication on the local network? The phone view is open to the LAN with no login. The settings page and operator endpoints are served to loopback only. A PIN or token for the phone view is still open (Phase 5).
- Is combined slide text enough, or do we need per-text-box structure? Still combined text per slide, plus notes. Per-text-box structure has not been needed yet.

## Phase 1: ProPresenter API Client

Status: complete.

Delivered:

- Typed ProPresenter client around `HttpClient` (`IProPresenterClient`, `ProPresenterClient`).
- `/version` for connection diagnostics and `/v1/status/slide` for current and next slide text.
- Configuration for host, port, and polling interval (`ProPresenterOptions`).
- Disconnected, unavailable, and invalid-response states reported as a connection state instead of crashing.
- Port auto-detection on Windows and macOS (`IProPresenterPortDetector`), re-checked after a failed poll.
- Audience-screen size from `/v1/status/screens` and slide thumbnails from the presentation thumbnail endpoint.

## Phase 2: Fast Lyrics Relay

Status: complete.

Delivered:

- Background polling service with an in-memory cache of the latest slide status.
- The cache publishes every poll result. Consumers that act on a slide change (screen capture, SlidePro.io) compare the slide UUID, falling back to its text.
- A per-session count of how often each slide has been shown, exposed as `seenCount` / `isRepeat`.
- Client endpoints: `GET /api/health`, `GET /api/current`.

The planned `GET /api/config` was never built; `GET` / `POST /api/settings` covers it.

## Phase 3: Live Browser Feed

Status: complete.

Delivered:

- Phone-friendly page at `/` showing the current slide text, with the next slide's text below it.
- Updates pushed over Server-Sent Events at `GET /events`, with a heartbeat every 15 seconds.
- Status line for live, ProPresenter offline, and reconnecting; the page reconnects on its own.
- Slides with no text fall back to ProPresenter's slide thumbnail (`GET /api/slide-image`, experimental).
- Text / Screen toggle, remembered per device.
- QR code overlay for passing the link along.

## Phase 4: Configuration And Packaging

Status: complete.

Delivered:

- Windows tray app with an Inno Setup installer: firewall rule, optional desktop icon, optional start with Windows.
- macOS menu bar app with a `.pkg` installer, optional signing and notarization, and a Start at Login toggle (launch agent).
- Both installers are self-contained, so no separate .NET install is needed.
- Browser settings page at `/settings` with Overview, Connection, Capture, and API Integration tabs. Settings persist to a per-user `settings.json` and apply without a restart, except a relay-port change.
- LAN URL discovery and a QR code for the phone URL.
- Environment variable and `appsettings.json` overrides when running from source.
- Update check in the Windows tray app, with download and install from the tray menu.

Not done:

- Windows runs as a tray app started from an optional Startup shortcut, not as a Windows service.
- No update check on macOS.
- No CI. `.github/workflows` is empty and tests run locally.

## Phase 5: Reliability For Live Use

Status: partially complete.

Delivered:

- Exponential backoff when ProPresenter is unreachable, capped at 30 seconds.
- Log throttling during an outage: one warning, then occasional debug lines.
- Browser reconnection and a visible connection state on the phone page.
- The settings page, settings API, and operator diagnostics are served to loopback only; other devices get a `404`.
- CORS limited to loopback-origin health polls.
- Automated tests for the ProPresenter client, polling, display selection, capture coordination, and endpoint access.

Still open:

- Keep the last-known slide on the phone page during an outage. Today the page clears and shows "ProPresenter offline".
- A health check that reports ProPresenter's state. `/api/health` only says the relay is up; `/api/current` carries the connection state.
- Poll duration metrics.
- Optional local access token or PIN for the phone view.

## Phase 6: Screen Capture

Status: complete. This was planned as "slow capture mode research" and shipped as a feature.

Delivered:

- A JPEG of the real output display on each live-slide change, cached and served from `GET /api/screen-capture`, with `GET /api/screen-capture/key` naming the slide it belongs to.
- Windows: DXGI Desktop Duplication. macOS: the built-in `screencapture` tool, which needs Screen Recording permission.
- Captures are encoded at 85% JPEG quality and capped at 1920 px wide.
- Display selection: a manual display always wins; otherwise the non-primary display matching ProPresenter's audience-screen size, then the first non-primary display.
- Auto never captures the primary display. With one display attached, nothing is captured until the operator picks a display, so the operator's desktop is never published by accident.
- `GET /api/displays` lists displays and explains the current choice; the settings page shows it under the display picker.
- Screen view on the phone page, which waits for the capture of the current slide before swapping the image.

Decisions:

- Capture lives in the relay process, not a separate worker.
- Capture is triggered by slide changes, not a timer.
- It is additive: the ProPresenter thumbnail flow (`/api/slide-image`) stays.

Not done:

- Capturing a single window. Only whole displays are captured.

## Phase 7: SlidePro.io Integration

Status: complete for the first version.

Delivered:

- Optional integration with SlidePro.io, off by default and configured under Settings → API Integration with an API key.
- Text mode: posts the current and next slide text on each slide change.
- Full Slide mode: uploads the screen capture for each new slide to a chosen presentation. A slide already uploaded this session is re-presented instead of uploaded again.
- Presentation picker fed by the SlidePro API.
- Failures are logged and dropped, so a SlidePro outage does not affect the local phone display.
- The Windows update check uses SlidePro.io's release endpoint.

See `docs/slidepro-public-api-integration-guide.md` for the API.

Still open:

- Upload queue and retry. A failed upload is not retried, and only the latest slide change is kept while one is in flight.
- Offline behavior beyond dropping updates.
- Privacy and data-retention notes for what is sent to SlidePro.io.

## Planned Features

From the README's "Coming Soon" list; none are started:

- Text customization: font size, color, and style on the phone page.
- Chord charts alongside lyrics.
- Translations: slide text in multiple languages.

## Handoff Notes For Future Sessions

Start by checking:

```powershell
git status
dotnet build
dotnet test tests/SlideProRelay.Tests/SlideProRelay.Tests.csproj
```

`dotnet build` at the repo root works on Windows, where the Mac project builds as a stub. On macOS, build individual projects, because the Windows tray project won't build there.

Then review:

- `README.md`
- `docs/ROADMAP.md`
- `src/SlideProRelay.Server/ServerHost.cs` for service wiring and every endpoint
- `src/SlideProRelay.Tray` and `src/SlideProRelay.Mac` for the host apps

Remaining work, in no set order:

- The open Phase 5 items: last-known slide during an outage, a fuller health check, poll metrics, and an optional PIN for the phone view.
- Retry and queueing for SlidePro.io uploads.
- An update check for macOS.
- CI for build and tests.
- The planned features above.
