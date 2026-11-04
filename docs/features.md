# Features

> Back to [README](../README.md)

## Backend - .NET 10 / C# 13

| Feature | Implementation |
|---|---|
| **Clean Architecture** | Domain, Application, Infrastructure, WebApi - with [architecture tests](../src/backend/tests/NetRockTemplate.Architecture.Tests) enforcing dependency rules at build time |
<!-- @init:if auth=jwt -->
| **Authentication** | JWT in HttpOnly cookies, refresh token rotation with reuse detection (stolen token revokes entire family), security stamp validation, remember-me persistent sessions |
| **Two-Factor Authentication** | TOTP-based 2FA with QR code setup, 6-digit verification, 8 single-use recovery codes, challenge token flow (login returns challenge, client submits TOTP code), admin can disable 2FA for locked-out users with automatic session revocation and notification email |
| **OAuth / External Login** | OAuth 2.0 / OIDC external authentication with 8 providers out of the box - Google, GitHub, Discord, Apple, Microsoft, Facebook, LinkedIn, and X (Twitter). Manual `HttpClient`-based code exchange (no ASP.NET middleware). Auto-links accounts by verified email. 2FA is bypassed for OAuth logins (provider already verified identity). Provider credentials stored with AES-256-GCM encryption |
| **OAuth Admin Management** | Admins configure OAuth providers entirely from the UI - enable/disable providers, set client ID and secret, configure scopes and endpoints, test connection with a single click. No redeploy required. Credentials encrypted at rest with AES-256-GCM |
| **Authorization** | Permission-based with custom roles - atomic permissions (`users.view`, `roles.manage`, ...) assigned per role, enforced via `[RequirePermission]` attribute. Superuser has implicit full access |
| **Role Hierarchy** | Superuser > Admin > User - privilege escalation prevention, self-protection rules, system role guards. Custom roles with arbitrary permission sets |
| **Rate Limiting** | Global + per-endpoint policies (registration, auth, sensitive operations, admin mutations), configurable fixed-window with IP and user partitioning |
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
| **Authentication** | An existing Keycloak server is the identity provider. The API validates Keycloak access tokens (bearer header or HttpOnly cookie), signs users in with the authorization code flow and PKCE (`GET /api/auth/keycloak/login` and `/callback`), and refreshes or ends the Keycloak session through `POST /api/auth/refresh` and `/api/auth/logout`. `GET /api/users/me` returns the user from token claims. Setup: [Keycloak](keycloak.md) |
| **Authorization** | `[Authorize]` on every `ApiController` endpoint. Roles, permissions and user management live in Keycloak - add policies for your own features as needed |
| **Rate Limiting** | Global limiter + a stricter IP-partitioned policy on the auth endpoints, configurable fixed-window |
<!-- @init:end -->
<!-- @init:if auth=none -->
| **Authentication** | None - the API has no users, no sign-in and no `/api/auth/*` or `/api/users/*` endpoints. Every endpoint is anonymous |
| **Rate Limiting** | Global fixed-window limiter partitioned by client IP, configurable per environment |
<!-- @init:end -->
<!-- @init:if frontend=yes -->
| **Validation** | FluentValidation + Data Annotations, flowing constraints into OpenAPI spec and generated TypeScript types |
<!-- @init:else -->
| **Validation** | FluentValidation + Data Annotations, flowing constraints into the OpenAPI spec |
<!-- @init:end -->
<!-- @init:if auth=jwt -->
| **Caching** | HybridCache (in-process L1) with auto-invalidation via EF Core interceptor, stampede protection, key management |
<!-- @init:else -->
| **Caching** | HybridCache (in-process L1) with stampede protection, ready for your own features |
<!-- @init:end -->
<!-- @init:if auth=jwt,keycloak -->
| **Database** | PostgreSQL + EF Core with soft delete, full audit trail (created/updated/deleted by + at), global query filters |
<!-- @init:else -->
| **Database** | PostgreSQL + EF Core with soft delete, created/updated/deleted timestamps, global query filters |
<!-- @init:end -->
<!-- @init:if auth=jwt -->
| **Audit Trail** | Append-only audit events table with JSONB metadata, 25+ action constants covering auth, account, admin, role, and OAuth operations. Admin per-user view and user self-activity log |
<!-- @init:end -->
| **API Documentation** | OpenAPI spec + Scalar UI, with custom transformers for enums, nullable types, numeric schemas, and camelCase query params |
| **Error Handling** | Result pattern for business logic, `ProblemDetails` ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)) on every error response, structured error messages |
| **Logging** | Serilog bridged to OpenTelemetry with structured request logging, correlation, and Aspire Dashboard |
<!-- @init:if auth=jwt -->
| **Account Management** | Registration with CAPTCHA, login/logout, remember me, email verification, password reset, profile updates with avatar upload, account deletion, connected OAuth accounts management, set password for OAuth-only users |
| **Admin Panel API** | User CRUD with search/pagination, custom role management with permission editor, role assignment with hierarchy enforcement, background job management, OAuth provider configuration, 2FA management |
| **Background Jobs** | Hangfire with PostgreSQL persistence - recurring jobs via `IRecurringJobDefinition`, fire-and-forget, admin dashboard with trigger/pause/resume/restore, persistent pause state |
| **Email** | MailKit SMTP with configurable enable/disable toggle, Fluid (Liquid) template engine with shared HTML base layout. Templates for verification, password reset, admin-initiated reset, invitation, and 2FA disable notification. MailPit integration for local email testing via Aspire |
| **File Storage** | S3-compatible blob storage via `IFileStorageService` - MinIO locally, any S3 provider in production (AWS S3, Cloudflare R2, DigitalOcean Spaces, Backblaze B2). Avatar upload with SkiaSharp image processing (resize to 512x512, WebP compression) |
| **PII Compliance** | `users.view_pii` permission gates personal data visibility. Emails masked to `j***@g***.com`, phone numbers to `***`. Self-view exemption. No PII in logs, URLs, or error responses |
<!-- @init:else -->
| **Background Jobs** | Hangfire with PostgreSQL persistence - recurring jobs via `IRecurringJobDefinition`, fire-and-forget, persistent pause state. Hangfire dashboard at `/hangfire` in Development only |
| **Email** | MailKit SMTP with configurable enable/disable toggle, Fluid (Liquid) template engine with shared HTML base layout (`_base.liquid`) - add templates for your own features. MailPit integration for local email testing via Aspire |
| **File Storage** | S3-compatible blob storage via `IFileStorageService` - MinIO locally, any S3 provider in production (AWS S3, Cloudflare R2, DigitalOcean Spaces, Backblaze B2) |
<!-- @init:end -->
| **Health Checks** | `/health` (all), `/health/ready` (DB + S3), `/health/live` (liveness) - Docker healthcheck integration |
<!-- @init:if auth=jwt -->
| **Search** | User lookup with search and pagination, PostgreSQL trigram similarity function pre-registered for custom use |
| **Testing** | 4 test projects - unit, component (mocked services), API integration (WebApplicationFactory), architecture enforcement. 1000+ tests covering auth flows, 2FA, OAuth, permissions, rate limiting, and response contracts |
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
| **Search** | PostgreSQL trigram similarity function pre-registered for custom use |
| **Testing** | 4 test projects - unit, component (mocked services), API integration (WebApplicationFactory), architecture enforcement. Keycloak sign-in, refresh and token validation tested against an in-process mock issuer |
<!-- @init:end -->
<!-- @init:if auth=none -->
| **Search** | PostgreSQL trigram similarity function pre-registered for custom use |
| **Testing** | 4 test projects - unit, component (mocked services), API integration (WebApplicationFactory), architecture enforcement |
<!-- @init:end -->

