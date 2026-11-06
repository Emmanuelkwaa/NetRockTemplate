# Keycloak

This project uses an existing Keycloak server as its identity provider. The project only connects to it: there is no Keycloak container and no realm import. Users, passwords, MFA and social login are managed in Keycloak.

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

## Create the client

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
| `Authentication:Keycloak:CallbackBaseUrl` | Optional. The public base URL Keycloak redirects back to. Defaults to `Email:FrontendBaseUrl`. Set it when browsers reach the API directly instead of through a frontend proxy. |

Store the development secret:

```bash
dotnet user-secrets set Parameters:keycloak-client-secret "<secret>" --project src/backend/NetRockTemplate.AppHost
```

The AppHost passes it to the API as `Authentication__Keycloak__ClientSecret`. HTTPS metadata is required outside Development.

## Troubleshooting

| Symptom | Cause |
|---|---|
| 401 on every request with a valid login | Missing audience mapper, or `Authority` differs from the token's `iss` (trailing slash, hostname, or `http` vs `https`). |
| Keycloak shows "Invalid parameter: redirect_uri" | The callback URL is not in **Valid redirect URIs**, or `Email:FrontendBaseUrl` (or `CallbackBaseUrl`) is wrong. |
| Callback returns 400 "sign-in request expired" | The login took longer than `LoginStateLifetime` (10 minutes), or the browser blocked the login-state cookie. |
| API fails at startup with an options validation error | `Authority`, `ClientId` or `ClientSecret` is empty. |
