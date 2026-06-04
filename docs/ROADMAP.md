# ProSlideRelay Roadmap

## Project Purpose

Build a cross-platform local relay for ProPresenter 7 that runs on the same machine as ProPresenter, reads the currently presented slide text, and serves a browser page for phones or local devices. The first useful version should be small, reliable, and easy to run during a live service or event.

## Working Assumptions

- Runtime: .NET 10.
- Server: ASP.NET Core.
- Initial operating systems: Windows and macOS.
- ProPresenter API: official HTTP API, default host `localhost`, default port `50001`.
- First data source: `GET /v1/status/slide`.
- First client experience: text-only live feed, not video.
- Initial relay behavior: polling, not direct ProPresenter websocket usage.
- Future expansion may include a slow screen-capture/upload mode.

## Phase 0: Discovery And Skeleton

Status: complete.

Goals:

- Create the repository and solution structure.
- Capture architecture assumptions and open questions.
- Verify .NET 10 local build.
- Keep the first server project intentionally minimal.

Deliverables:

- Git repository.
- .NET solution.
- ASP.NET Core server project.
- Roadmap document.

Open questions:

- Should the local server have a desktop tray/config UI, or remain config-file/browser based?
- What local port should the relay use by default?
- Do phone clients need authentication on the local network?
- Is combined slide text enough, or do we eventually need per-text-box structure?

## Phase 1: ProPresenter API Client

Status: complete.

Goals:

- Add a typed ProPresenter client around `HttpClient`.
- Read `/version` for connection diagnostics.
- Read `/v1/status/slide` for current and next slide text.
- Add configuration for ProPresenter host, port, and polling interval.
- Represent disconnected, unavailable, and invalid-response states cleanly.

Likely implementation:

- `ProPresenterOptions`
- `IProPresenterClient`
- `ProPresenterClient`
- `SlideStatus`, `SlideText`, and connection status models.

Acceptance criteria:

- A developer can run the server and see current cached slide JSON from a relay endpoint.
- If ProPresenter is closed or Network/API is disabled, the relay reports a useful status instead of crashing.

## Phase 2: Fast Lyrics Relay

Status: complete.

Goals:

- Add a background polling service.
- Cache the latest slide status in memory.
- Detect meaningful changes using slide UUID and text.
- Expose a stable local API for clients.

Candidate endpoints:

- `GET /api/health`
- `GET /api/current`
- `GET /api/config`

Acceptance criteria:

- Multiple browser/phone clients can request the current slide without each one hitting ProPresenter directly.
- Polling interval can be tuned without code changes.
- Relay keeps working if ProPresenter temporarily disappears and returns.

## Phase 3: Live Browser Feed

Status: complete.

Goals:

- Serve a simple phone-friendly web page.
- Show current slide text prominently.
- Optionally show next slide text in a secondary area.
- Push updates from the relay to browsers.

Preferred browser update mechanism:

- Server-Sent Events from the relay, for example `GET /events`.

Fallback:

- Browser polling against `GET /api/current`.

Acceptance criteria:

- A phone on the same network can open the relay URL and see lyrics update.
- The display remains readable at common phone sizes.
- The page shows connection state when ProPresenter or the relay feed is unavailable.

## Phase 4: Configuration And Packaging

Status: partially complete. LAN URL discovery, default port, env var overrides, and ProPresenter setup docs are done. Windows service / macOS launch agent packaging is deferred to a later iteration.

Goals:

- Make first-run setup practical for non-developer use.
- Support Windows and macOS packaging decisions.
- Document ProPresenter setup steps.

Topics:

- Config file location.
- Environment variable overrides.
- Command-line arguments.
- Default relay port.
- Windows service or startup shortcut.
- macOS launch agent or simple app bundle.
- Local firewall prompts and network binding.

Acceptance criteria:

- The relay can be started consistently on Windows and macOS.
- The user can discover the phone URL easily.
- Setup instructions include the ProPresenter Network/API settings to verify.

## Phase 5: Reliability For Live Use

Goals:

- Harden failure handling and observability.
- Avoid runaway polling or noisy logs.
- Keep a useful last-known slide state.

Topics:

- Health checks.
- Structured logs.
- Retry/backoff behavior.
- Poll duration metrics.
- Browser reconnection behavior.
- Optional local access token or PIN.

Acceptance criteria:

- Temporary API failures do not break connected browser clients.
- Operators can tell whether the issue is ProPresenter, the relay, or the phone browser.

## Phase 6: Slow Capture Mode Research

Goals:

- Investigate a future low-frequency screen capture mode.
- Keep it separate from fast lyrics mode.
- Decide whether capture belongs in the same app or an optional worker.

Possible flow:

- Capture ProPresenter output or selected display/window.
- Downscale and compress image.
- Queue upload to external service.
- Store upload status separately from lyrics feed.

Risks:

- Cross-platform screen capture APIs differ significantly.
- macOS screen recording permissions can affect setup.
- Capturing the whole screen may expose sensitive operator UI.
- Upload reliability and bandwidth need separate design from local lyrics updates.

Acceptance criteria for research:

- Prototype can capture a chosen screen/window on Windows and macOS.
- Permissions and packaging implications are documented before full implementation.

## Phase 7: External Web App Integration

Goals:

- Define integration with the existing external web app once requirements are known.
- Avoid coupling the local relay to an external API too early.

Topics:

- Auth model.
- Tenant/event identity.
- Upload queue and retry semantics.
- Offline behavior.
- Privacy and data retention.

Acceptance criteria:

- Integration can be enabled or disabled without affecting local lyrics mode.
- Failures in external upload do not degrade local phone display.

## Handoff Notes For Future Sessions

Start by checking:

```powershell
git status
dotnet build
```

Then review:

- `README.md`
- `docs/ROADMAP.md`
- `src/ProSlideRelay.Server/Program.cs`

Next likely implementation step:

Build Phase 5 by hardening reliability: add exponential backoff when ProPresenter is unreachable, avoid log spam during outages, and keep the last-known slide visible on the browser page when the relay temporarily loses the ProPresenter connection.
