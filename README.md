# ProSlideRelay

ProSlideRelay is a local relay server for ProPresenter 7. The first goal is to run on the same Windows or macOS machine as ProPresenter, poll the ProPresenter API for the current slide text, and serve a simple live text page for phones or other local devices.

## Current Scope

- .NET 10 / ASP.NET Core local web server.
- ProPresenter 7 API polling, starting with `GET /v1/status/slide`.
- Local browser and phone clients served by the relay.
- Fast lyrics mode first; slow screen capture and external upload are future phases.

## Development

```powershell
dotnet build
dotnet run --project .\src\ProSlideRelay.Server\ProSlideRelay.Server.csproj
```

The default ProPresenter API port in the official OpenAPI spec is `50001`.
