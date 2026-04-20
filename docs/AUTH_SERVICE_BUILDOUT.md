# Auth Service — Phased Buildout

Last updated: 2026-04-20
Status: **Phase A in progress** — all other phases not started.

## Why this exists as a separate service

The Angular web client logs in against `${authServiceUrl}/api/v1/auth/login`
(see [apps/web/src/app/core/services/auth.service.ts:68](apps/web/src/app/core/services/auth.service.ts#L68)),
which resolves to a Rust auth-service that was **planned but never built**.
The desktop uses its own path: Windows SSO → `app_users` via Central.Api. The
two identity stores don't bridge today. Building auth-service is how the web
gets a real login without shimming against Central.Api.

Config already exists at [config/auth-service.toml](config/auth-service.toml)
expecting port 8081, database `secure_auth` on localhost, a JWT signing key,
and Redis for session state.

## Goals

1. One service the web client can point at to log in, refresh, and log out.
2. Pluggable enough that desktop can eventually also delegate here instead
   of the current Windows-SSO-only path.
3. Honest failure modes — auth is the wrong place for "fail silently and move
   on"; every unhappy path returns a concrete HTTP status + error body.
4. Shipped incrementally. Each phase lands a shippable increment + passes the
   existing `ServiceHealthTests.AuthService_IsReachable` probe once its
   health endpoint binds.

## Release classification for auth changes

Auth-service is versioned independently of the desktop's module-update system
(see [docs/MODULE_UPDATE_SYSTEM.md](MODULE_UPDATE_SYSTEM.md) for the module
system's own classification). But the same principle applies: default to the
most backward-compatible option. Concretely:

| Change | Auth-service impact |
|--------|--------------------|
| Adding a new endpoint | Rolling deploy. Old clients ignore the new path. |
| Adding a new field to `LoginResponse` | Rolling deploy. Angular tolerates unknown fields. |
| Renaming/removing an endpoint | Full upgrade window — stagger client + server deploys. |
| Changing JWT claims shape | Full upgrade + token invalidation. Users re-login. |

## Phases

### Phase A — Scaffold + minimum login (**current roll**)

**Goal:** The Angular web login succeeds against a real service against a
user row in the DB. No refresh, no MFA, no SSO.

Deliverables:
- `services/auth/` Cargo crate. Axum + sqlx + jsonwebtoken + argon2.
- `POST /api/v1/auth/login` endpoint: accepts `{ email, password }` with
  `X-Tenant-ID` header. Argon2-verifies against
  `secure_auth.users.password_hash`. On success returns the
  `LoginResponse` shape the Angular client expects (access_token,
  refresh_token, session_id, expires_in, token_type, mfa_required,
  mfa_methods, user). On failure returns 401 with `{ error, detail }`.
- `GET /health` — 200 OK when the service is up + DB reachable. The
  existing `ServiceHealthTests.AuthService_IsReachable` probes this
  through the K8s NodePort (30081).
- Migration `110_secure_auth_schema.sql` — creates `secure_auth` schema,
  `users` table (id / email / password_hash / display_name / first_name /
  last_name / is_global_admin / mfa_enabled / created_at / last_login_at /
  deleted_at), seeds one known account (`corys@central.local` with an
  Argon2 hash of a known dev password).
- JWT signing key from config — single key for access tokens. No refresh
  rotation yet: the refresh_token field returns the same access token
  for now (honest placeholder; real refresh lands in Phase B).
- CREDENTIALS.md update — reflect reality (what's seeded today, what's
  still aspirational).

Non-goals in Phase A:
- Refresh token lifecycle (Phase B).
- MFA, SSO, external IDPs (Phases C, E).
- Rate limiting, session tracking, lockout (Phase D, G).
- Tests beyond `cargo check` (tests need a live `secure_auth` DB + come in Phase G).
- K8s deployment (Phase F) — Phase A runs locally via `cargo run`.

### Phase B — Token lifecycle

**Goal:** Access tokens are short-lived (15 min) and refresh tokens rotate
properly. Logout revokes.

Deliverables:
- `POST /api/v1/auth/refresh` — accepts `{ refresh_token }`, validates
  against `secure_auth.sessions`, rotates + returns a new access + refresh.
- `POST /api/v1/auth/logout` — authenticated; revokes the current
  session.
- `secure_auth.sessions` table — id / user_id / refresh_token_hash /
  issued_at / expires_at / revoked_at / user_agent / ip_address.
- Argon2-hashed refresh tokens stored; raw value returned once to client.
- Clock skew tolerance built in (30s default).

### Phase C — MFA

**Goal:** Users with MFA enabled complete a second factor before the final
access token issues.

Deliverables:
- `POST /api/v1/auth/login` returns `{ mfa_required: true, session_id,
  mfa_methods }` when the user has MFA enabled, without an access_token.
- `POST /api/v1/auth/mfa/verify` — accepts `{ session_id, code, method }`,
  validates TOTP against `secure_auth.mfa_secrets`, issues real access +
  refresh on success.
- `POST /api/v1/auth/mfa/setup` — generates TOTP secret, returns otpauth
  URI + QR-code-ready.
- `secure_auth.mfa_recovery_codes` table — one-shot backup codes.
- Rate limit: 5 wrong codes per session, then abort.

### Phase D — Password management + lockout

**Goal:** Users can change password, reset forgotten ones, and accounts
lock after abuse. Passwords obey history + complexity requirements.

Deliverables:
- `POST /api/v1/auth/change-password` — authenticated; old + new password.
- `POST /api/v1/auth/password-reset/request` — email-backed.
- `POST /api/v1/auth/password-reset/confirm` — token + new password.
- `secure_auth.password_history` — last N hashes per user; reject reuse.
- `secure_auth.login_attempts` — per-IP + per-email; lockout after 10
  failures in 15 min; admin unlock endpoint.
- Password policy: ≥12 chars, ≥1 upper, ≥1 lower, ≥1 digit, ≥1 symbol.
  Configurable via `config/auth-service.toml`.

### Phase E — Federation + external IDPs

**Goal:** SAML2, OIDC (generic), Entra ID, Okta, Google, Microsoft,
GitHub — configurable per tenant.

Deliverables:
- `POST /api/v1/auth/sso/:provider/start` — returns redirect URL.
- `POST /api/v1/auth/sso/:provider/callback` — exchanges code, maps to
  an internal user via `secure_auth.user_external_identities`.
- Tenant-scoped IDP config in `secure_auth.identity_providers` (already
  partially modelled by migration 047 on the `central` DB side; Phase E
  brings the auth-service's own copy into sync).
- Claim mapping (email / display_name / groups → roles).

### Phase F — Deployment

**Goal:** Auth-service runs in K8s alongside Central.Api and the
networking-engine, exposed via NodePort 30081.

Deliverables:
- `Containerfile` in `services/auth/` (multi-stage Rust build).
- `infra/k8s/base/auth-service-deployment.yaml` + Service + ConfigMap.
- `/metrics` Prometheus endpoint (prometheus-client crate).
- Structured JSON logs via `tracing-subscriber`.
- Secrets via sealed-secrets (JWT signing key, DB password).

### Phase G — Tests + hardening

**Goal:** Automated coverage + basic security posture checked in CI.

Deliverables:
- `cargo test` suite — unit (password hashing, JWT issue/verify, refresh
  rotation) + integration against a testcontainer Postgres.
- Rate-limit integration test.
- Security headers on all responses (HSTS, X-Content-Type-Options,
  X-Frame-Options, CSP).
- CORS policy locked to the Angular origin.
- Dependency audit step added to the existing `.github/workflows/security.yml`.

## Acceptance criteria per phase

Each phase ends green on:
1. `cargo build` passes in the service directory.
2. `cargo test` passes (from Phase G onwards; pre-G rolls skip this gate).
3. Full `.NET` suite unaffected — auth-service is a sibling service, its
   changes shouldn't regress Central.Api's tests.
4. The `ServiceHealthTests.AuthService_IsReachable` probe flips from
   "expected-fail when down" to "passes when the service is running"
   starting from Phase A.
5. Docs updated — this file's phase table reflects shipped phases; the
   per-phase commit body names the phase + what shipped.

## Out of scope (permanently or for now)

- **WebAuthn / passkeys** — considered for Phase C+; the design carries the
  `mfa_methods` array so adding a new method is additive, no contract
  break.
- **Audit forensics dashboards** — the M365 auth-service on the desktop
  side (migration 033 + the Audit WPF module) has its own scope. Auth-service
  emits audit events; visualisation stays with the Audit module.
- **User-facing admin UI** — the Angular admin page exists
  ([apps/web/src/app/modules/admin/](apps/web/src/app/modules/admin/))
  and talks to Central.Api. Phase E extends it to manage auth-service's
  IDP configuration.
