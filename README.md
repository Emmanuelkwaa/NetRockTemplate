<div align="center">

# NETrock

<!-- @init:if frontend=svelte -->
**Full-stack .NET 10 + SvelteKit foundation for building real products.**
<!-- @init:else -->
**.NET 10 foundation for building real products.**
<!-- @init:end -->

OAuth with 8 providers. TOTP two-factor auth. Admin-configurable everything. 1000+ tests. API-first - use the included frontend or bring your own.

[![CI](https://github.com/fpindej/netrock/actions/workflows/ci.yml/badge.svg)](https://github.com/fpindej/netrock/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
<!-- @init:if frontend=svelte -->
[![SvelteKit](https://img.shields.io/badge/SvelteKit-Svelte_5-FF3E00?logo=svelte&logoColor=white)](https://svelte.dev/)
<!-- @init:end -->
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Discord](https://img.shields.io/badge/Discord-Join-5865F2?logo=discord&logoColor=white)](https://discord.gg/5rHquRptSh)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/fpindej/netrock)

**[Live Demo](https://demo.netrock.dev)** · **[Documentation](#documentation)** · **[Quick Start](#quick-start)** · **[Discord](https://discord.gg/5rHquRptSh)**

</div>

---

## Why NETrock?

Every project starts the same way: authentication, role management, rate limiting, validation, API documentation, Docker setup... You spend weeks on infrastructure before writing a single line of business logic.

<!-- @init:if frontend=svelte -->
**NETrock skips all of that.** It ships a production-grade .NET 10 API with a SvelteKit frontend that goes far beyond boilerplate. Users can log in with Google, GitHub, Discord, Apple, Microsoft, Facebook, LinkedIn, or X - configured by admins from the UI, no redeploy needed. Two-factor authentication with TOTP and recovery codes is built in. The permission system enforces role hierarchy. The admin panel manages users, roles, background jobs, and OAuth providers with AES-256-GCM encrypted credentials. Full audit trail, PII compliance, and rate limiting are part of the foundation.
<!-- @init:else -->
**NETrock skips all of that.** It ships a production-grade .NET 10 API that goes far beyond boilerplate. Users can log in with Google, GitHub, Discord, Apple, Microsoft, Facebook, LinkedIn, or X - configured by admins from the UI, no redeploy needed. Two-factor authentication with TOTP and recovery codes is built in. The permission system enforces role hierarchy. The admin panel manages users, roles, background jobs, and OAuth providers with AES-256-GCM encrypted credentials. Full audit trail, PII compliance, and rate limiting are part of the foundation.
<!-- @init:end -->

**For developers**, every convention is documented, the architecture is tested at the layer boundary, and Claude Code skills automate common workflows. **For end users**, the product they interact with has dark mode, i18n, a command palette, responsive design, and security features they expect from a real application.

**Fork it, init it, own it.** After initialization, there is no dependency on "the template." It's your code, your architecture, your product. Every decision is documented so you can understand it, change it, or throw it away.

---

## What You Get

**Backend** - JWT auth with token rotation and reuse detection, TOTP two-factor authentication with recovery codes, OAuth/OIDC external login with 8 providers (admin-configurable from the UI), permission-based authorization with role hierarchy, transactional email with Fluid templates, rate limiting, HybridCache, PostgreSQL with soft delete and audit trails, S3-compatible file storage, Hangfire background jobs, OpenAPI docs, health checks, Result pattern with ProblemDetails. [See full details ->](docs/features.md#backend--net-10--c-13)

<!-- @init:if frontend=svelte -->
**Frontend** - Svelte 5 runes, type-safe API client from OpenAPI, Tailwind CSS 4 with shadcn-svelte component library, Cmd+K command palette with permission-gated navigation, BFF proxy with CSRF protection, i18n (English + Czech, add more with a single JSON file), dark mode, responsive design with 44px touch targets, admin panel with user/role/job/OAuth provider management. [See full details ->](docs/features.md#frontend--sveltekit--svelte-5)
<!-- @init:end -->
<!-- @init:if frontend=blazor -->
**Frontend** - Blazor SSR app in `src/frontend/NetRockTemplate.UI`, created from the stock `dotnet new blazor` template on your SDK, wired to Aspire and to the API through a named `HttpClient`. It has no sign-in UI yet; add pages per project.
<!-- @init:end -->

**Infrastructure** - Aspire AppHost for local development (one command for the full stack with OTEL dashboard and MailPit for email testing), production-ready Dockerfiles, init script for project bootstrapping, GitHub Actions CI with smart path filtering, Claude Code skills for development workflows. [See full details ->](docs/features.md#infrastructure--devops)

**Security** - HttpOnly JWT cookies, refresh token rotation with reuse detection, TOTP 2FA with challenge tokens and recovery codes, OAuth state tokens with TOCTOU protection, AES-256-GCM encrypted provider credentials, PII compliance with server-side masking, security stamp propagation, CSP with nonces, rate limiting, input validation everywhere. [See full details ->](docs/security.md)

---

## Quick Start

> **Want to see it first?** Check out the [live demo](https://demo.netrock.dev).

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
<!-- @init:if frontend=svelte -->
- [Node.js 22+](https://nodejs.org/) (run `corepack enable` for pnpm)
<!-- @init:end -->
- [Git](https://git-scm.com/)
<!-- @init:if template -->

### Create Your Project

```bash
git clone https://github.com/fpindej/netrock.git netrock
cd netrock
./init.sh            # macOS / Linux
.\init.ps1           # Windows (PowerShell)
```

Init asks for a name and base port, then the options below, and writes a new project into `../<Name>` (or `--output`). The template folder is never modified. Node.js is only needed for `api-svelte`.

| Option | Values | Default |
|---|---|---|
| `--stack` | `api-svelte` (API + SvelteKit), `api-blazor` (API + Blazor SSR), `api` (API only), `blazor` (Blazor SSR only, no backend) | `api-svelte` |
| `--auth` | `jwt` (built-in accounts, 2FA, OAuth, roles), `keycloak` (Keycloak as identity provider), `none`. Not for `blazor` | `jwt` |
| `--keycloak-dev` | `managed` (Aspire runs Keycloak with a generated realm and test user), `external` (your server). Only with `--auth keycloak` | `external` |
| `--keycloak-prod` | `managed` (the production stack runs Keycloak), `external` (your server; realm settings required at deployment). Only with `--auth keycloak` | `external` |
| `--deploy` | `aspire` (Aspire generates production compose), `compose` (own `docker-compose.yaml`) | `aspire` |
| `--database` | `managed` (PostgreSQL container), `external` (your server). Not for `blazor` | `managed` |

Blazor projects start from the stock `dotnet new blazor` template (static server rendering, no interactivity) on your installed SDK, so they have no sign-in UI. In `api-blazor`, `--auth` protects the API and the Blazor app calls it through a named `HttpClient`; add sign-in pages per project.

Other flags: `--name`, `--port`, `--output`, `--email` / `--password` (jwt superuser or Keycloak test user), `--keycloak-authority` / `--keycloak-client-id` / `--keycloak-client-secret` (development realm, `--keycloak-dev external` only), `--yes`, `--no-migration`, `--no-commit`, `--no-aspire`. Run `./init.sh --help` for details.

```bash
./init.sh --name MyApi --stack api --auth keycloak --deploy compose --yes
./init.sh --name MyApp --auth keycloak --keycloak-dev managed --keycloak-prod managed --yes
./init.sh --name MySite --stack blazor --yes
```

Maintainers: `tools/init/verify-matrix.sh` builds and checks every option combination.
<!-- @init:end -->

### Launch Everything

```bash
dotnet run --project src/backend/NetRockTemplate.AppHost
```

<!-- @init:if frontend=svelte -->
That's it. Aspire starts the infrastructure (PostgreSQL, MinIO, MailPit) as containers and launches the API and frontend dev server. The Aspire Dashboard URL appears in the console - all service URLs (API docs, pgAdmin, MinIO) are linked from the Dashboard.
<!-- @init:else -->
<!-- @init:if frontend=blazor -->
That's it. Aspire starts the infrastructure (PostgreSQL, MinIO, MailPit) as containers and launches the API and the Blazor app. The Aspire Dashboard URL appears in the console - all service URLs (API docs, pgAdmin, MinIO) are linked from the Dashboard.
<!-- @init:else -->
That's it. Aspire starts the infrastructure as containers and launches the API. The Aspire Dashboard URL appears in the console - all service URLs (API docs, pgAdmin, MinIO) are linked from the Dashboard.
<!-- @init:end -->
<!-- @init:end -->

| Service | URL |
|---|---|
| **Aspire Dashboard** | Shown in console output |
<!-- @init:if frontend=svelte -->
| **Frontend** | `http://localhost:<BASE_PORT>` (default: `http://localhost:13000`) |
<!-- @init:end -->
<!-- @init:if frontend=blazor -->
| **Blazor app** | `http://localhost:<BASE_PORT>` (default: `http://localhost:13000`) |
<!-- @init:end -->
<!-- @init:if frontend=blazor,none -->
| **API docs** | `http://localhost:<BASE_PORT + 2>` (default: `http://localhost:13002`) |
<!-- @init:end -->
| **MailPit (Email Testing)** | `http://localhost:<BASE_PORT + 8>` |

<!-- @init:if auth=jwt -->
A Superuser is seeded in development with the credentials you gave init (defaults: `superuser@test.com` / `Superuser123!`), stored in AppHost user secrets.

<!-- @init:end -->
<!-- @init:if keycloak-dev=managed -->
Sign-in goes through the local Keycloak that Aspire starts (admin console on `http://localhost:<BASE_PORT + 9>`). Sign in with the test user you gave init (defaults: `superuser@test.com` / `Superuser123!`), stored in AppHost user secrets. See [docs/keycloak.md](docs/keycloak.md).
<!-- @init:end -->
<!-- @init:if keycloak-dev=external -->
Sign-in goes through your Keycloak realm. Set up the client first: see [docs/keycloak.md](docs/keycloak.md).

<!-- @init:end -->
<!-- @init:if database=external -->
The database is external: set its connection string with `dotnet user-secrets set ConnectionStrings:Database "<connection string>" --project src/backend/NetRockTemplate.AppHost` before launching.

<!-- @init:end -->
Production deployment: [docs/deployment.md](docs/deployment.md).

### Start Building

Add your domain entities, services, and pages - the architecture guides you.

---

## Claude Code Integration

NETrock ships with 20+ native [Claude Code](https://docs.anthropic.com/en/docs/claude-code) skills that automate common development workflows. Type `/` in Claude Code to see all available skills.

| Skill | What it does |
|---|---|
<!-- @init:if frontend=svelte -->
| `/new-feature` | Scaffold a full-stack feature - entity, service, controller, frontend page |
<!-- @init:else -->
| `/new-feature` | Scaffold a feature - entity, service, controller |
<!-- @init:end -->
| `/new-endpoint` | Add an API endpoint to an existing feature |
| `/new-entity` | Create a domain entity with EF Core configuration |
<!-- @init:if frontend=svelte -->
| `/new-page` | Create a frontend page with routing, i18n, and navigation |
| `/gen-types` | Regenerate frontend TypeScript types from the OpenAPI spec |
<!-- @init:end -->
| `/create-pr` | Create a PR with session docs, reviews, and labels |
| `/review-pr` | Review a PR for production-readiness |
<!-- @init:if frontend=svelte -->
| `/review-design` | Review frontend components for UI/UX standards |
<!-- @init:end -->
| `/create-issue` | Create a GitHub issue with labels |
| `/create-release` | Create a GitHub release with auto-generated notes |

Skills are also loaded automatically when Claude Code plans work - it reads the relevant skill and follows the procedure without you having to invoke it. The project also includes `CLAUDE.md` and `FILEMAP.md` as structured context files, plus 12 specialized agents (`.claude/agents/`) and lifecycle hooks (`.claude/hooks/`) for auto-formatting, safety gates, and quality checks. No separate onboarding needed.

---

## Documentation

| File | Purpose |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Hard rules, verification, agent team guide, architecture overview |
| [`.claude/agents/`](.claude/agents/) | Specialized agents - engineers, reviewers, designers, devops, tech writer |
| [`.claude/skills/`](.claude/skills/) | Procedures, convention references, and design tokens (use `/` to list) |
| [`.claude/hooks/`](.claude/hooks/) | Lifecycle hooks - auto-format, safety gates, quality checks |
| [`FILEMAP.md`](FILEMAP.md) | Change impact tables - "when you change X, also update Y" |

Deep dives: **[Features](docs/features.md)** · **[Security](docs/security.md)** · **[Architecture](docs/architecture.md)** · **[Development](docs/development.md)** · **[Before You Ship](docs/before-you-ship.md)** · **[Troubleshooting](docs/troubleshooting.md)**

---

<!-- @init:if frontend=svelte -->
## Localization

i18n with [Paraglide JS](https://inlang.com/m/gerre34r/library-inlang-paraglideJs) - type-safe keys, SSR-compatible, auto-detection via `Accept-Language`. Ships with English and Czech. Adding a language is a single JSON file.

---
<!-- @init:end -->

## What This Is NOT

NETrock is opinionated by design. It's not:

- **A generic starter** - it makes real choices (PostgreSQL, not "any database"; JWT cookies, not "pluggable auth")
- **A microservices framework** - it's a monolith, because that's what 95% of products should start as
<!-- @init:if frontend=svelte -->
- **A frontend framework** - SvelteKit is included, but you can use just the API with any other frontend
- **Magic** - you still need to understand .NET (and SvelteKit if you keep it)
<!-- @init:else -->
- **Magic** - you still need to understand .NET
<!-- @init:end -->

---

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## Support the Project

NETrock is free and open source under the [MIT License](LICENSE). If it saves you time, consider supporting its development:

<a href="https://buymeacoffee.com/fpindej" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" height="50"></a>

Star the repo on [GitHub](https://github.com/fpindej/netrock) · Join the [Discord](https://discord.gg/5rHquRptSh) · Need custom development, consulting, or training? [Get in touch](mailto:contact@mail.pindej.cz)

---

## License

This project is licensed under the [MIT License](LICENSE).
