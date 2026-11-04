# Development

> Back to [README](../README.md)

## Local Development (Aspire)

<!-- @init:if frontend=yes -->
Aspire is the sole local development workflow. It starts all infrastructure (PostgreSQL, MinIO, MailPit) as containers and launches the API and frontend dev server.
<!-- @init:else -->
Aspire is the sole local development workflow. It starts all infrastructure (PostgreSQL, MinIO, MailPit) as containers and launches the API.
<!-- @init:end -->

```bash
dotnet run --project src/backend/NetRockTemplate.AppHost
```

The Aspire Dashboard URL appears in the console. All service URLs (API docs, pgAdmin, MinIO, MailPit) are linked from the Dashboard.

### Debugging with breakpoints in Rider/VS

Launch the AppHost project from your IDE. The API runs in-process with full debugger support. Infrastructure containers are still managed by Aspire.

### Configuration

<!-- @init:if auth=jwt -->
Behavioral config (log levels, rate limits, JWT lifetimes, CORS, OAuth providers) lives in `appsettings.Development.json`. Infrastructure connection strings are injected by Aspire via environment variables - no manual config needed.

Development secrets live in AppHost user secrets (`dotnet user-secrets list --project src/backend/NetRockTemplate.AppHost`). Init stores the dev superuser there as `Parameters:seed-user-email` and `Parameters:seed-user-password`; the AppHost passes them to the API as `Seed__Users__0__*`.
<!-- @init:else -->
Behavioral config (log levels, rate limits, CORS) lives in `appsettings.Development.json`. Infrastructure connection strings are injected by Aspire via environment variables - no manual config needed.
<!-- @init:end -->
<!-- @init:if auth=keycloak -->

The Keycloak client secret lives in AppHost user secrets as `Parameters:keycloak-client-secret`. See [Keycloak](keycloak.md).
<!-- @init:end -->
<!-- @init:if database=external -->

The database is external. Set its connection string in AppHost user secrets: `dotnet user-secrets set ConnectionStrings:Database "<connection string>" --project src/backend/NetRockTemplate.AppHost`.
<!-- @init:end -->

### Email Testing

<!-- @init:if auth=jwt -->
MailPit captures all outgoing emails locally. Access the MailPit web UI from the Aspire Dashboard (port `BASE_PORT + 8`). Email verification, password reset, invitation, and 2FA disable notification emails all appear there immediately.
<!-- @init:else -->
MailPit captures all outgoing emails locally. Access the MailPit web UI from the Aspire Dashboard (port `BASE_PORT + 8`).
<!-- @init:end -->

---

## Claude Code Skills

NETrock ships with 20+ native Claude Code skills that automate common development tasks. Type `/` in Claude Code to see all available skills.

Key skills for daily development:

| Skill | When to use |
|---|---|
| `/new-feature` | Adding a new full-stack feature (entity through to frontend page) |
| `/new-endpoint` | Adding an API endpoint to an existing feature |
| `/new-entity` | Creating a new backend entity with EF Core config |
| `/new-page` | Creating a new frontend page with routing and i18n |
| `/gen-types` | After changing backend DTOs or endpoints |
| `/create-pr` | When your work is ready for review |
| `/review-pr` | Reviewing someone else's PR |
| `/review-design` | Checking UI/UX quality of frontend components |

The project context files (`CLAUDE.md`, `FILEMAP.md`) plus specialized agents, convention skills, and lifecycle hooks provide Claude Code with deep understanding of the architecture and conventions. No separate onboarding needed.

---

## Database Migrations

```bash
dotnet ef migrations add <Name> \
  --project src/backend/<YourProject>.Infrastructure \
  --startup-project src/backend/<YourProject>.WebApi \
  --output-dir Persistence/Migrations
```

Migrations auto-apply on startup in Development, and in other environments when `Database:MigrateOnStartup` is `true`. See [Deployment](deployment.md#database-migrations).

---

## Production Deployment

See [Deployment](deployment.md) for the production stack, its settings and how migrations run.

See [Before You Ship](before-you-ship.md) for the configuration checklist.
