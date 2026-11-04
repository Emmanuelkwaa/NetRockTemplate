# Architecture

> Back to [README](../README.md)

```
<!-- @init:if frontend=yes -->
Frontend (SvelteKit :5173)
    |
    |  /api/* proxy (catch-all server route)
    |  Forwards cookies + headers, validates CSRF origin
    v
<!-- @init:else -->
Clients (web, mobile, other services)
    |
    v
<!-- @init:end -->
Backend API (.NET :8080)
    |
    |  Clean Architecture
    |  WebApi -> Application <- Infrastructure -> Domain (+Shared)
    |
<!-- @init:if auth=jwt -->
    |-- PostgreSQL           - EF Core, soft delete, audit trails, Hangfire storage
    |-- MinIO                - S3-compatible blob storage (avatars, file uploads)
    |-- Hangfire             - Recurring + fire-and-forget background jobs
    |-- OAuth Providers      - Google, GitHub, Discord, Apple, Microsoft, Facebook, LinkedIn, X
<!-- @init:else -->
    |-- PostgreSQL           - EF Core, soft delete, audit fields, Hangfire storage
    |-- MinIO                - S3-compatible blob storage (file uploads)
    |-- Hangfire             - Recurring + fire-and-forget background jobs
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
    |-- Keycloak (external)  - Identity provider: sign-in, token refresh, signing keys
<!-- @init:end -->
    |-- SMTP (MailKit)       - Transactional email with Fluid templates
    +-- OpenTelemetry -------> Aspire Dashboard (local) / OTLP endpoint (production)
```

The backend follows Clean Architecture with **architecture tests** that enforce dependency direction at build time - Domain and Shared have zero dependencies, Application only references Domain and Shared, Infrastructure never references WebApi. Breaking these rules fails the build.

<!-- @init:if auth=keycloak -->
Authentication is delegated to an existing Keycloak server. See [Keycloak](keycloak.md) for the client setup and configuration.

<!-- @init:end -->
---

## Testing

NETrock is thoroughly tested across 4 test projects, covering every layer of the backend:

| Project | What it covers |
|---|---|
<!-- @init:if auth=jwt -->
| **Unit Tests** | Result pattern, error messages, phone normalization, base entity, roles, permissions |
| **Component Tests** | Auth service (login, register, refresh, token rotation, 2FA), admin service (hierarchy, role assignment, lock/delete), role management, user service, OAuth external auth |
| **API Tests** | Full HTTP pipeline (status codes, auth gates, ProblemDetails shape), all validators, response contract testing, permission enforcement, rate limiting, 2FA endpoints, OAuth endpoints |
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
| **Unit Tests** | Result pattern, error messages, phone normalization, base entity |
| **Component Tests** | Keycloak auth service (authorization request, code exchange, refresh, revoke), options validation, email sending, caching, repository |
| **API Tests** | Full HTTP pipeline (status codes, auth gates, ProblemDetails shape), Keycloak login/callback/refresh/logout, token validation against an in-process mock issuer, origin validation, CORS and rate limiting options |
<!-- @init:end -->
<!-- @init:if auth=none -->
| **Unit Tests** | Result pattern, error messages, phone normalization, base entity |
| **Component Tests** | Email sending, caching, repository, options validation |
| **API Tests** | HTTP pipeline middleware (origin validation), CORS and rate limiting options |
<!-- @init:end -->
| **Architecture Tests** | Layer dependency direction, naming conventions, access modifiers |

All tests run in-process - no Docker or PostgreSQL required:

```bash
dotnet test src/backend/NetRockTemplate.slnx -c Release
```

---

## Project Structure

