# 2026-11-03 - Init options (auth, deploy, database, frontend)

## Summary

The template gained an option-aware project generator. `init.sh` / `init.ps1` launch `tools/init/init.cs`, a .NET 10 file-based app that copies the template to a new folder and applies four options (24 combinations). The template source stays the jwt + managed PostgreSQL + SvelteKit variant and keeps building and running as before.

| Option | Values | Default |
|---|---|---|
| `--auth` | `jwt`, `keycloak`, `none` | `jwt` |
| `--deploy` | `aspire`, `compose` | `aspire` |
| `--database` | `managed`, `external` | `managed` |
| `--frontend` | `yes`, `no` | `yes` |

## How variants are produced

- `template-options/manifest.json`: rules with a condition (`auth=keycloak,none frontend=yes`), delete globs, an overlay folder and JSON edits.
- `template-options/auth-common`, `auth-keycloak`, `auth-none`, `deploy-compose`: overlay files, mirroring repo paths.
- `@init:if` / `@init:else` / `@init:end` comment markers in shared files. A branch flagged `uncomment` carries other-variant code as comments so the template still compiles. The `template` condition marks template-only content. Marker lines are always removed; init fails if any marker or `NetRockTemplate` text is left.
- Init renames the project, replaces ports, regenerates secrets, writes dev secrets (seed user, Keycloak client secret) to AppHost user secrets, clears and regenerates migrations, and refuses non-empty output folders.

## Key decisions

- Keycloak is an existing external server, identity provider only. The API validates its tokens (exact issuer, audience = client ID, `sub` must be a GUID, claim mapping off) and runs the code flow with PKCE and an encrypted state cookie. The existing access and refresh cookies are reused, so the frontend middleware barely changes.
- `none` removes authentication and every user-dependent feature, keeping health checks, jobs, email engine, file storage, CORS, origin validation and security headers.
- Defaults and open items answered by Emmanuel: `--deploy` defaults to `aspire`; production storage is always external (MinIO runs in Aspire run mode only); the personal seed user is removed from the template; `.github` stays out; API-only projects redirect `/` to Scalar outside Production.
- Own compose is the single-instance shape from Glowcyte's layout, with an optional commented Coolify network.

## Fixes found along the way

- Nothing created `pg_trgm`, so fuzzy search failed on a fresh database. The model now declares the extension and a migration creates it.
- Migrations only ran in Development; `Database:MigrateOnStartup` enables them elsewhere (on by default for managed PostgreSQL in production).
- The SvelteKit proxy followed API redirects and could drop extra `Set-Cookie` headers; it now passes both through.
- The compose frontend healthcheck depended on S3 through `/api/health`; it is now a local liveness probe.

## Verification

- `tools/init/verify-matrix.sh all`: 175 checks passed (template build, tests and frontend checks; 6 auth x frontend full builds with tests and frontend checks; all 24 combinations build their AppHost and pass `aspire publish` or `docker compose config`).
- Smoke tests: JWT login under Aspire; Keycloak login, refresh and logout against a throwaway local Keycloak; `none` API-only health and Hangfire; `aspire deploy` and own compose production stacks; fresh production database migrated; template unchanged after generation.

## Follow-ups

- Local Node 22.12 is below the lockfile's minimum (22.13); frontend checks ran in a `node:22` container.
- `Aspire.Hosting.Docker` only has a preview build matching Aspire 13.1.2; moving Aspire to 13.2+ gets a stable one.
- Existing NuGet advisories (MailKit, MessagePack, Microsoft.OpenApi, OpenTelemetry, Cryptography.Pkcs) predate this work.
- CI/CD sections in the docs still describe workflows removed with `.github`.
