# SlideProRelay

SlideProRelay runs on the same computer as ProPresenter 7. It reads the current slide text and shares it with phones or other devices on your local network — no extra hardware required.

## What it does

- Connects to ProPresenter's built-in API.
- Displays current slide text on any phone or tablet connected to your Wi-Fi.
- Slides update live as you advance through ProPresenter — no refresh needed.
- Shows a "ProPresenter offline" message if the connection drops, and recovers automatically.

## Requirements

- Windows or macOS
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- ProPresenter 7 with Network enabled (see setup below)

## ProPresenter setup

1. Open ProPresenter → **Preferences** → **Network**
2. Turn on **Enable Network**
3. Note the **Port** shown (default is `50001`) — you only need this if it differs from the default

## Running

Windows (PowerShell):

```powershell
dotnet run --project src\SlideProRelay.Server\SlideProRelay.Server.csproj
```

macOS / Linux (bash):

```bash
dotnet run --project src/SlideProRelay.Server/SlideProRelay.Server.csproj
```

When it starts, you will see something like:

```
  SlideProRelay is running

  Local:   http://localhost:5174
  Network: http://192.168.1.42:5174

  Open the Network URL on your phone to follow along.
```

Type the **Network** address into a phone browser. The slide text will appear and update as you advance slides in ProPresenter.

Press `Ctrl+C` to stop.

## Configuration

Edit `src/SlideProRelay.Server/appsettings.json` to change settings:

```json
"ProPresenter": {
  "Host": "localhost",
  "Port": 50001,
  "PollingIntervalMs": 500,
  "AutoDetectPort": true
}
```

**Port auto-detection:** ProPresenter assigns its network/API port automatically
and it can change between launches. When `AutoDetectPort` is `true` (the default)
and `Host` is local, SlideProRelay reads the current port straight from
ProPresenter's own preferences — so you normally never set `Port` at all. The
relay also re-checks on connection loss, so if ProPresenter restarts on a new
port it reconnects on its own. `Port` is used only as a fallback (detection off,
a remote `Host`, or detection unavailable). Auto-detection is implemented on
macOS today; on Windows it falls back to the configured `Port` for now.

You can also override any setting with an environment variable:

```powershell
# Windows (PowerShell)
$env:ProPresenter__Port = "50002"
dotnet run --project src\SlideProRelay.Server\SlideProRelay.Server.csproj
```

```bash
# macOS / Linux (bash)
ProPresenter__Port=50002 dotnet run --project src/SlideProRelay.Server/SlideProRelay.Server.csproj
```

## API

| Endpoint | Description |
|---|---|
| `GET /` | Phone-friendly live slide display |
| `GET /events` | Server-Sent Events stream (used by the browser page) |
| `GET /api/current` | Current slide as JSON |
| `GET /api/health` | Returns `ok` if the relay is running |

## Windows installer

The tray app runs as a system tray icon — no console window. The installer adds a Start Menu shortcut, optional desktop shortcut, optional start-with-Windows, and configures the Windows Firewall automatically.

**Prerequisites (one-time):**
1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php) (free)

**Build the installer:**

```powershell
.\installer\build.ps1
# or with a specific version:
.\installer\build.ps1 -Version 1.2.0
```

The installer is written to `installer\output\SlideProRelay-1.0.0-Setup.exe`.

**What the installer does:**
- Installs to `Program Files\SlideProRelay`
- Adds a Start Menu shortcut
- Optional: desktop shortcut
- Optional: start automatically when Windows starts
- Adds a Windows Firewall rule so phones on your network can connect
- Registers a proper uninstaller (via Add/Remove Programs)

Settings are saved to `%APPDATA%\SlideProRelay\settings.json` so they survive reinstalls and updates.

## macOS app (menu bar)

The macOS app lives in the menu bar as a **P7** icon — no Dock icon. Click it to open the browser, adjust settings, or enable "Start at Login".

**Prerequisites (one-time, on your Mac):**

```bash
xcode-select --install               # Xcode Command Line Tools (provides lipo, codesign, pkgbuild)
dotnet workload install macos        # .NET macOS AppKit bindings
```

You also need the [.NET 10 **SDK**](https://dotnet.microsoft.com/download/dotnet/10.0)
(the SDK, not just the runtime) to build. Verify with `dotnet --version` (expects `10.0.x`)
and `dotnet workload list` (expects `macos` listed).

**Unsigned build (anyone — for local use/testing):**

```bash
chmod +x installer-mac/build.sh
./installer-mac/build.sh --skip-signing
```

By default this produces a **universal** binary (Apple Silicon + Intel). To build only
for your own machine — faster — pass `--arch arm64` (Apple Silicon) or `--arch x64` (Intel).

Output: `installer-mac/output/SlideProRelay-1.0.0.pkg`

Install it by double-clicking the `.pkg` (the app lands in `/Applications`). Because the
build is unsigned, macOS Gatekeeper will warn on first open — **right-click the `.pkg` →
Open** to bypass. The app then runs as a **P7** menu bar icon with no Dock icon; click it
for the browser link, settings, and "Start at Login".

**Signed + notarized build (maintainer release):**

```bash
./installer-mac/build.sh \
  --team-id   YOUR_TEAM_ID \
  --apple-id  you@example.com \
  --password  xxxx-xxxx-xxxx-xxxx
```

Requires an [Apple Developer account](https://developer.apple.com) ($99/year), Developer ID certificates installed in Keychain, and an [app-specific password](https://appleid.apple.com).
The output installer is fully signed and notarized — no Gatekeeper warnings on any Mac.

## Development

```bash
dotnet test
dotnet run --project src/SlideProRelay.Server/SlideProRelay.Server.csproj
```

**Note on building the whole solution:** a plain `dotnet build` at the repo root
builds every project, including `SlideProRelay.Tray` (Windows Forms) — that project
only builds on Windows. On macOS, build the projects you need individually:

```bash
# Console server (the core; used by both the Windows and macOS apps)
dotnet build src/SlideProRelay.Server/SlideProRelay.Server.csproj

# Menu bar app — a macOS .app bundle is self-contained, so it must be
# published (with a runtime identifier), not plain-built:
dotnet publish src/SlideProRelay.Mac/SlideProRelay.Mac.csproj -c Release -r osx-arm64
```

For a packaged `.app` + `.pkg`, use `installer-mac/build.sh` (see the macOS section above).