<!-- @init:if frontend=yes -->
## Frontend - SvelteKit / Svelte 5

| Feature | Implementation |
|---|---|
| **Svelte 5 Runes** | Modern reactivity with `$state`, `$derived`, `$effect` - no legacy stores or `export let` |
| **Type-Safe API Client** | Generated from OpenAPI spec via `openapi-typescript` - backend changes break the build, not the user |
| **shadcn-svelte UI** | Full component library built on bits-ui headless primitives - Button, Card, Dialog, Table, Sidebar, Command, Breadcrumb, InputOTP, Tooltip, and more. CSS variable theming with Tailwind CSS 4 |
<!-- @init:if auth=jwt -->
| **Command Palette** | Cmd+K (Mac) / Ctrl+K (Windows/Linux) command palette for quick navigation. Permission-gated - users only see pages they can access. Keyboard shortcut help dialog (Shift+?) |
| **OAuth Login** | "Sign in with" buttons for all configured providers, automatic redirect flow, connected accounts management in user settings, provider-specific icons |
| **Two-Factor Authentication UI** | QR code TOTP setup wizard, shadcn InputOTP with auto-submit, recovery codes display with copy-to-clipboard, disable confirmation dialog |
<!-- @init:else -->
| **Command Palette** | Cmd+K (Mac) / Ctrl+K (Windows/Linux) command palette for quick navigation. Keyboard shortcut help dialog (Shift+?) |
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
| **Keycloak Sign-in** | Login hands off to the Keycloak hosted login page through the API, then returns to the app with the session in HttpOnly cookies |
<!-- @init:end -->
<!-- @init:if auth=jwt,keycloak -->
| **Automatic Token Refresh** | 401 interceptor with refresh and retry, transparent to components, thundering-herd protection |
<!-- @init:end -->
| **BFF Architecture** | Server-side API proxy handles cookies, CSRF validation, header filtering, and `X-Forwarded-For` propagation |
| **i18n** | Paraglide JS - type-safe keys, compile-time validation, SSR-compatible, auto-detection via Accept-Language. Ships with English and Czech |
| **Security Headers** | CSP with nonce mode, HSTS, X-Frame-Options, Referrer-Policy, Permissions-Policy on every response |
<!-- @init:if auth=jwt -->
| **Permission Guards** | Layout-level + page-level route guards, per-permission nav item filtering, component-level conditional rendering |
<!-- @init:end -->
| **Dark Mode** | Light/dark/system theme with localStorage persistence, FOUC prevention, CSS variable theming |
| **Responsive Design** | Mobile-first with sidebar drawer, desktop content header with breadcrumbs, 44px touch targets on all interactive elements, logical CSS properties for RTL readiness, safe-area insets for notched devices |
<!-- @init:if auth=jwt -->
| **Loading States** | Skeleton components and loading spinners for async content throughout the admin panel |
<!-- @init:else -->
| **Loading States** | Skeleton components and loading spinners for async content |
<!-- @init:end -->
| **Error Handling** | Unified mutation error handler - rate limiting with visible cooldown timers, field-level validation with shake animations, generic errors with toast notifications |
<!-- @init:if auth=jwt -->
| **Admin UI** | User table with search/pagination and PII masking, role card grid with permission editor, job dashboard with execution history, OAuth provider management with toggle switches and test connection, 2FA management |
| **Avatar Upload** | Drag-and-drop with client-side crop tool, preview, SkiaSharp server-side compression to WebP, S3 storage |
| **Auth Pages** | Login with API health indicator, registration with form draft persistence, forgot/reset password flows, email verification - all with CAPTCHA integration and smooth transitions |
<!-- @init:end -->

