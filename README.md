# SlideProRelay

**Put your lyrics on every phone in the room — automatically.**

SlideProRelay runs on the same computer as ProPresenter 7. As you advance slides, the text appears live on any phone or tablet connected to your Wi-Fi. Nothing for your congregation to install — they just open a link in their browser.

> ### SlideProRelay is — and will always be — free and open source.
> No subscriptions. No licenses. No catch. Free forever.

---

## Features

- Live slide text delivered to any phone or tablet on your network
- Updates instantly as you advance slides — no refresh needed
- Works over your existing Wi-Fi — no extra hardware required
- Reconnects automatically if ProPresenter restarts
- Windows system tray app and macOS menu bar app — runs quietly in the background

## Coming Soon

- **Text customization** — adjust font size, color, and style to suit your preferences
- **Chord charts** — display chords alongside lyrics for musicians
- **Translations** — show slide text in multiple languages simultaneously

## Sponsored by SlidePro.io

SlideProRelay is proudly sponsored by **[SlidePro.io](https://slidepro.io)** — a platform built for churches that want to take congregation engagement further.

Coming soon: a direct integration so ProPresenter 7 can relay slides straight to SlidePro.io, enabling cloud-based delivery, remote viewers, and more — all without leaving ProPresenter.

---

## Getting Started

### Windows

Download and run `SlideProRelay-Setup.exe`. The installer adds a system tray icon, sets up your firewall automatically, and optionally starts with Windows.

### macOS

Download and run `SlideProRelay.pkg`. The app appears as a **P7** icon in your menu bar — no Dock icon. Click it to get the link to share with your congregation.

### Using it

1. Open ProPresenter and enable **Network** in Preferences → Network
2. Launch SlideProRelay
3. Share the network URL shown (e.g. `http://192.168.1.42:5174`) — attendees open it in any browser

---
---

## Technical Reference

### Requirements

- Windows 10+ or macOS 12+
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or the SDK to build)
- ProPresenter 7 with Network enabled

### ProPresenter Setup

Open ProPresenter → **Preferences** → **Network** → enable **Enable Network**. The port is auto-detected — you normally don't need to configure anything.

### Building

**Windows installer** (requires [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
.\installer\build.ps1
# or with a specific version:
.\installer\build.ps1 -Version 1.2.0
```

Output: `installer\output\SlideProRelay-1.0.0-Setup.exe`

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

**Tests:**

```bash
dotnet test
```

> On macOS, avoid `dotnet build` at the repo root — the Windows tray project won't build. Build individual projects instead.

### Configuration

`src/SlideProRelay.Server/appsettings.json`:

```json
"ProPresenter": {
  "Host": "localhost",
  "Port": 50001,
  "PollingIntervalMs": 500,
  "AutoDetectPort": true
}
```

Port auto-detection is on by default — SlideProRelay reads the active port directly from ProPresenter's preferences and re-checks on reconnect, so `Port` is only used as a fallback.

Override any setting with an environment variable:

```powershell
$env:ProPresenter__Port = "50002"
dotnet run --project src\SlideProRelay.Server\SlideProRelay.Server.csproj
```

### API

| Endpoint | Description |
|---|---|
| `GET /` | Live slide display (phone-friendly) |
| `GET /events` | Server-Sent Events stream |
| `GET /api/current` | Current slide as JSON |
| `GET /api/health` | Returns `ok` if the relay is running |


## License
GNU Affero General Public License v3.0 — see [LICENSE](LICENSE) for details.