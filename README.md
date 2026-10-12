# SlideProRelay

**Put your lyrics on every phone in the room — automatically.**

SlideProRelay runs on the same computer as ProPresenter 7. As you advance slides, the text appears live on any phone or tablet connected to your Wi-Fi. Nothing for your congregation to install — they just open a link in their browser.

> ### SlideProRelay is — and will always be — free and open source.
> No subscriptions. No licenses. No catch. Free forever.

---

## Features

- Live slide text delivered to any phone or tablet on your network
- Updates instantly as you advance slides — no refresh needed
- **Screen view** — viewers can switch from text to a picture of your actual output screen, refreshed on every slide change
- Slides with no text show the slide image instead
- Works over your existing Wi-Fi — no extra hardware required
- Finds ProPresenter's network port on its own and reconnects automatically if ProPresenter restarts
- QR code for sharing the link — on the settings page and on the phone page itself
- Settings page in your browser, reachable only from the computer running SlideProRelay
- Windows system tray app and macOS menu bar app — runs quietly in the background
- The Windows app offers new versions from its tray menu
- Optional **SlidePro.io** integration for viewers who can't reach your local network

## Coming Soon

- **Text customization** — adjust font size, color, and style to suit your preferences
- **Chord charts** — display chords alongside lyrics for musicians
- **Translations** — show slide text in multiple languages simultaneously

## Sponsored by SlidePro.io

