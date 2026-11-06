# 2026-11-05 - Project stacks (api-svelte, api-blazor, api, blazor)

## Summary

`--frontend yes|no` is replaced by `--stack`, which picks the project type. The template source is unchanged in behavior: it is still the api-svelte + jwt + managed PostgreSQL variant.

| `--stack` | Creates |
|---|---|
| `api-svelte` (default) | Backend API with the SvelteKit frontend (was `--frontend yes`) |
| `api-blazor` | Backend API with a Blazor SSR frontend |
| `api` | Backend API only (was `--frontend no`) |
| `blazor` | Blazor SSR only, no backend |

`--auth` and `--database` are errors with `blazor`. `--deploy` applies to every stack. That gives 36 API stack combinations plus 2 for `blazor`.

## How it works

- Conditions use two derived keys: `frontend=svelte|blazor|none` and `backend=yes|no`. `blazor` evaluates with `auth=none` and `database=none`, and the auth rules in `manifest.json` are gated on `backend=yes`. The `@init` syntax is unchanged.
- Blazor projects are created during init with `dotnet new blazor --interactivity None --auth None --no-restore`, so they match the installed SDK and no Blazor source lives in the repo. Init then adds `AddServiceDefaults()`, `MapDefaultEndpoints()`, the ServiceDefaults project reference and, for `api-blazor`, a named `HttpClient` "api" with base address `http://api`. Generated pages are untouched. Init fails loudly if the stock `Program.cs` changes shape.
- `template-options/stack-blazor` adds the Blazor Dockerfile (build context `./src`, StripDevConfig handling, curl for the healthcheck) and `src/.dockerignore`. `template-options/stack-blazor-only` replaces README, CLAUDE.md, FILEMAP.md and the docs for `blazor`.
- The AppHost, solution, AppHost csproj, `Directory.Packages.props` and ServiceDefaults carry `backend=yes` and `frontend=blazor` markers. `blazor` reduces the AppHost to the Blazor project plus the compose environment.

## Decisions and conflicts resolved

- ServiceDefaults had no `MapDefaultEndpoints`. Blazor stacks get one that maps `/health/live` (liveness only) in every environment, because compose healthchecks need it in Production. The API keeps its own health endpoints.
- The Keycloak redirect URI was built from `Email:FrontendBaseUrl`, which points at Blazor in `api-blazor`. New optional `Authentication:Keycloak:CallbackBaseUrl` overrides it; init sets it to the API URL for `api-blazor`, compose passes `API_PUBLIC_URL` and Aspire the `api-public-url` parameter. Unset, behavior is unchanged.
- Own compose for `api-blazor` publishes the API on `API_PORT` next to Blazor on `HTTP_PORT`, because there is no proxy in front of the API.
- Own compose has no MinIO service (storage is external in production), so only `api` and `postgres` are gated on `backend=yes`.
- Aspire picked the stock Blazor template's `https` launch profile, so HTTPS redirection sent browsers to a random port. The AppHost uses the `http` profile instead of `--no-https`, which would also drop HSTS.
- `aspire publish` wrote `aspire.config.json` into the template root during verification, and later generations copied it. Publish now runs from the generated project, and the `all` scope ends with a template-unchanged check.
- Svelte-only guidance that leaked into `api` projects since the init options work (agents, skills, hooks, CONTRIBUTING, troubleshooting, README) is now marked `frontend=svelte`.

## Verification

- Stage 1: the 24 api-svelte and api projects were byte-identical to the previous engine's output, apart from random secrets and UserSecretsIds.
- `tools/init/verify-matrix.sh all`: 339 checks passed (template build, tests and SvelteKit checks; 9 API stack x auth full builds with tests and migrations; `blazor` in both deploy modes; all 38 combinations build their AppHost and pass `aspire publish` or `docker compose config`; the template is unchanged afterwards).
- Smoke tests:
  1. `api-blazor` under Aspire: the Blazor home page loads, and the named `api` client reached the API (`/health/live` 200 Healthy).
  2. `api-blazor` with own compose: `api`, `frontend` and `postgres` all became healthy.
  3. `blazor` under Aspire: the page loads, and the dashboard lists one resource (`frontend`).
  4. `blazor` with `aspire deploy`: the container runs and `/health/live` answers, but it reports no Docker health status, because Aspire's compose publisher emits no healthcheck for project resources (the API has the same gap).
  5. `api` (auth none) under Aspire: health and ready endpoints answer, `/` redirects to Scalar. `api-svelte`: the API answers, but the SvelteKit dev server cannot start on this Mac (Node 22.12 and pnpm 9.15 are below the project's requirements); its checks passed in a `node:22` container and its generated output is byte-identical to before.
  6. The template folder had no changes after generating.

## Follow-ups

- Aspire-generated compose has no healthchecks for project resources (Blazor and API).

- Local Node 22.12 is below the lockfile minimum; SvelteKit checks ran in a `node:22` container.
- C# doc comments in the WebApi still mention the SvelteKit proxy in `api` and `api-blazor` projects.
- `CONTRIBUTING.md` describes contributing to the template itself and ships in generated API projects.
