# Before You Ship

> Back to [README](../README.md)

NETrock works out of the box for local development, but there are things you need to configure before going to production. This checklist covers what the template **can't decide for you**.

> **Environment variables** are the primary configuration mechanism. Set them on your API and frontend containers however your platform supports it (env files, UI, secrets manager, etc.). The checklist below lists what needs configuring.

## Must Do

- [ ] **Email service** - replace `NoOpEmailService` with a real provider (SMTP, SendGrid, Postmark, etc.). The NoOp service just logs emails to the console. Configure via `Email__Smtp__*` env vars or swap the service registration in `ServiceCollectionExtensions.cs`
- [ ] **CORS origins** - set `Cors__AllowedOrigins__0` to your production domain (add `__1`, `__2` for additional origins). The app **will refuse to start** if `AllowAllOrigins` is `true` outside of Development - this is intentional
<!-- @init:if auth=jwt -->
- [ ] **JWT secret** - the init script generates a random 64-char key in `appsettings.json`. For production, set `Authentication__Jwt__Key` as an environment variable (minimum 32 chars, cryptographically random)
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
- [ ] **Keycloak** - set `Authentication__Keycloak__ClientSecret` as an environment variable, check `Authority` and `ClientId` in `appsettings.json`, and register `{public URL}/api/auth/keycloak/callback` on the client. See [Keycloak](keycloak.md)
<!-- @init:end -->
<!-- @init:if auth=none -->
- [ ] **Access control** - the API has no authentication. Keep it on a private network or behind a gateway that authenticates callers
<!-- @init:end -->
- [ ] **Database** - point `ConnectionStrings__Database` to your production PostgreSQL instance. See [Deployment](deployment.md#database-migrations) for how migrations run
<!-- @init:if auth=jwt -->
- [ ] **CAPTCHA keys** - replace the Cloudflare Turnstile development keys with production keys (`Captcha__SecretKey` backend, `TURNSTILE_SITE_KEY` frontend - runtime-configurable via the `(public)` layout server load)
<!-- @init:end -->
<!-- @init:if auth=jwt -->
- [ ] **Frontend URL in emails** - set `Email__FrontendBaseUrl` to your production domain so email verification and password reset links work
<!-- @init:else -->
- [ ] **Public URL** - set `Email__FrontendBaseUrl` to your production domain. It is used for links in emails.
<!-- @init:end -->
<!-- @init:if auth=jwt -->
- [ ] **Bootstrap admin** - set `Seed__Users__0__Email`, `Seed__Users__0__Password`, and `Seed__Users__0__Role=Superuser` environment variables to create an initial Superuser on first deploy. Idempotent - safe to leave set, but remove after creating admin accounts through the UI
<!-- @init:end -->
- [ ] **File storage** - configure `FileStorage__*` env vars for your S3-compatible provider. Local dev uses MinIO (included in Aspire). For production, point to your preferred provider - AWS S3, Cloudflare R2, DigitalOcean Spaces, Backblaze B2, or any S3-compatible service. Set `FileStorage__Endpoint`, `FileStorage__AccessKey`, `FileStorage__SecretKey`, `FileStorage__BucketName`, `FileStorage__Region` (if applicable), and `FileStorage__UseSSL=true`. Use `/manage-file-storage` skill for provider-specific configs or to remove file storage entirely
<!-- @init:if auth=jwt -->
- [ ] **OAuth encryption key** - set `Authentication__ExternalProviders__EncryptionKey` to a cryptographically random base64 string (32+ bytes). This key encrypts OAuth provider client secrets at rest with AES-256-GCM. Generate with `openssl rand -base64 32`. Without this, OAuth provider management will not work in production
<!-- @init:end -->
<!-- @init:if auth=jwt -->
- [ ] **OAuth providers** - configure OAuth providers from the admin UI after first deploy. Each provider needs a client ID and secret from the provider's developer console (Google Cloud Console, GitHub Developer Settings, etc.). Admins can enable/disable providers, test connections, and update credentials without redeploying
<!-- @init:end -->

## Should Do

<!-- @init:if frontend=svelte -->
- [ ] **TLS termination** - the containers expose API (8080) and frontend (3000) as plain HTTP. Put a reverse proxy (nginx, Caddy, Traefik) in front to terminate TLS, or use your platform's built-in TLS (Coolify, Railway, etc.). Set `ORIGIN=https://your-domain.com` in the frontend env so SvelteKit generates correct URLs
<!-- @init:else -->
- [ ] **TLS termination** - the API container exposes port 8080 as plain HTTP. Put a reverse proxy (nginx, Caddy, Traefik) in front to terminate TLS, or use your platform's built-in TLS (Coolify, Railway, etc.)
<!-- @init:end -->
- [ ] **Reverse proxy** - if behind nginx/load balancer, configure `Hosting__ReverseProxy__TrustedNetworks` and `TrustedProxies` so rate limiting uses real client IPs
- [ ] **Logging** - logs flow via OpenTelemetry. Set `OTEL_EXPORTER_OTLP_ENDPOINT` for your production collector (Grafana, Datadog, etc.). Locally, logs are visible in the Aspire Dashboard. Adjust log levels (`Serilog__MinimumLevel__Default=Information`)
- [ ] **Rate limits** - review the production defaults in `appsettings.json` and adjust for your expected traffic
- [ ] **Backups** - set up automated PostgreSQL backups. NETrock uses soft delete, but that doesn't replace real backups
- [ ] **Monitoring** - the health check endpoints (`/health`, `/health/ready`, `/health/live`) are ready for your uptime monitoring
- [ ] **Resource limits** - configure CPU/memory limits in your deployment platform. Recommended starting points: API 2 CPU / 1G, frontend 1 CPU / 512M, PostgreSQL 1 CPU / 1G. PostgreSQL alone typically wants 25% of available memory for `shared_buffers`

## Good to Know

<!-- @init:if auth=jwt -->
- **Hangfire dashboard** is automatically disabled in production. Use the admin API endpoints (`/api/admin/jobs/*`) instead
<!-- @init:else -->
- **Hangfire dashboard** is only available in Development, at `/hangfire`
<!-- @init:end -->
- **HTTPS** is forced in production via `Hosting__ForceHttps=true` (default). Development runs on HTTP
- **Dev config is stripped** from production Docker images - `appsettings.Development.json` and `appsettings.Testing.json` are removed at build time
- **CORS startup guard** will crash the app on purpose if you deploy with `AllowAllOrigins=true` - this is a security feature, not a bug