SlideProRelay is proudly sponsored by **[SlidePro.io](https://slidepro.io)** — a platform built for churches that want to take congregation engagement further.

The SlidePro.io integration is built in. Turn it on and SlideProRelay pushes each slide to SlidePro.io as you advance, so viewers who can't reach your local network — a separate VLAN, a firewalled machine, or somewhere else entirely — can still follow along. Choose **Text** for fast text-only updates or **Full Slide** to send a picture of each slide. Set it up under **Settings → API Integration** with an API key from your SlidePro.io account.

---

## Getting Started

Download the installer for your computer from the [Releases page](https://github.com/rrfarmer/SlideProRelay/releases).

### Windows

Run the `Setup.exe` installer. It adds a **P7** icon to your system tray, sets up your firewall automatically, and optionally starts with Windows.

### macOS

Run the `.pkg` installer. The app appears as a **P7** icon in your menu bar — no Dock icon. Choose **Start at Login** from its menu to have it launch whenever you sign in.

### Using it

1. Open ProPresenter and enable **Network** in Preferences → Network
2. Launch SlideProRelay
3. Click the **P7** icon and choose **Settings…** — the Overview tab shows your network URL (e.g. `http://192.168.1.42:5174`) and a QR code
4. Share the URL or QR code — attendees open it in any browser

### Screen view

Viewers can tap **Screen** on the phone page to see a picture of your output screen instead of the slide text.

- SlideProRelay picks the output display automatically by matching ProPresenter's audience screen. It never picks your main display on its own, so on a single-display computer nothing is captured until you choose a display under **Settings → Capture**.
- On macOS, the first capture asks for **Screen Recording** permission.

---
---

## Technical Reference

### Requirements

- Windows 10+ or macOS 13+
- ProPresenter 7 with Network enabled
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build or run from source — the installers include the runtime

### ProPresenter Setup

Open ProPresenter → **Preferences** → **Network** → enable **Enable Network**. The port is auto-detected — you normally don't need to configure anything.

### Building

**Windows installer** (requires [Inno Setup 6](https://jrsoftware.org/isdl.php) or newer):

```powershell
.\installer\build.ps1
# or with a specific version:
.\installer\build.ps1 -Version 1.2.0
```

Output: `installer\output\SlideProRelay-1.0.0-Setup.exe`. Without `-Version`, the version comes from `Directory.Build.props`.

**macOS app** (requires Xcode CLT and .NET macOS workload):

```bash
xcode-select --install
dotnet workload install macos

# Unsigned build (local use):
./installer-mac/build.sh --skip-signing

# Signed + notarized release build:
./installer-mac/build.sh --team-id YOUR_TEAM_ID --apple-id you@example.com --password xxxx-xxxx-xxxx-xxxx
```

Output: `installer-mac/output/SlideProRelay-1.0.0.pkg`

For unsigned builds, Gatekeeper will warn on first open — right-click the `.pkg` → Open to bypass.

**Run from source:**

```powershell
# Windows
dotnet run --project src\SlideProRelay.Server\SlideProRelay.Server.csproj
```

```bash
# macOS
dotnet run --project src/SlideProRelay.Server/SlideProRelay.Server.csproj
```

This runs the relay on its own: the phone page and API work, and it is configured through `appsettings.json` (see below). The settings page only works under the tray or menu bar app, which supplies the settings file.

**Tests:**

```bash
dotnet test tests/SlideProRelay.Tests/SlideProRelay.Tests.csproj
```

> On macOS, avoid `dotnet build` at the repo root — the Windows tray project won't build. Build individual projects instead.

### Configuration

The tray and menu bar apps are configured from the settings page — **Settings…** in the app's menu, or `http://localhost:5174/settings` on the computer running SlideProRelay. It is not served to other devices on the network.

| Tab | What it sets |
|---|---|
| Overview | ProPresenter connection status, phone URL and QR code |
| Connection | ProPresenter host, port and polling interval; relay port |
| Capture | Screen capture on/off and which display to capture |
| API Integration | SlidePro.io API key, mode (Off / Text / Full Slide) and presentation |

Settings are saved to `SlideProRelay/settings.json` in your user application-data folder (`%AppData%` on Windows) and apply immediately. Changing the relay port restarts the relay.

When running from source, the relay reads `src/SlideProRelay.Server/appsettings.json` instead:

```json
"ProPresenter": {
  "Host": "localhost",
  "Port": 50001,
  "PollingIntervalMs": 500,
  "AutoDetectPort": true
}
```

| Key | Default | Description |
|---|---|---|
| `ProPresenter:Host` | `localhost` | Machine running ProPresenter |
| `ProPresenter:Port` | `50001` | Fallback port when auto-detection finds nothing |
| `ProPresenter:PollingIntervalMs` | `500` | How often the relay asks ProPresenter for the current slide |
| `ProPresenter:AutoDetectPort` | `true` | Read the port from ProPresenter's own preferences |
| `Relay:Port` | `5174` | Port the phone page is served on |
| `ScreenCapture:Enabled` | `true` | Capture the output display on each slide change |
| `ScreenCapture:DisplayIndex` | `0` | 1-based display to capture; `0` = auto (never the primary display) |
| `SlidePro:SendTextUpdates` | `false` | Text mode — send slide text to SlidePro.io |
| `SlidePro:Enabled` | `false` | Full Slide mode — upload each screen capture to SlidePro.io |
| `SlidePro:ApiKey` | | SlidePro.io API key |
| `SlidePro:PresentationId` | | SlidePro.io presentation to send slides to |
| `SlidePro:BaseUrl` | `https://slidepro.io/api/public/v1` | SlidePro.io public API |

Port auto-detection is on by default — SlideProRelay reads the active port directly from ProPresenter's preferences and re-checks on reconnect, so `Port` is only used as a fallback.

Override any setting with an environment variable:

```powershell
$env:ProPresenter__Port = "50002"
dotnet run --project src\SlideProRelay.Server\SlideProRelay.Server.csproj
```

### Screen capture

On each slide change the relay grabs a JPEG of the output display and serves the latest one from `/api/screen-capture`. Windows uses DXGI Desktop Duplication; macOS uses the built-in `screencapture` tool. Full Slide mode for SlidePro.io uploads these captures, so it needs screen capture turned on.

### API

Open to devices on your network:

| Endpoint | Description |
|---|---|
| `GET /` | Live slide display (phone-friendly) |
| `GET /events` | Server-Sent Events stream of slide updates |
| `GET /api/current` | Current and next slide as JSON |
| `GET /api/health` | Returns `ok` if the relay is running |
| `GET /api/qr` | QR code (PNG) for the phone URL |
| `GET /api/screen-capture` | Latest capture of the output display (JPEG); `204` when there is none |
| `GET /api/screen-capture/key` | Which slide the latest capture belongs to, and when it was taken |
| `GET /api/slide-image` | Experimental: ProPresenter's thumbnail of the current slide (JPEG) |

Served only to the computer running SlideProRelay — other devices get a `404`:

| Endpoint | Description |
|---|---|
| `GET /settings` | Settings page |
| `GET` / `POST /api/settings` | Read and save settings |
| `GET /api/network-url` | Local and network URLs of the phone page |
| `GET /api/displays` | Displays available for capture and which one is selected |
| `GET /api/slidepro/presentations` | Your SlidePro.io presentations |
| `GET /api/raw` | Raw slide status from ProPresenter, for diagnostics |


## License
GNU Affero General Public License v3.0 — see [LICENSE](LICENSE) for details.