```
src/
<!-- @init:if frontend=yes -->
|-- backend/                          # .NET 10 API (Clean Architecture)
<!-- @init:else -->
+-- backend/                          # .NET 10 API (Clean Architecture)
<!-- @init:end -->
|   |-- YourProject.Shared/           # Result pattern, error types, cross-cutting utilities
|   |-- YourProject.Domain/           # Entities with audit fields and soft delete
<!-- @init:if auth=jwt -->
|   |-- YourProject.Application/      # Interfaces, DTOs, service contracts, permissions
|   |-- YourProject.Infrastructure/   # EF Core, Identity, HybridCache, Hangfire, S3, email, OAuth
|   |-- YourProject.WebApi/           # Controllers, middleware, validation, authorization
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
|   |-- YourProject.Application/      # Interfaces, DTOs, service contracts
|   |-- YourProject.Infrastructure/   # EF Core, Keycloak token client, HybridCache, Hangfire, S3, email
|   |-- YourProject.WebApi/           # Controllers, middleware, validation
<!-- @init:end -->
<!-- @init:if auth=none -->
|   |-- YourProject.Application/      # Interfaces, DTOs, service contracts
|   |-- YourProject.Infrastructure/   # EF Core, HybridCache, Hangfire, S3, email
|   |-- YourProject.WebApi/           # Controllers, middleware, validation
<!-- @init:end -->
|   +-- tests/
<!-- @init:if auth=jwt -->
|       |-- YourProject.Unit.Tests/        # Pure logic tests (Result, entities, roles, permissions)
<!-- @init:else -->
|       |-- YourProject.Unit.Tests/        # Pure logic tests (Result, entities)
<!-- @init:end -->
|       |-- YourProject.Component.Tests/   # Service tests with mocked dependencies
<!-- @init:if auth=jwt -->
|       |-- YourProject.Api.Tests/         # HTTP integration tests + validator tests
<!-- @init:else -->
|       |-- YourProject.Api.Tests/         # HTTP integration tests
<!-- @init:end -->
|       +-- YourProject.Architecture.Tests/ # Dependency direction + naming enforcement
<!-- @init:if frontend=yes -->
|
+-- frontend/                         # SvelteKit frontend
    +-- src/
        |-- lib/
        |   |-- api/                  # Type-safe API client + generated OpenAPI types
        |   |-- components/           # Feature-organized with barrel exports
<!-- @init:if auth=jwt -->
        |   |   |-- admin/            # Admin components (tables, cards, editors, OAuth management)
        |   |   |-- auth/             # Login, register, CAPTCHA, 2FA, password reset
<!-- @init:end -->
        |   |   |-- common/           # Shared components (PageHeader, EmptyState, FieldError)
        |   |   |-- layout/           # Sidebar, header, breadcrumbs, command palette, theme, language
<!-- @init:if auth=jwt -->
        |   |   |-- oauth/            # OAuth provider buttons, connected accounts, provider icons
        |   |   |-- profile/          # Profile editing, avatar management
        |   |   |-- settings/         # Password change, 2FA setup, account deletion
<!-- @init:end -->
        |   |   +-- ui/               # shadcn-svelte (button, card, dialog, table, command, sidebar, ...)
        |   |-- state/                # Reactive state (.svelte.ts) - theme, shortcuts, health, breadcrumb
<!-- @init:if auth=jwt -->
        |   +-- utils/                # Permissions, platform detection, audit labels, class merging
<!-- @init:else -->
        |   +-- utils/                # Platform detection, class merging
<!-- @init:end -->
        |-- routes/
<!-- @init:if auth=jwt -->
        |   |-- (app)/                # Authenticated pages with sidebar layout
        |   |   |-- admin/            # User, role, job, and OAuth provider management
        |   |   |-- profile/          # User profile
        |   |   +-- settings/         # Account settings, 2FA, connected accounts
        |   |-- (public)/             # Login, register, forgot/reset password, email verification, OAuth callback
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
        |   |-- (app)/                # Authenticated pages with sidebar layout
        |   |-- (public)/             # Login (hands off to Keycloak)
<!-- @init:end -->
<!-- @init:if auth=none -->
        |   |-- (app)/                # Pages with sidebar layout
<!-- @init:end -->
        |   +-- api/                  # CSRF-protected API proxy to backend
        +-- messages/                 # i18n translations (en, cs)
<!-- @init:end -->
```