<!-- @init:end -->
## Infrastructure & DevOps

| Feature | Implementation |
|---|---|
<!-- @init:if frontend=yes -->
| **Aspire Local Dev** | One `dotnet run` for the full stack - API, frontend (hot-reload), PostgreSQL, MinIO (S3 storage), MailPit (email testing), OpenTelemetry Dashboard with traces, logs, and metrics |
| **Production Docker** | Multi-stage Dockerfiles for API (.NET) and frontend (Node), dev config stripping, non-root users, health probe binary. Deploy to any platform - Docker Compose, Coolify, Railway, Kubernetes |
<!-- @init:else -->
| **Aspire Local Dev** | One `dotnet run` for the full stack - API, PostgreSQL, MinIO (S3 storage), MailPit (email testing), OpenTelemetry Dashboard with traces, logs, and metrics |
| **Production Docker** | Multi-stage Dockerfile for the API (.NET), dev config stripping, non-root user, health probe binary. Deploy to any platform - Docker Compose, Coolify, Railway, Kubernetes |
<!-- @init:end -->
| **Init Script** | Interactive project bootstrapping - renames solution, configures ports, creates migration, launches Aspire. Works on macOS, Linux, and Windows |
| **CI Pipeline** | GitHub Actions with smart path filtering - backend-only PRs skip frontend checks and vice versa. Coverage reports with ReportGenerator |
| **Docker Validation** | CI validates image builds on Dockerfile/dependency changes, with layer caching |
| **Dependabot** | Weekly NuGet, npm, and GitHub Actions updates with grouped minor+patch PRs |
| **Environment Config** | `appsettings.json` defines all options, `appsettings.Development.json` provides working dev defaults, env vars override for production |
| **Production Hardening** | Dev config stripping from production images, reverse proxy trust configuration, CORS startup safeguard (crashes on `AllowAllOrigins=true` in production) |
<!-- @init:if frontend=yes -->
| **Claude Code Skills** | 20+ native skills for development workflows - feature scaffolding, endpoint creation, PR management, design review, type generation. 12 specialized agents (engineers, reviewers, UX designer, product owner, tech writer), lifecycle hooks, and context files (`CLAUDE.md`, `FILEMAP.md`) for deep codebase understanding |
<!-- @init:else -->
| **Claude Code Skills** | Native skills for development workflows - feature scaffolding, endpoint creation, PR management. 8 specialized agents (engineers, reviewers, product owner, tech writer), lifecycle hooks, and context files (`CLAUDE.md`, `FILEMAP.md`) for deep codebase understanding |
<!-- @init:end -->

<!-- @init:if frontend=yes -->
## What Your Users Get

NETrock is not just a developer tool - the included frontend delivers features that end users of your product interact with directly:

<!-- @init:if auth=jwt -->
- **Sign in with their existing accounts** - Google, GitHub, Discord, Apple, Microsoft, Facebook, LinkedIn, X. Admins enable providers from the UI without any redeploy
- **Two-factor authentication** - TOTP setup with QR code, recovery codes for account recovery. Admins can disable 2FA for locked-out users
- **Profile management** - Avatar upload with image cropping, profile editing, connected OAuth accounts management, password management
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
- **Single sign-on** - Users sign in on your Keycloak realm's login page, with whatever methods the realm enables
<!-- @init:end -->
- **Dark mode and language switching** - Light/dark/system theme, language auto-detection with manual override
- **Keyboard-driven navigation** - Cmd+K command palette, global keyboard shortcuts, help dialog
- **Mobile-ready** - Responsive design with touch-friendly targets, safe-area support for notched devices, sidebar drawer on mobile
<!-- @init:if auth=jwt -->
- **Security they can trust** - CAPTCHA on registration, visible rate limiting feedback, email verification, password reset flows
<!-- @init:else -->
- **Clear feedback** - Visible rate limiting feedback with countdown timers instead of silent failures
<!-- @init:end -->
<!-- @init:end -->
