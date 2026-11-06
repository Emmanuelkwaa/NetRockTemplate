# 2026-11-06 - Keycloak follow-ups: management port and http realm URLs

Two follow-ups from [the Keycloak modes session](2026-11-06-keycloak-modes.md).

## Keycloak's management port in Aspire-generated compose

- **Problem**: for `--keycloak-prod managed --deploy aspire`, `aspire publish` wrote `- "9000"` under Keycloak's `ports`, so Docker published the management interface (health) on an ephemeral host port.
- **Fix**: the AppHost's publish path calls `PublishAsDockerComposeService` on the Keycloak resource, removes the 9000 port mapping and adds `expose: ["9000"]`. The port stays reachable on the compose network. Run mode is unchanged.
- **Check**: `verify-matrix.sh` fails if any Aspire-generated compose publishes a container's port 9000 to the host.

## Plain http:// realm URLs outside Development

- **Problem**: an `http://` `Authentication:Keycloak:Authority` outside Development passed options validation. JwtBearer then threw "The MetadataAddress or Authority must use HTTPS" on every request, `/health/live` and `/health/ready` included, while the process kept running (the container stayed up as unhealthy rather than exiting).
- **Fix**: `KeycloakEnvironmentOptionsValidator` runs at startup (`ValidateOnStart`) and fails outside Development when `Authority` uses `http`, with a message naming `Authentication:Keycloak:Authority`. `InternalUrl` may still use `http`, so the managed production path (https public URL, `http://keycloak:8080` internal address) keeps working.
- **Tests**: validator cases for Development with http, Production, Staging and Testing with http, Production with https, and Production with an http `InternalUrl`; startup cases through `IStartupValidator`; an API test that the host fails to start with an http `Authority` and starts with an http `InternalUrl`.
- **Impact**: the earlier managed production smoke tests used an `http://localhost` public Keycloak URL. That now stops at startup, as it should; local tests need a TLS-terminating proxy in front of Keycloak, matching the documented production setup.

## Also found

- `aspire deploy` saves parameter values in `~/.aspire/deployments/<hash>/production.json`, and on later deploys the saved values take priority over `Parameters__*` environment variables. A redeploy with a changed public URL silently kept the old one. `--clear-cache` deploys with the supplied values; `docs/deployment.md` now says so.
- `verify-matrix.sh all` ended by requiring a clean working tree, so uncommitted edits made before a run failed the final check. It now compares against the tree as it was when the run started.

## Verification

- Reproduced both problems before the fix: `- "9000"` in the published Keycloak service, and `/health/live`, `/health/ready` and every other request returning 500 with an `http://` realm URL in Production.
- Generated Keycloak project tests: all passed (12 new component tests, 2 new API tests).
- `tools/init/verify-matrix.sh all`: 805 of 806 checks passed, including 37 new `no published management port` checks. The one failure was the final clean-tree check, tripped by this note's uncommitted edits made before the run (the before/after template check passed); the check was fixed as described above.
- `aspire deploy` for `--keycloak-prod managed`:
  - With an `http://` public Keycloak URL, the API container exits with code 1 and "Authentication:Keycloak:Authority must use https outside Development (environment: Production)".
  - Behind a local TLS-terminating proxy (Caddy, `https://localhost:18443`): issuer `https://localhost:18443/realms/asp-kc`, `InternalUrl` `http://keycloak:8080/...`, and sign-in, `/api/users/me`, refresh and logout all work (session ended at Keycloak).
  - Keycloak publishes only port 8080 on the host; `/health/ready` on port 9000 answers `UP` from inside the compose network.
