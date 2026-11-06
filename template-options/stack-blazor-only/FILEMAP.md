# FILEMAP - Change Impact Reference

> **When you change X, also update Y.**

| When you change... | Also update... |
|---|---|
| `NetRockTemplate.UI/Program.cs` service registrations | `docs/development.md` if setup steps change |
| `NetRockTemplate.UI/NetRockTemplate.UI.csproj` (project references) | `NetRockTemplate.UI/Dockerfile` COPY lines |
| `NetRockTemplate.ServiceDefaults` (packages, `MapDefaultEndpoints`) | `NetRockTemplate.UI/Dockerfile`, healthcheck paths in compose files |
| AppHost `Program.cs` (ports, resources, parameters) | `docs/deployment.md`, `README.md` service table |
| `src/backend/Directory.Packages.props` | Nothing else - versions live only here |
<!-- @init:if deploy=compose -->
| `docker-compose.yaml` | `.env.example`, `docs/deployment.md` |
<!-- @init:end -->
