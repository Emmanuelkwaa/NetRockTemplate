---
description: "Security checklist and conventions. Auto-injected into security-aware agents - not user-invocable."
user-invocable: false
---

# Security Conventions

## Architecture Context

<!-- @init:if auth=jwt -->
- JWT access tokens in HttpOnly cookies, refresh token rotation
- Permission-based authorization via `[RequirePermission]` attributes
- Role hierarchy: Superuser > Admin > User > Custom
- PII masking server-side via `PiiMasker` / `WithMaskedPii` - requires `users.view_pii` permission
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
- External Keycloak server is the identity provider (setup: `docs/keycloak.md`)
- API validates Keycloak access tokens (signature, issuer, audience, lifetime) from bearer header or HttpOnly cookie
- Authorization code flow with PKCE; state and verifier in a short-lived, data-protected HttpOnly cookie
- `[Authorize]` via `ApiController`; no roles or permissions in the project
<!-- @init:end -->
<!-- @init:if auth=none -->
- No authentication - every endpoint is anonymous
<!-- @init:end -->
- CSRF protection via Origin header validation on mutations
<!-- @init:if frontend=yes -->
- CSP with nonce-based script-src
- API proxy in SvelteKit forwards cookies + headers
<!-- @init:end -->

## Security Checklist

<!-- @init:if auth=jwt -->
### Authentication & Authorization
- [ ] Auth middleware cannot be bypassed (no unprotected routes that should be protected)
- [ ] `[RequirePermission]` on every action that needs it - not just `[Authorize]`
- [ ] Role hierarchy enforced - cannot manage users at/above your rank
- [ ] Cannot modify your own roles, lock yourself, or delete yourself
- [ ] Refresh token rotation works correctly - old tokens invalidated
- [ ] Permission changes invalidate affected users' tokens and cache
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
### Authentication & Authorization
- [ ] Auth middleware cannot be bypassed (no unprotected routes that should be protected)
- [ ] Token validation keeps issuer, audience, signature and lifetime checks on - never disabled to make a token pass
- [ ] Login `returnUrl` and stored return URL are local paths only
- [ ] Login state compared in constant time; state cookie short-lived, HttpOnly, data-protected
- [ ] Logout revokes the Keycloak session and clears the cookies
<!-- @init:end -->
<!-- @init:if auth=none -->
### Access
- [ ] No endpoint exposes data or operations that need an identity - there is no authentication
<!-- @init:end -->

### Input Validation
- [ ] All input validated on backend (FluentValidation) - frontend validation is UX only
- [ ] File uploads: size limits, MIME type validation, no path traversal
- [ ] URL fields: restrict to http/https schemes via `Uri.TryCreate`
- [ ] SQL injection: parameterized queries only (EF Core handles this, but check raw SQL)
- [ ] No string interpolation in LINQ/SQL queries
- [ ] Rate limiting on auth endpoints and mutations

### PII Compliance
<!-- @init:if auth=jwt -->
- [ ] Emails, phone numbers, usernames never exposed without `users.view_pii` permission
- [ ] PII masking happens server-side - never rely on frontend
<!-- @init:end -->
- [ ] No PII in logs (structured logging with masking)
- [ ] No PII in URLs, tokens, or error messages
<!-- @init:if auth=jwt -->
- [ ] Self-view exemption: users see their own unmasked data
- [ ] Audit trail for PII access
<!-- @init:end -->

### Information Leakage
- [ ] Error messages use `ErrorMessages.*` constants - no stack traces, no internal details
- [ ] `ProblemDetails` for all error responses - no anonymous objects
<!-- @init:if auth=jwt -->
- [ ] Identity errors: log `.Description` server-side, return static message to client
- [ ] No sensitive data in JWT claims beyond what's needed
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
- [ ] Keycloak error responses logged server-side, static `ErrorMessages` constant returned to client
<!-- @init:end -->
- [ ] `SetDbStatementForText` never set to `true` in OTEL (captures SQL with PII)
- [ ] `appsettings.Development.json` and `appsettings.Testing.json` excluded from publish

<!-- @init:if auth=jwt -->
### Cookie & Token Security
- [ ] HttpOnly, Secure, SameSite=Strict on auth cookies
- [ ] JWT secret is strong (64+ chars for HMAC SHA-256)
- [ ] Token lifetimes appropriate (short access, longer refresh)
- [ ] Refresh tokens are one-time-use with rotation
<!-- @init:end -->
<!-- @init:if auth=keycloak -->
### Cookie & Token Security
- [ ] HttpOnly, Secure on auth cookies
- [ ] Keycloak client secret only in user secrets or environment - never committed
- [ ] Cookie lifetimes follow the token lifetimes Keycloak returns
<!-- @init:end -->

### CSRF & CORS
- [ ] Origin header validated on all mutations via API proxy
- [ ] CORS restrictive by default - only `ALLOWED_ORIGINS`
- [ ] No `Access-Control-Allow-Origin: *` in production

### Response Headers
- [ ] `X-Content-Type-Options: nosniff`
- [ ] `X-Frame-Options: DENY`
- [ ] `Referrer-Policy: strict-origin-when-cross-origin`
- [ ] HSTS in production
- [ ] CSP nonce-based script-src

### Infrastructure
- [ ] Secrets in env vars or `.env` - never in code or config committed to git
- [ ] Docker containers hardened (read-only root, no-new-privileges, caps dropped)
- [ ] Database credentials not exposed in connection strings in logs
- [ ] Health check endpoints don't leak sensitive info

<!-- @init:if auth=jwt -->
### Audit
- [ ] Security-significant actions audited via `IAuditService.LogAsync`
- [ ] Audit metadata serialized with `JsonSerializer.Serialize` (not string interpolation)
- [ ] Login, logout, password change, role change, permission change all audited
<!-- @init:end -->
