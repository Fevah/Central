# Auth Service — Phased Buildout

Last updated: 2026-04-20
Status: **Phases A + B + C + D shipped**; E-G not started.

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

### Phase B — Token lifecycle  **(shipped 2026-04-20)**

**Goal:** Access tokens are short-lived (15 min) and refresh tokens rotate
properly. Logout revokes the current session.

Shipped:
- Migration 111 — `secure_auth.sessions` table (id / user_id /
  refresh_token_hash / issued_at / expires_at / revoked_at /
  rotated_to_session_id / user_agent / ip_address). Partial index
  on `(refresh_token_hash)` filtered to live rows.
- `POST /api/v1/auth/refresh` — validates the incoming refresh
  token's SHA-256 hash against `sessions.refresh_token_hash`,
  issues a new pair in the same transaction that marks the old
  row revoked + sets `rotated_to_session_id`. Failure at any step
  rolls back; the client keeps using the old token.
- `POST /api/v1/auth/logout` — extracts `sid` from the bearer JWT
  (even if already expired — signature still checked), revokes
  that specific session. Always returns 204 so retries are safe.
- Login + refresh both bake `sid` into the JWT claims so logout +
  future audit queries know which session to act on.
- 30s clock-skew leeway on JWT validation (`jsonwebtoken::Validation::leeway`).
- Refresh tokens are NOT Argon2'd (unlike passwords). The input is
  256 bits of crypto-random entropy, so SHA-256 gives sufficient
  protection + lets the refresh-handler do an O(1) indexed lookup
  rather than scanning every live row.
- `rt_<43-chars-base64url>` prefix on generated tokens so they're
  identifiable in logs.

Not shipped (deferred):
- MFA branching — Phase C.
- Refresh token reuse-detection "someone replayed a revoked token;
  nuke the whole chain" — pragmatic next iteration, not blocking.
- IP-address-based session metadata — the column exists; Phase D's
  lockout flow needs it filled in from the request.

### Phase C — MFA  **(shipped 2026-04-20)**

**Goal:** Users with MFA enabled complete a second factor before the final
access token issues.

Shipped:
- Migration 112 — three tables: `secure_auth.mfa_secrets` (TOTP
  secret per user, base32, algorithm/digits/period config,
  verified_at for setup-confirm gating), `secure_auth.mfa_login_challenges`
  (5-min-TTL row created at login when mfa_enabled; consumed_at +
  failed_attempts for one-shot replay protection + 5-attempt lockout),
  `secure_auth.mfa_recovery_codes` (10 per user, Argon2-hashed so
  lookup is a bounded iterate + argon2.verify scan).
- `POST /api/v1/auth/login` branches on `users.mfa_enabled=true` —
  returns the mfa_required payload the Angular client already knows
  how to render (empty access_token + challenge id in session_id +
  mfa_methods listing "totp" and "recovery" when codes remain).
- `POST /api/v1/auth/mfa/verify` — method="totp" verifies current
  TOTP (±30s skew via totp-rs `check_current`), method="recovery"
  iterates live recovery codes + argon2-verifies each. Wrong code
  increments `failed_attempts`; 5th wrong aborts the challenge. On
  success consumes the challenge + recovery code (if used) + issues
  a full access + refresh pair (same shape as normal login — Phase
  B session rotation continues to work).
- `POST /api/v1/auth/mfa/setup` (authenticated) — generates a new
  160-bit TOTP secret, 10 random recovery codes (`xxxx-xxxxxx`
  hex), argon2-hashes the recovery codes server-side, upserts the
  user's secret, deletes any previous recovery codes. Returns the
  otpauth URI + raw recovery codes (shown once — client must store).
- `POST /api/v1/auth/mfa/setup/confirm` (authenticated) — takes a
  TOTP code, verifies it, flips `verified_at` + `users.mfa_enabled=true`
  atomically. Next login now requires MFA.
- End-to-end verified live against the podman central-postgres pod:
  plain login → /mfa/setup → /mfa/setup/confirm → fresh login
  returns challenge → wrong TOTP 401 → right TOTP 349-char access
  token → challenge replay 401 → new login → recovery code 349-char
  access token → recovery replay 401.

Not shipped (deferred):
- `POST /api/v1/auth/mfa/disable` — no endpoint yet to turn MFA off
  for a user. Admin can do it via `UPDATE secure_auth.users SET
  mfa_enabled=false` for now. Phase D bundles this with the
  password-management endpoints.
- Recovery-code regeneration — setup overwrites; no endpoint to
  request a fresh batch without re-generating the secret. Phase D.
- TOTP secret encryption at rest — currently plaintext base32
  (honest — same blast radius as password hashes + API keys that
  already live in the same DB). Phase G wraps with
  `CredentialEncryptor` to match the desktop's SSH secret handling.

### Phase D — Password management + lockout  **(shipped 2026-04-20)**

**Goal:** Users can change password, reset forgotten ones, and accounts
lock after abuse.

Shipped:
- Migration 113 — `secure_auth.password_history` (per-user ledger of
  retired Argon2 hashes; handler trims to newest 5 after each
  change), `secure_auth.login_attempts` (per-email + per-IP rolling
  audit with `succeeded`+`failure_reason` cols + partial index on
  recent failures), `secure_auth.password_reset_tokens` (32-byte
  crypto-random token, SHA-256 hashed in DB, 1-hour TTL,
  single-use).
- Lockout at `POST /api/v1/auth/login` — 5 failures in the last 15
  min with no successful login since -> 429 Too Many Requests with
  `Retry-After: 900`. Lockout query filters failures-since-last-
  success so a legit login clears the counter implicitly. All
  attempts logged (even lockout-rejected) for the audit trail.
- `POST /api/v1/auth/change-password` — authed; verifies old
  password, enforces 12-char min + diff-from-old, scans the last 5
  history hashes with `argon2.verify` + rejects reuse, upserts the
  new hash + writes old to history + trims, **revokes every live
  session** for the user in the same tx so a stolen refresh token
  dies with the password change.
- `POST /api/v1/auth/password-reset/request` — generates a token,
  persists its SHA-256 + 1-hour expiry. Returns the raw token in
  the response body for Phase D (local dev workflow); Phase F
  swaps the response to an empty body + the token goes via
  Central's notifications pipeline. Enumeration-safe: unknown
  emails still get a 200 + a random token that will simply fail
  on confirm.
- `POST /api/v1/auth/password-reset/confirm` — takes the raw
  token + new password, SHA-256s for lookup, atomically: marks
  token consumed, pushes old hash to history, writes new hash,
  revokes every live session.
- End-to-end verified live against the podman central-postgres
  pod:
  1. Five wrong passwords → 401 each; 6th with right password
     → 429 (locked out).
  2. change-password wrong-old → 401 / same-as-old → 400 /
     too-short → 400 / valid → 204.
  3. After change, old login → 401, new login → 200.
  4. Attempt to revert to old password → 400 (password_reused).
  5. /password-reset/request → raw token; /confirm → 204; replay
     → 401; login with reset password → 200.

Not shipped (deferred):
- Full password-policy (upper/lower/digit/symbol) — Phase D ships
  the "12-char minimum" + "must differ from old" floor. Full
  policy is config-driven, lands with Phase G's hardening pass.
- Admin unlock endpoint — operators can manually `DELETE FROM
  secure_auth.login_attempts WHERE email = ...` for now. Admin UI
  lands Phase F with the deployment work.
- Email delivery — Phase F wires this via the Central notifications
  pipeline so we don't duplicate the SMTP integration that already
  exists on the .NET side.

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
