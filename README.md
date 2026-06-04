# ProSlideRelay

ProSlideRelay runs on the same computer as ProPresenter 7. It reads the current slide text and shares it with phones or other devices on your local network — no extra hardware required.

## What it does

- Connects to ProPresenter's built-in API (the Network tab in ProPresenter settings).
- Serves a local web page that updates as slides change.
- Works over your existing Wi-Fi — phones just open a URL in their browser.

## Requirements

- Windows or macOS
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- ProPresenter 7 with **Network** enabled (Settings → Network → Enable Network)

## Running

```powershell
dotnet run --project src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
```

Then open `http://localhost:5000/api/current` in a browser to see the current slide.

## Configuration

Edit `src/ProSlideRelay.Server/appsettings.json` to change the ProPresenter connection:

```json
"ProPresenter": {
  "Host": "localhost",
  "Port": 50001,
  "PollingIntervalMs": 500
}
```

The default port `50001` matches ProPresenter's default. Only change it if you changed it in ProPresenter's Network settings.

## API

| Endpoint | Description |
|---|---|
| `GET /api/health` | Returns `ok` if the relay is running |
| `GET /api/current` | Returns the current and next slide text |

## Development

```powershell
dotnet build
dotnet test
dotnet run --project src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
```
