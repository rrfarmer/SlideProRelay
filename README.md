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

## Development

```powershell
dotnet build
dotnet test
dotnet run --project src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
```
