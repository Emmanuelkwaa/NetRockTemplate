# 2026-11-06 - Managed or external Keycloak for dev and prod

## Summary

With `--auth keycloak`, init now chooses where Keycloak runs separately for development and production:

| Flag | Values | Default |
|---|---|---|
| `--keycloak-dev` | `managed`, `external` | `external` |
| `--keycloak-prod` | `managed`, `external` | `external` |

Both default to `external`, so existing projects are unchanged apart from production realm settings becoming required. The flags are errors outside `--auth keycloak` (including `--stack blazor`). `--keycloak-authority`, `--keycloak-client-id` and `--keycloak-client-secret` are development-only and are errors with `--keycloak-dev managed`. Setup never asks for the production realm.

## How it works

- Derived condition keys `keycloak-dev`, `keycloak-prod` (`none` without Keycloak) and `keycloak-aspire` (`yes` when the AppHost hosts a Keycloak container). The `@init` syntax is unchanged.
- **Dev managed**: `Aspire.Hosting.Keycloak` 13.1.2-preview.1.26125.13, image pinned to `quay.io/keycloak/keycloak:26.4.7`, port base + 9, data volume, realm import. Init generates `keycloak/dev/realm.json`: realm and client named from the slug, confidential client, standard flow, PKCE S256, redirect URIs and web origins for the stack, the audience mapper and a test user from `--email` / `--password`. Client secret, admin password and test user credentials go to AppHost user secrets and reach the import through `${...}` placeholders. Authority and ClientId go to `appsettings.Development.json`.
- **Prod external**: own compose uses `${VAR:?message}` for the realm URL, client ID and secret; Aspire gets `keycloak-authority` and `keycloak-client-id` parameters with no default. `KeycloakOptions` validation stays as the backstop.
- **Prod managed**: a Keycloak service in production mode (`start --import-realm`) in own compose and the Aspire publish path. `KC_HOSTNAME`, `KC_HOSTNAME_BACKCHANNEL_DYNAMIC`, `KC_HTTP_ENABLED` and `KC_PROXY_HEADERS` assume a TLS-terminating proxy. `keycloak/prod/realm.json` has no users and reads its URLs and client secret from the environment. Managed PostgreSQL gets a `keycloak` database and login from `keycloak/postgres-init/01-keycloak.sh`; external PostgreSQL requires `KC_DB_URL` and `KC_DB_USERNAME`.
- **`Authentication:Keycloak:InternalUrl`** (new, optional): discovery, signing keys, token and logout calls go to the internal address while tokens are validated against `Authority`. HTTPS metadata is relaxed for this address only.

## Confirmed against the pinned versions

- `AddKeycloak` runs `start-dev --import-realm` in run mode and `start --import-realm` when publishing. Its default image tag `26.4` floats, so the template pins `26.4.7`.
- Keycloak 26.4.7 substitutes `${VAR}` placeholders in realm imports, and `--import-realm` skips an existing realm (`IGNORE_EXISTING`).
- With backchannel-dynamic hostnames, discovery fetched internally returns internal token and JWKS endpoints but the public issuer, authorization and logout endpoints, so the API rewrites the logout endpoint to `InternalUrl`.
- Aspire's compose publisher supports bind mounts but not container files, so publish mode uses `WithBindMount` / `WithInitBindMount`.
- `aspire deploy` without the production realm stops at `prepare-compose`, before any container starts, naming the parameter; interactive runs prompt for it. `aspire publish` succeeds and leaves `${KEYCLOAK_AUTHORITY}` placeholders, which the API's startup validation catches.
- `aspire deploy` stores parameter values, including secrets, in plain text in `~/.aspire/deployments/<hash>/production.json` and reuses them (documented).

## Fixes found along the way

- `.env.example` shipped a placeholder realm URL that satisfied `:?`; it is now empty.
- The API exited with code 0 after a fatal startup error; it now exits with 1.

## Verification

- Regression: the 38 existing combinations matched the previous engine apart from the intended Keycloak production changes (and random secrets, IDs and Blazor launch ports).
- `tools/init/verify-matrix.sh all`: 769 checks passed (template build, tests and SvelteKit checks; 18 full builds with tests and migrations; `blazor` in both deploy modes; all 74 combinations build their AppHost and pass `aspire publish` or `docker compose config` with values supplied; Keycloak secrets, realm and missing-realm negative checks; template unchanged afterwards).
- Smoke tests:
  1. Dev managed: test user sign-in, `/api/users/me`, refresh and logout (session ended at Keycloak) for `api-svelte` (through the SvelteKit proxy), `api-blazor` and `api`.
  2. Dev external against Emmanuel's real Keycloak: skipped at his request. The external flow itself is unchanged and was covered against a throwaway Keycloak when it was built.
  3. Prod managed, own compose: fresh stack healthy, sign-in, then `down`/`up`: realm import skipped, user still signs in.
  4. Prod managed, `aspire deploy`: sign-in, redeploy and container restart: realm import skipped, user still signs in.
  5. Prod managed, external PostgreSQL: Keycloak on its own database (89 tables, realm present, none in the app database), sign-in works.
  6. Prod external, realm missing: own compose and `aspire deploy` refuse to start; both deploy healthy once set. The API exits at startup with an options validation message when the values are empty.
  7. Template unchanged afterwards.

## Follow-ups

- An `http://` realm URL outside Development fails on every request (including health checks) instead of at startup.
- Aspire-published compose has no healthchecks for project resources, and publishes Keycloak's management port (9000) on an ephemeral host port.
- The Mac ran out of disk space during testing (Docker's disk image was 67 GB); pruning Docker build cache freed it.
