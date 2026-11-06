# Keycloak

Keycloak is this project's identity provider. Users, passwords, MFA and social login are managed in Keycloak; the project only signs users in and validates their tokens.

Init chose where Keycloak runs, separately for development and production:

| | Where Keycloak runs |
|---|---|
<!-- @init:if keycloak-dev=managed -->
| **Development** | Managed: Aspire runs Keycloak 26.4.7 with the realm in `keycloak/dev/realm.json`, including a test user. See [Managed Keycloak in development](#managed-keycloak-in-development). |
<!-- @init:else -->
| **Development** | External: an existing Keycloak server. Create the client as described in [Create the client](#create-the-client). |
<!-- @init:end -->
<!-- @init:if keycloak-prod=managed -->
| **Production** | Managed: the production stack runs Keycloak 26.4.7 in production mode with the realm in `keycloak/prod/realm.json` (no users). See [docs/deployment.md](deployment.md). |
<!-- @init:else -->
| **Production** | External: an existing Keycloak server. The realm URL, client ID and client secret are required at deployment; deployment stops with an error until they are set. |
<!-- @init:end -->

## How it works

- The API validates Keycloak access tokens as JWT bearer tokens, using the realm's discovery metadata and signing keys. Tokens are read from the `Authorization: Bearer` header first, then from the `__Secure-ACCESS-TOKEN` HttpOnly cookie.
- Browsers sign in with the authorization code flow, PKCE (S256) and a `state` value:

| Endpoint | Behavior |
|---|---|
| `GET /api/auth/keycloak/login?returnUrl=/path` | Redirects to Keycloak. The state, PKCE verifier and return URL travel in a short-lived, encrypted HttpOnly cookie. `returnUrl` must be a local path. |
| `GET /api/auth/keycloak/callback` | Checks the state, exchanges the code (with the client secret and verifier), sets the access and refresh cookies, and redirects to `returnUrl`. |
| `POST /api/auth/refresh?useCookies=true` | Refreshes with Keycloak and rotates the cookies (204). Without `useCookies`, send `{ "refreshToken": "..." }` and get new tokens back. |
| `POST /api/auth/logout` | Ends the Keycloak session (back-channel logout with the refresh token) and clears the cookies. |
| `GET /api/users/me` | The current user from token claims: `sub`, `preferred_username`, `email`, `given_name`, `family_name`. |

- Mobile apps and other backends can skip these endpoints and send Keycloak access tokens directly.

<!-- @init:if keycloak-dev=managed -->
## Managed Keycloak in development

`dotnet run --project src/backend/NetRockTemplate.AppHost` starts Keycloak on port 13009 (base port + 9) next to the API. The API waits for it.

- **Realm and client**: realm and client are both named `net-rock-template`. The client is confidential, uses the standard flow with PKCE S256, has the redirect URI and web origin for this project, and has the audience mapper the API needs.
- **Test user**: signs in with the email and password you gave init. Its password is in AppHost user secrets (`Parameters:keycloak-test-user-password`). It exists only in the development realm.
- **Admin console**: `http://localhost:13009/admin`, user `admin`, password in `Parameters:keycloak-admin-password`.
- **Secrets**: `keycloak/dev/realm.json` contains `${KC_CLIENT_SECRET}`, `${KC_TEST_USER_EMAIL}` and `${KC_TEST_USER_PASSWORD}` placeholders. The AppHost passes the values from user secrets as environment variables, and Keycloak fills them in when it imports the realm. No secret is stored in a tracked file.
- **Data**: the realm is imported once into the `net-rock-template-keycloak-data` volume. To apply changes to `realm.json`, stop the AppHost and remove that volume.

`Authentication:Keycloak:Authority` and `ClientId` for this realm are in `appsettings.Development.json`.

<!-- @init:end -->
## Create the client

<!-- @init:if keycloak-dev=managed keycloak-prod=managed -->
Both environments create this client from the generated realm files. Use these steps only if you move to an existing Keycloak server.

<!-- @init:end -->
In the Keycloak admin console, in your realm:

1. **Clients > Create client**, type OpenID Connect. Pick a client ID, for example `my-app`.
2. **Capability config**: turn **Client authentication** on (confidential client) and keep **Standard flow** on. Direct access grants can stay off.
3. **Login settings**:
   - **Valid redirect URIs**: `{base URL}/api/auth/keycloak/callback`
   - **Web origins**: `{base URL}`
<!-- @init:if frontend=svelte -->
   - The base URL is the frontend's URL, for example `http://localhost:13000` in development and `https://app.example.com` in production. The SvelteKit frontend proxies `/api/*` to the API. It is the value of `Email:FrontendBaseUrl`.
<!-- @init:end -->
<!-- @init:if frontend=blazor -->
   - The base URL is the API's own URL: `http://localhost:13002` in development and `API_PUBLIC_URL` (or the `api-public-url` parameter) in production. The Blazor frontend has no proxy in front of the API, so the sign-in flow runs on the API, the same as in an API-only project. It is the value of `Authentication:Keycloak:CallbackBaseUrl`.
   - **Web origins** is the Blazor app's URL (`http://localhost:13000` in development).
<!-- @init:end -->
<!-- @init:if frontend=none -->
   - The base URL is the API's URL, for example `http://localhost:13002` in development. It is the value of `Email:FrontendBaseUrl`.
<!-- @init:end -->
4. **Advanced > Proof Key for Code Exchange Code Challenge Method**: `S256`.
5. **Credentials**: copy the client secret.
6. **Audience mapper**. Keycloak puts `account` in the access token's `aud` claim by default, and the API rejects tokens whose audience is not its client ID. Open **Client scopes > my-app-dedicated > Add mapper > By configuration > Audience**, set **Included Client Audience** to your client ID, and turn **Add to access token** on.

The API also requires `sub` to be a UUID, which is true for normal Keycloak users.

## Configure the project

| Setting | Where |
|---|---|
| `Authentication:Keycloak:Authority` | `appsettings.json`. The realm URL, for example `https://auth.example.com/realms/my-realm`. It must match the token's `iss` claim exactly, so use the URL Keycloak itself advertises (check `{realm URL}/.well-known/openid-configuration`). |
| `Authentication:Keycloak:ClientId` | `appsettings.json` |
| `Authentication:Keycloak:ClientSecret` | Never in a file. Development: AppHost user secrets. Production: environment variable `Authentication__Keycloak__ClientSecret`. |
| `Authentication:Keycloak:Audience` | Optional. Defaults to the client ID. |
<!-- @init:if keycloak-prod=managed -->
| `Authentication:Keycloak:InternalUrl` | Optional. A realm URL the API uses on an internal network (discovery, signing keys, token and logout calls) when the public `Authority` is not reachable from the API container. Tokens are still validated against `Authority`. Plain HTTP is allowed for this address only. Keycloak must run with `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`. The managed production stack sets it to `http://keycloak:8080/realms/net-rock-template`. |
<!-- @init:else -->
| `Authentication:Keycloak:InternalUrl` | Optional. A realm URL the API uses on an internal network (discovery, signing keys, token and logout calls) when the public `Authority` is not reachable from the API container. Tokens are still validated against `Authority`. Plain HTTP is allowed for this address only. Keycloak must run with `KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true`. |
<!-- @init:end -->
| `Authentication:Keycloak:CallbackBaseUrl` | Optional. The public base URL Keycloak redirects back to. Defaults to `Email:FrontendBaseUrl`. Set it when browsers reach the API directly instead of through a frontend proxy. |

Store the development secret:

```bash
dotnet user-secrets set Parameters:keycloak-client-secret "<secret>" --project src/backend/NetRockTemplate.AppHost
```

The AppHost passes it to the API as `Authentication__Keycloak__ClientSecret`.

Outside Development, `Authority` must use `https`. An `http://` realm URL stops the API at startup with a validation error naming `Authentication:Keycloak:Authority`. Only `InternalUrl` may use plain `http`, because it is an internal network address.

## Troubleshooting

| Symptom | Cause |
|---|---|
| 401 on every request with a valid login | Missing audience mapper, or `Authority` differs from the token's `iss` (trailing slash, hostname, or `http` vs `https`). |
| Keycloak shows "Invalid parameter: redirect_uri" | The callback URL is not in **Valid redirect URIs**, or `Email:FrontendBaseUrl` (or `CallbackBaseUrl`) is wrong. |
| Callback returns 400 "sign-in request expired" | The login took longer than `LoginStateLifetime` (10 minutes), or the browser blocked the login-state cookie. |
| API fails at startup with an options validation error | `Authority`, `ClientId` or `ClientSecret` is empty, or `Authority` uses `http://` outside Development. |
