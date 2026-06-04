# ProSlideRelay

ProSlideRelay runs on the same computer as ProPresenter 7. It reads the current slide text and shares it with phones or other devices on your local network — no extra hardware required.

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

```powershell
dotnet run --project src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
```

When it starts, you will see something like:

```
  ProSlideRelay is running

  Local:   http://localhost:5174
  Network: http://192.168.1.42:5174

  Open the Network URL on your phone to follow along.
```

Type the **Network** address into a phone browser. The slide text will appear and update as you advance slides in ProPresenter.

Press `Ctrl+C` to stop.

## Configuration

Edit `src/ProSlideRelay.Server/appsettings.json` to change settings:

```json
"ProPresenter": {
  "Host": "localhost",
  "Port": 50001,
  "PollingIntervalMs": 500
}
```

You can also override any setting with an environment variable:

```powershell
$env:ProPresenter__Port = "50002"
dotnet run --project src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
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

The installer is written to `installer\output\ProSlideRelay-1.0.0-Setup.exe`.

**What the installer does:**
- Installs to `Program Files\ProSlideRelay`
- Adds a Start Menu shortcut
- Optional: desktop shortcut
- Optional: start automatically when Windows starts
- Adds a Windows Firewall rule so phones on your network can connect
- Registers a proper uninstaller (via Add/Remove Programs)

Settings are saved to `%APPDATA%\ProSlideRelay\settings.json` so they survive reinstalls and updates.

## macOS app (menu bar)

The macOS app lives in the menu bar as a **P7** icon — no Dock icon. Click it to open the browser, adjust settings, or enable "Start at Login".

**Prerequisites (one-time, on your Mac):**

```bash
xcode-select --install               # Xcode Command Line Tools
dotnet workload install macos        # .NET macOS AppKit bindings
```

**Unsigned build (anyone — for local use/testing):**

```bash
chmod +x installer-mac/build.sh
./installer-mac/build.sh --skip-signing
```

Output: `installer-mac/output/ProSlideRelay-1.0.0.pkg`
macOS will warn about the unsigned installer on other Macs (right-click → Open to bypass).

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

```powershell
dotnet build
dotnet test
dotnet run --project src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
```
