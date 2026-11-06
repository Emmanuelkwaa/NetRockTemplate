# NetRockTemplate

A Blazor web app with static server-side rendering (SSR), orchestrated by .NET Aspire. Created from the NETrock template with `--stack blazor`.

The UI in `src/frontend/NetRockTemplate.UI` comes from the stock `dotnet new blazor` template (`--interactivity None --auth None`). It has no sign-in UI and no backend API. Add pages, authentication and services as the project needs them.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for production images)
- [Git](https://git-scm.com/)

## Run it

```bash
dotnet run --project src/backend/NetRockTemplate.AppHost
```

| Service | URL |
|---|---|
| **Aspire Dashboard** | Shown in console output |
| **Blazor app** | `http://localhost:<BASE_PORT>` (default: `http://localhost:13000`) |

## Layout

| Path | Contains |
|---|---|
| `src/frontend/NetRockTemplate.UI` | Blazor SSR app (stock template, plus `AddServiceDefaults` and `MapDefaultEndpoints`) |
| `src/backend/NetRockTemplate.AppHost` | Aspire AppHost: runs the app in development and describes production |
| `src/backend/NetRockTemplate.ServiceDefaults` | OpenTelemetry, service discovery, HTTP resilience and the `/health/live` endpoint |

## Documentation

- [Development](docs/development.md)
- [Deployment](docs/deployment.md)
- [`CLAUDE.md`](CLAUDE.md) and [`FILEMAP.md`](FILEMAP.md) for Claude Code
