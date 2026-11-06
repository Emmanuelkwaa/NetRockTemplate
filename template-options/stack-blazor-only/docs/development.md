# Development

> Back to [README](../README.md)

Aspire runs the Blazor app locally and hosts the dashboard (logs, traces, metrics):

```bash
dotnet run --project src/backend/NetRockTemplate.AppHost
```

The app listens on the base port (default `http://localhost:13000`). Launch the AppHost from Rider or Visual Studio to debug the app with breakpoints.

## Where things live

- Pages and layout: `src/frontend/NetRockTemplate.UI/Components`
- Static files: `src/frontend/NetRockTemplate.UI/wwwroot`
- Settings: `appsettings.json` and `appsettings.Development.json` in the UI project. Development secrets belong in user secrets, never in these files.

## Rendering

The project uses static server-side rendering: pages render on the server and forms post back. To make one component interactive, add an interactive render mode to the app and use `@rendermode` on that component. See the [Blazor render modes documentation](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes).
