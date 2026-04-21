# Identity Provider (IDP) Module — Full Buildout

Last updated: 2026-04-21
Status: **Phases 1 + 2 + 3 + 4 done**. Phases 5-10 pending.

## Why this document exists

Central today has three user stores (`app_users`, `central_platform.global_users`,
`secure_auth.users`) + two partial MFA attempts. The auth-service work so
far (see [AUTH_SERVICE_BUILDOUT.md](AUTH_SERVICE_BUILDOUT.md) phases A-E.1)
covers the web login path but doesn't touch the desktop's `app_users`
Windows-SSO path. Every feature we've shipped — MFA, lockout, password
management, SSO consumer — benefits web users only until we unify.

The right move isn't to keep adding to `auth-service` in isolation. It's
to turn it into the core of a proper Identity Provider module, serve
both surfaces (web + desktop) from one identity store, and grow into
a full IDP over time.

This doc is the plan — 10 phases, sequential. Each phase lands a
shippable increment that leaves the system in a working state; at no
point is the desktop non-functional.

## Ten-phase map

| # | Phase | Goal | Shipped |
|---|-------|------|---------|
| 1 | Foundation | auth-service with login / refresh / logout / MFA / password-mgmt / SSO-consumer scaffold | ✅ (A-E.1) |
| 2 | Unify stores (dual-write bridge, not view — FK reality) | Cross-reference columns + triggers; Windows-SSO bridge endpoint | ✅ |
| 3 | Restructure into `services/identity/` | Workspace with `core` lib + `auth-service` bin; foundation for Phase 4+ | ✅ |
| 4 | Admin API | User CRUD + tenant mgmt + audit reader + session/device mgmt + SSO config CRUD | ✅ (4.A + 4.B both shipped) |
| 5 | Real Duo + real OIDC | Finish Phase C.1 (Duo) + Phase E.2 (OIDC providers) + Phase E.3 (SAML2) against the unified store | ⏳ |
| 6 | Groups + RBAC + claims | `groups` + `user_groups` tables; JWT claims include effective permissions; Central.Api validates | ⏳ |
| 7 | OIDC provider mode | BE an IDP — `/oauth2/authorize` + `/token` + `/userinfo` + `/jwks` + client registration | ⏳ |
| 8 | WebAuthn + magic links | Modern factors + passwordless sign-in | ⏳ |
| 9 | Admin UI + desktop bridge | Angular admin pages + WPF Global-module panels; desktop runs on JWTs from the IDP | ⏳ |
| 10 | Deploy + harden | K8s manifests + Containerfile + metrics + CI engine-contract check + security headers | ⏳ |

Each phase below: what it ships, what it doesn't, what depends on it, how
end-to-end verification works.

---

## Phase 1 — Foundation (**shipped**)

`services/identity/auth-service/` Rust crate (workspace member since Phase 3). Endpoints:
- `POST /api/v1/auth/login` (Argon2 + JWT + session_id in claims)
- `POST /api/v1/auth/refresh` (rotating refresh tokens via `secure_auth.sessions`)
- `POST /api/v1/auth/logout` (revokes specific session by `sid` claim)
- `POST /api/v1/auth/mfa/verify` (TOTP + recovery codes; 5-attempt lockout per challenge)
- `POST /api/v1/auth/mfa/setup` + `/mfa/setup/confirm` (enrolment)
- `POST /api/v1/auth/change-password` (history + session revocation on change)
- `POST /api/v1/auth/password-reset/request` + `/password-reset/confirm`
- `GET /api/v1/auth/sso/providers` + `POST /api/v1/auth/sso/:provider/start` + `/callback` (mock provider live; real providers stubbed 501)

Migrations 110-114 shipped: `secure_auth.users`, `sessions`, `mfa_secrets`,
`mfa_login_challenges`, `mfa_recovery_codes`, `password_history`,
`login_attempts`, `password_reset_tokens`, `identity_providers`,
`user_external_identities`, `sso_sessions`.

Seed: `corys@central.local` / `corys-dev-pass!` + mock SSO provider.

---

## Phase 2 — Unify stores  *(shipped 2026-04-21 — dual-write bridge, not view)*

**What actually shipped + why the plan below was revised.** The
original plan (further down, kept as the aspirational future state)
proposed `app_users becomes a VIEW`. Live audit revealed
`app_users` is referenced by **11 foreign keys** from other tables
(activity_feed, audit_log, dashboards, portfolios, programmes,
project_members, saved_reports, sprint_allocations, task_comments,
task_views, custom_column_permissions). Replacing a FK-referenced
table with a view would mean rewriting every one of those foreign
keys and every WPF path that inserts into them — a multi-week
exercise touching live modules.

The pragmatic shipped revision: **dual-write bridge**.

Migration 116_user_store_bridge.sql:
- Extended `secure_auth.users` with `legacy_int_id` (nullable unique),
  `username`, `role`, `is_active`, `tenant_id`, `ad_sid`, `ad_guid`.
- Added `app_users.secure_auth_user_id uuid` cross-reference column.
- Backfilled every existing `app_users` row into `secure_auth.users`,
  paired by email. Users without email got a synthesised
  `username@central.local`. Password hashes that weren't Argon2 were
  wrapped with a `(legacy-sha256:...)` prefix so auth-service's
  argon2.verify fails cleanly for them (these users will bounce
  through password-reset or Windows-SSO until a real Argon2 hash
  lands).
- Installed two trigger functions with `pg_trigger_depth() > 1` re-
  entrancy guards: `mirror_to_secure_auth_users` on app_users,
  `mirror_to_app_users` on secure_auth.users. INSERT / UPDATE in
  either table propagates basic identity fields (username, email,
  display_name, role, is_active, last_login_at) to the other.

Migration 117_identity_providers_windows.sql:
- Dropped + re-added `identity_providers.kind` CHECK to include
  `'windows'` (CHECK constraints aren't ALTERable in place).
- Seeded `provider_code='windows'` row.
- Migrated every AD-linked app_users row into
  `secure_auth.user_external_identities` with
  `provider_code='windows'`, `external_id=lower(username)`, raw_claims
  carrying `ad_guid` + `ad_sid` + `tenant_id`.

Dedicated bridge endpoint on auth-service:
- `POST /api/v1/auth/sso/windows/callback` — desktop posts
  `{ domain_username, machine_name? }`. Accepts either `DOMAIN\user`
  or bare `user` form (case-folded). Looks up
  user_external_identities WHERE provider_code='windows' AND
  external_id=$1, issues a full JWT via the shared Phase B session
  path. No state nonce dance (desktop isn't doing a browser
  redirect). Failure cases log to `login_attempts` with
  `failure_reason='windows_sso_unknown_user'`.
- **Production deployment caveat:** this endpoint MUST be IP-
  restricted to known desktop subnets — anyone who can POST here
  can log in as any AD-linked user. Phase 10 adds the restriction
  via K8s NetworkPolicy + tower-http.

End-to-end verified live (9 assertions):
1. Existing app_users rows (cory.sharplin, corys) have
   `secure_auth_user_id` linked after migration.
2. `secure_auth.users` has matching rows with `legacy_int_id`
   populated.
3. Windows provider live + 2 AD identities migrated.
4. INSERT into app_users → trigger mirrored into secure_auth.users.
5. INSERT into secure_auth.users → trigger mirrored back into
   app_users.
6. No cascade loops — `pg_trigger_depth()` guards worked.
7. POST `/sso/windows/callback` with `CORP\corys` resolved to
   `cs@opentech.live` + issued a 345-char JWT.
8. Unknown username → 401 + audit row logged.
9. Cleanup (delete bridge-test rows) cascaded correctly.

Not shipped (still deferred):
- Dropping `central_platform.global_users`. Phase 4 does that when
  admin-service replaces any legacy uses.
- Making `app_users` a view. The view plan below is retained as
  aspirational — requires Phase 4 + follow-on FK refactor across
  11 tables. Realistically: landing around Phase 9 or later.
- Updating WPF desktop's `App.xaml.cs` to call the Windows-SSO
  bridge endpoint. Endpoint ships in this phase; desktop wiring
  lands in Phase 9 once the JWT-based data service path is ready.

---

## Phase 2 (original aspirational plan — retained for Phase 9 revisit)

**Goal:** one user table serves web + desktop. `app_users` becomes a view.
Windows SSO bridges through the IDP.

### Problem today

- `app_users` has: `id (int)`, `username`, `email`, `role`, `password_hash`,
  `salt`, `last_login`, per-module permissions, department / title / phone
  fields (migration 039), AD sync fields (GUID / last_ad_sync).
- `secure_auth.users` has: `id (uuid)`, `email`, `password_hash` (Argon2),
  `display_name` / `first_name` / `last_name`, `is_global_admin`,
  `mfa_enabled`, `duo_enabled` (once 115 lands).
- `central_platform.global_users` was never wired to a real login path —
  legacy artefact from the platform-admin spike.

### Schema merge strategy

One `secure_auth.users` with a superset of columns. The subset
currently in `app_users` that matters for identity (email / username /
password / active) moves over; the subset that's organisational
metadata (department / phone / AD GUID) becomes `secure_auth.user_profiles`
(1:1, optional) so the identity table stays small.

Key decisions:

1. **Primary key:** `uuid`. `app_users.id` (int) gets mapped through a
   `legacy_int_id` nullable column on `secure_auth.users` + a unique
   index, so every WPF `int`-PK query keeps working via the view.

2. **Username + email:** username becomes nullable on
   `secure_auth.users` (Phase 1 users don't have one). Desktop queries
   by username; web queries by email. Both paths reach the same row.

3. **Password hash:** Windows-SSO-only users get the `(sso-only)`
   sentinel hash (already used by Phase E.1 for SSO-created users).
   `/login` rejects them naturally; they auth via the Windows-SSO
   bridge instead.

4. **Role:** `app_users.role` (string) becomes a column on
   `secure_auth.users` initially. Phase 6 replaces with proper groups.

5. **AD / domain user:** `app_users.ad_guid` / `domain_username` move
   to `secure_auth.user_external_identities` with `provider_code='windows'`,
   `external_id='{DOMAIN}\{username}'`. This IS the Windows-SSO
   bridge — the bridge endpoint looks up by external_id.

### Migration plan (migration 116)

```sql
-- 1. Extend secure_auth.users with superset columns
ALTER TABLE secure_auth.users
    ADD COLUMN IF NOT EXISTS username         varchar(128) UNIQUE,
    ADD COLUMN IF NOT EXISTS role             varchar(64) NOT NULL DEFAULT 'user',
    ADD COLUMN IF NOT EXISTS is_active        boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS legacy_int_id    integer UNIQUE,
    ADD COLUMN IF NOT EXISTS must_change_password boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS tenant_id        uuid;

-- 2. secure_auth.user_profiles — organisational metadata
CREATE TABLE secure_auth.user_profiles (
    user_id       uuid PRIMARY KEY REFERENCES secure_auth.users(id) ON DELETE CASCADE,
    department    varchar(255),
    job_title     varchar(255),
    phone         varchar(64),
    mobile        varchar(64),
    company       varchar(255),
    notes         text,
    updated_at    timestamptz NOT NULL DEFAULT now()
);

-- 3. Migrate app_users -> secure_auth.users by username/email match
INSERT INTO secure_auth.users
    (username, email, password_hash, role, is_active, legacy_int_id, ...)
SELECT ... FROM app_users
ON CONFLICT (email) DO UPDATE SET username = EXCLUDED.username, ...;

-- 4. Migrate AD identities -> user_external_identities
INSERT INTO secure_auth.user_external_identities
    (user_id, provider_code, external_id, raw_claims)
SELECT u.id, 'windows', au.domain_username, jsonb_build_object('ad_guid', au.ad_guid)
FROM app_users au
JOIN secure_auth.users u ON u.legacy_int_id = au.id
WHERE au.domain_username IS NOT NULL;

-- 5. app_users becomes a VIEW — every existing WPF query still works
DROP TABLE app_users;
CREATE VIEW app_users AS
SELECT
    legacy_int_id        AS id,
    username,
    email,
    password_hash,
    '(derived)'          AS salt,   -- Argon2 includes salt in hash, exposed for back-compat
    role,
    is_active,
    last_login_at        AS last_login,
    ...
FROM secure_auth.users
WHERE legacy_int_id IS NOT NULL;
```

### Desktop Windows-SSO bridge (new endpoint)

`POST /api/v1/auth/sso/windows/callback` — accepts `{ domain_username,
machine_name }`, looks up `user_external_identities WHERE provider_code='windows'
AND external_id=$1`, issues a JWT for the matched user. The endpoint is
IP-restricted in production (Phase 10) to known desktop subnets; for
dev it's open.

Desktop's `App.xaml.cs` changes:
- `Environment.UserName` + domain lookup produces `DOMAIN\corys`.
- POST to `/api/v1/auth/sso/windows/callback` with that string.
- Store the JWT in memory; pass as `Authorization: Bearer` on every
  Central.Api call.
- If the POST fails (auth-service unreachable), fall back to the
  existing `CentralDataService.DirectDb` offline mode — the desktop
  keeps working air-gapped. Phase 10 adds JWT-cache persistence so
  offline mode keeps the last-known identity.

### Phase 2 acceptance

- `SELECT COUNT(*) FROM app_users` returns the same count as before (view).
- Desktop logs in as `corys` via Windows SSO bridge → gets a JWT →
  calls Central.Api endpoints with it.
- Web login as `corys@central.local` / `corys-dev-pass!` still works.
- `secure_auth.users` has one row per human; no duplicates.
- Every existing desktop test that asserted against `app_users`
  passes unchanged (the view delivers back-compat).

### Phase 2 deliberately NOT doing

- Removing `central_platform.global_users`. Still there, still dead.
  A cleanup migration lands with Phase 4 once admin-service replaces
  whatever the legacy rows were used for.
- Changing WPF `DbRepository` — it keeps reading `app_users`. The
  view abstracts the storage change.

---

## Phase 3 — Restructure into `services/identity/`  *(shipped 2026-04-21)*

**Goal:** the code layout supports multiple binaries sharing a core
library + migrations. Foundation for every phase 4+.

Shipped:
- `git mv services/auth/ services/identity/auth-service/` — history
  traces cleanly, no path refs outside docs needed updating.
- `services/identity/Cargo.toml` — workspace manifest with shared
  `[workspace.dependencies]` so future members don't drift on sqlx
  / serde / argon2 versions.
- `services/identity/core/` — new `identity-core` lib crate. Three
  starter modules:
  - `tokens` — `generate_refresh_token` / `generate_password_reset_token`
    / `generate_sso_state_nonce` (typed wrappers over a shared 32-byte
    random + prefix), plus `sha256_hex` for deterministic lookup.
  - `passwords` — Argon2id `hash_password` + `verify_password`.
  - `jwt` — `Claims` struct + `sign` + `decode_with_leeway`. Matches
    the shape auth-service has been issuing since Phase A +B so
    existing clients + tokens keep working.
  - 11 unit tests covering the module surface.
- `services/identity/auth-service/Cargo.toml` — now declares
  `identity-core` as a path dep + inherits versions from the
  workspace. main.rs unchanged on this roll (still owns its own
  copies of the same functions); extraction happens opportunistically
  in Phase 4+ when admin-service needs them too.

Acceptance criteria met:
1. `cargo build` green from the workspace root.
2. `cargo test -p identity-core` — 11 passed / 0 failed.
3. auth-service smoke-tested from the new binary path:
   password login → 349-char JWT; Windows SSO bridge → 346-char JWT;
   `/sso/providers` lists mock + test-google + windows.
4. No external (non-docs) references to the old `services/auth/`
   path in the repo — grep verified before the move.

What Phase 3 deliberately didn't do:
- Full module extraction into `core`. The lib crate exports the
  three easiest-to-portable modules (tokens / passwords / jwt).
  Config loading, DB handling, and all the handlers stay in
  auth-service for now. Phase 4+ extracts what admin-service shares
  once that code actually exists to share.
- `services/identity/migrations/` subfolder. Migrations 110-117 stay
  under `db/migrations/` where every other repo migration lives —
  splitting them would fork the `schema_versions` ledger. Phase 10
  documentation describes the identity-owned migration range.
- Containerfile / K8s manifests. Those are Phase 10.

---

## Phase 4 — Admin API  *(4.A shipped 2026-04-21; 4.B pending)*

**4.A shipped.** New `services/identity/admin-service/` binary in the
workspace, listens on port 8083 (overridable via `ADMIN_SERVICE_PORT`).
Reads the same `config/auth-service.toml` + `AUTH_SERVICE_JWT_SECRET`
so it agrees with auth-service on JWT signing. Separate binary
because blast radius + auth posture differ (auth-service handles
every login; admin-service requires an already-issued global_admin
JWT).

Shipped endpoints (all gated by `require_global_admin` middleware
that verifies the bearer JWT + checks the DB's is_global_admin OR
role='global_admin' — claims can lie, the DB row is authoritative):

- `GET  /api/v1/admin/users` — paginated (default 50, max 200) + `?q=`
  search across email / username / display_name.
- `GET  /api/v1/admin/users/:id` — full row.
- `PATCH /api/v1/admin/users/:id` — partial update (role, is_active,
  display_name, first_name, last_name, email, mfa_enabled, duo_enabled).
  Dynamic UPDATE builds only the columns the caller supplied.
- `POST /api/v1/admin/users/:id/unlock` — DELETE failures in the
  last 15 min so lockout counter resets.
- `POST /api/v1/admin/users/:id/revoke-all-sessions` — sets
  `revoked_at` on every live session for the user.
- `POST /api/v1/admin/users/:id/force-reset` — sentinel password
  hash `(forced-reset)` + revoke every session. User regains access
  only via `/api/v1/auth/password-reset/request` + confirm.
- `GET  /api/v1/admin/audit/logins` — read of `secure_auth.login_attempts`
  with optional `?email=` + `?succeeded=` filters. Admin UI shows
  who's been trying to log in + why failing.
- `GET  /api/v1/admin/audit/sessions` — join of `secure_auth.sessions`
  with the user row for email context. Optional `?user_id=`,
  `?include_revoked=true`.
- `GET  /health` — 200 when DB reachable (skips the admin middleware).

Verified live (12 assertions): both services up, no-token → 401,
global_admin token grants access, list paginated correctly, search
narrows results, PATCH updates + persists, lockout audit shows
failure_reason, revoke-all-sessions returns count, audit/sessions
joins correctly, stub endpoints return honest 501, display_name
round-trip restored on cleanup.

### Phase 4.B — shipped 2026-04-21

Completes the admin surface. All 501 stubs from 4.A replaced with
real handlers:

- `POST /api/v1/admin/users` — accepts email + optional password
  (omit for SSO-only, gets `(sso-only)` sentinel hash; /login stays
  closed for them until a real password is set). 12-char min when
  supplied. Optional username / display_name / first_name / last_
  name / role / is_global_admin / is_active. Returns 201 + full
  user row. 409 on duplicate email; 400 on short password or
  missing email.
- `DELETE /api/v1/admin/users/:id` — soft-delete (stamps
  `deleted_at` + flips `is_active=false`) + revokes every live
  session in the same tx. 204 on success; 404 when already
  deleted.
- `GET /api/v1/admin/tenants` — thin read of
  `central_platform.tenants` (id / slug / display_name / domain /
  tier / is_active / created_at). Full tenant CRUD + metadata
  editor remain in the Phase 9 admin UI scope.
- `GET /api/v1/admin/sso/providers` — admin view INCLUDES disabled
  + soft-deleted rows + `config_json`. Public `/sso/providers` on
  auth-service filters these out.
- `POST /api/v1/admin/sso/providers` — create. 400 on check-
  constraint violations (unknown kind, bad provider_code format);
  409 on existing code.
- `PATCH /api/v1/admin/sso/providers/:code` — update display_name /
  enabled / config_json. Only columns in the body change.
- `DELETE /api/v1/admin/sso/providers/:code` — soft delete (stamps
  `deleted_at` + `enabled=false`). Row stays for audit — SSO
  callback logs reference it by provider_code.
- `GET /api/v1/admin/audit/password-changes` — reads
  `secure_auth.password_history` joined with user email. Timestamps
  + user id only — hash values never leave the DB. Optional
  `?user_id=` narrower. Every password change path writes here:
  `/change-password` + `/password-reset/confirm` (auth-service) +
  `/admin/users/:id/force-reset` (this service — fixed mid-roll to
  archive the old hash before the sentinel UPDATE, otherwise the
  audit missed it).
- `GET /api/v1/admin/audit/sso-callbacks` — filters
  `secure_auth.login_attempts` to rows where `failure_reason` is
  either `'sso:*'` (success) or `'*_sso_*'` (failure, e.g.
  `windows_sso_unknown_user`). Shows both paths together so the
  audit UI can pivot by success/failure without a second query.

End-to-end verified live (14 assertions + 1 follow-up for the
audit fix):
- POST /users creates + returns row; duplicate → 409; short
  password → 400; new user can immediately log in against
  auth-service.
- GET /tenants returns the Immunocore row.
- SSO provider GET (admin view) shows all 4 existing rows with
  full metadata; POST corp-okta creates; PATCH disables; DELETE
  soft-deletes.
- force-reset archives old hash to password_history so
  /audit/password-changes surfaces the event.
- /audit/sso-callbacks returns windows + mock entries together;
  confirms the LIKE pattern catches both success + failure paths.
- DELETE user soft + reflected in list (q=phase4b → total=0).

### Original Phase 4 plan (retained for 4.B reference)

**Goal:** an admin can manage users, tenants, SSO providers, groups,
and see audit trails — all via API, without `psql` + raw UPDATEs.

New binary: `services/identity/admin-service/` on port 8083.

Endpoints (all require `roles: ["global_admin"]` claim in JWT):

### Users
- `GET  /api/v1/admin/users` (paginated; search by email/username/role)
- `GET  /api/v1/admin/users/:id`
- `POST /api/v1/admin/users` (create; email required; password optional — admin can force reset flow)
- `PATCH /api/v1/admin/users/:id` (partial; profile + role changes)
- `DELETE /api/v1/admin/users/:id` (soft — sets `deleted_at`)
- `POST /api/v1/admin/users/:id/unlock` (clears `login_attempts` for this user)
- `POST /api/v1/admin/users/:id/force-reset` (invalidates password, requires reset flow on next login)
- `POST /api/v1/admin/users/:id/revoke-all-sessions`

### Tenants
- `GET /api/v1/admin/tenants`
- (full CRUD to follow once the tenant-scoping story in Phase 2 settles)

### SSO providers
- `GET  /api/v1/admin/sso/providers`
- `POST /api/v1/admin/sso/providers` (create)
- `PATCH /api/v1/admin/sso/providers/:code` (update config)
- `DELETE /api/v1/admin/sso/providers/:code`
- `POST /api/v1/admin/sso/providers/:code/test` (dry-run the provider)

### Audit
- `GET /api/v1/admin/audit/logins` (from `login_attempts`, filter by email/ip/window)
- `GET /api/v1/admin/audit/sessions` (active + revoked from `sessions`)
- `GET /api/v1/admin/audit/password-changes`
- `GET /api/v1/admin/audit/sso-callbacks`

### Migration 117 — `central_platform.global_users` cleanup

Admin-service absorbs any real use cases for global_users (there turned
out to be none — it was a legacy experiment). Drop the table in a
follow-up commit once Phase 4 UI consumes the admin API.

---

## Phase 5 — Real Duo + real OIDC + SAML

**Goal:** finish the SSO-consumer work the Phase 1 scaffold stubs with
501. This phase is the long-tail — each provider family is its own
slice.

### Phase 5.A — Duo Universal Prompt

- `duo_universal_sdk` crate (from Duo Labs) or similar maintained fork.
- Config in `secure_auth.duo_config` (migration 115 is already
  written — waiting for Phase 2 to land before applying).
- `/api/v1/auth/mfa/duo/start` + `/mfa/duo/callback` — the Phase 1
  scaffolded endpoints get real implementations behind `mode='live'`.
- End-to-end test requires a real Duo account; Phase 10 documents
  how to wire one.

### Phase 5.B — Generic OIDC + branded OIDC

- `openidconnect-rs` crate.
- Issuer + client_id + client_secret + scopes config in `config_json`
  on `identity_providers` rows.
- `kind='oidc'` handler (generic). `kind='google'` / `kind='microsoft'`
  / `kind='entra'` / `kind='okta'` wrap the generic handler with pre-
  baked issuer URLs + scope lists.
- nonce / state / JWK set handling per spec.

### Phase 5.C — SAML2

- `samael` crate for AuthnRequest / Response.
- SP metadata endpoint: `GET /api/v1/auth/sso/:provider/metadata.xml`
  so admins can hand Central's metadata to their IdP.
- NameID + attribute claim mapping.

---

## Phase 6 — Groups + RBAC + JWT claims

**Goal:** JWTs carry effective permissions so Central.Api can authorise
without calling back to the IDP on every request.

```sql
CREATE TABLE secure_auth.groups (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid,          -- NULL = platform-wide
    name        varchar(128) NOT NULL,
    description text,
    UNIQUE (tenant_id, name)
);

CREATE TABLE secure_auth.user_groups (
    user_id     uuid NOT NULL REFERENCES secure_auth.users(id) ON DELETE CASCADE,
    group_id    uuid NOT NULL REFERENCES secure_auth.groups(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, group_id)
);

CREATE TABLE secure_auth.group_permissions (
    group_id     uuid NOT NULL REFERENCES secure_auth.groups(id) ON DELETE CASCADE,
    permission   varchar(64) NOT NULL,   -- e.g. "crm:write", "networking:admin"
    PRIMARY KEY (group_id, permission)
);
```

JWT claim shape after Phase 6:
```json
{
  "sub": "<uuid>",
  "email": "...",
  "sid": "<session uuid>",
  "iss": "central-auth",
  "aud": "central-platform",
  "exp": ...,
  "iat": ...,
  "roles": ["admin"],
  "groups": ["platform-admins"],
  "perms": ["crm:write", "networking:admin", ...]
}
```

Central.Api's existing JWT middleware gains a permission-check
attribute: `[RequirePermission("crm:write")]` on endpoint handlers.
The desktop's client-side `AuthContext.HasPermission(...)` reads from
the JWT claims it already holds — no extra round-trips.

### Permission taxonomy

The set of permissions is fixed at the Central.Api level (the server
decides what permissions exist; the IDP just assigns them via groups).
Permission codes are owned by `libs/engine/Auth/Permissions.cs` and
seeded into a new `secure_auth.platform_permissions` table so admin
UIs can enumerate them.

---

## Phase 7 — OIDC provider mode

**Goal:** other applications (legacy internal tools, partner
integrations, maybe a mobile app later) can "Sign in with Central"
via standard OIDC.

New binary: `services/identity/federation/` on port 8084.

### Endpoints

- `GET /.well-known/openid-configuration` — discovery.
- `GET /jwks` — public key set for client JWT verification.
- `GET /oauth2/authorize` — browser redirect flow (renders our login
  page, prompts for consent).
- `POST /oauth2/token` — code exchange.
- `GET /oauth2/userinfo` — authenticated user profile.
- `POST /oauth2/revoke` — token revocation.

### Client registration

```sql
CREATE TABLE secure_auth.oidc_clients (
    client_id      varchar(128) PRIMARY KEY,
    client_secret_hash text,     -- Argon2; NULL for public clients (SPA / mobile)
    display_name   varchar(255),
    redirect_uris  text[] NOT NULL,
    allowed_scopes text[] NOT NULL DEFAULT '{openid,profile,email}',
    kind           varchar(16) NOT NULL DEFAULT 'confidential'
                     CHECK (kind IN ('confidential', 'public')),
    created_at     timestamptz NOT NULL DEFAULT now()
);
```

Admin-service (Phase 4) gains CRUD on `oidc_clients`.

---

## Phase 8 — WebAuthn + magic links

**Goal:** modern auth factors + passwordless sign-in.

### WebAuthn

- `webauthn-rs` crate.
- `secure_auth.webauthn_credentials` — one row per registered
  key/passkey per user.
- Endpoints: `/api/v1/auth/webauthn/register/start` +
  `/register/finish` + `/authenticate/start` + `/authenticate/finish`.
- Surfaces in the MFA methods list as `"method": "webauthn"` — same
  challenge flow as Phase C TOTP.

### Magic links

- `secure_auth.magic_link_tokens` (raw token SHA-256 stored, 15-min
  TTL, single-use).
- `POST /api/v1/auth/magic-link/request` — emails the user a signed
  URL.
- `GET /api/v1/auth/magic-link/redeem?token=...` — consumes + issues
  tokens.
- Shares the email-gateway crate with password-reset.

---

## Phase 9 — Admin UI + desktop integration

**Goal:** all the admin-service endpoints have a UI; desktop runs
entirely on JWTs from the IDP with an offline-mode fallback.

### Angular admin UI

New module under `apps/web/src/app/modules/identity/`:
- Users page (list, create, edit, lock/unlock, force-reset, revoke-
  sessions).
- Audit pages (logins / sessions / password-changes / SSO-callbacks).
- SSO provider admin (list, create, edit, test).
- Groups + permissions admin.
- OIDC-client admin (register, regen-secret, revoke).

### WPF desktop integration

Migrates the WPF path off of direct `app_users` queries onto the JWT
flow:

1. Startup: Windows SSO → `/api/v1/auth/sso/windows/callback` → JWT.
2. `CentralApiClient` carries the JWT on every call.
3. `AuthContext.CurrentUser` derives from JWT claims (previously from
   `app_users` row).
4. Offline mode: last-known JWT cached to
   `%LocalAppData%\Central\auth-cache.json` (AES-256 encrypted with
   the existing `CredentialEncryptor` key). On disconnect, use the
   cached JWT + permissions until it expires; then read-only.

Admin panels in the Global module read admin-service endpoints
instead of raw DB tables.

---

## Phase 10 — Deploy + harden

**Goal:** everything runs in K8s; CI enforces contract stability;
security posture is documented + tested.

### Deployment

- `Containerfile` for each identity binary (auth-service,
  admin-service, federation).
- `infra/k8s/base/identity-*.yaml` — Deployment + Service +
  ConfigMap + sealed-secrets for each.
- Separate Postgres pool per service (pgBouncer config).
- JWT signing key from a sealed secret; rotated quarterly with a
  grace-window design (`kid` header; accept old key for N days after
  rotation).

### Observability

- `/metrics` Prometheus endpoints on each service.
- Structured JSON logs (already enabled) ship to the existing Loki
  deployment.
- Dashboards: login success/failure rate, MFA challenge rate, lockout
  rate, token rotation rate, SSO-callback latency per provider.

### Security headers + CORS

- `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`,
  `Strict-Transport-Security`, `Content-Security-Policy` (strict —
  the identity service renders no HTML except the OIDC consent
  screen).
- CORS locked to the Angular origin + any registered OIDC client
  redirect URIs.

### CI engine-contract check

- A `services/identity/contract.json` checked into git, schema-
  version-stamped with each migration.
- CI step runs `cargo test contract_snapshot` — fails if the service
  surface drifts without a matching contract.json bump. Same pattern
  as `EngineContractBaselineTests` on the .NET side.

### Rate limits

- Tower middleware: per-IP on `/login`, `/password-reset/request`,
  `/mfa/verify`, `/sso/:provider/start`. Thresholds:
  - login: 10 / minute / IP
  - password-reset/request: 5 / hour / IP
  - sso: 30 / minute / IP (allows dashboard reloads)

---

## Dependencies between phases

```
Phase 1 (done) ─┐
                ├─> Phase 2 (unify stores) ──┐
                │                            ├─> Phase 3 (restructure) ──┐
                │                            │                           ├─> Phase 4 (admin API) ──┐
                │                            │                           │                         ├─> Phase 9 (admin UI)
                │                            │                           ├─> Phase 5 (Duo + OIDC)  │
                │                            │                           ├─> Phase 6 (RBAC)        │
                │                            │                           │                         │
                │                            │                           ├─> Phase 7 (OIDC provider)
                │                            │                           └─> Phase 8 (WebAuthn + magic links)
                │                            │
                │                            └─> Phase 9 (desktop integration on JWT)
                │
                └──────────────────────────────────────────────────────────> Phase 10 (deploy + harden)
```

Phase 10 can land in parallel with any others once Phase 3 sets up
the workspace shape.

## Global "would break the plan" risks

- **Desktop offline mode.** The current desktop falls back to
  `DirectDb` when the API is down; Phase 2 has to preserve that. The
  `user_external_identities` query the Windows-SSO bridge makes must
  also be readable directly by WPF for offline-mode identity
  resolution (same connection string).

- **WPF back-compat.** Every test that queries `app_users` has to
  keep passing. The view layer is the mitigation; Phase 2 fails fast
  if it regresses.

- **Session migration.** Live refresh tokens issued before Phase 2
  survive the migration (the `secure_auth.sessions` table stays put).
  JWTs issued to desktop users via Windows-SSO bridge are new-form —
  the old Windows-auth-bound "session" was implicit.

- **Duo + SAML2 without production credentials.** Phase 5 can't
  end-to-end verify without real IdP accounts. The mock paths from
  Phase 1 + Phase C.1 stay in place as fallbacks for local dev;
  Phase 5's commits include runbook docs for wiring real IdPs.

## What each phase explicitly DOESN'T do

Phase 2: doesn't drop `central_platform.global_users`. Phase 4 does that.

Phase 3: doesn't change any endpoint behaviour. Pure reorganisation.

Phase 4: doesn't build UI. That's Phase 9.

Phase 5: doesn't unify the Duo config model with the OIDC provider
model. They stay separate because their auth shapes genuinely differ.

Phase 6: doesn't migrate existing per-module permission data (e.g.
`role_permission_grants` on the Central side). That's a Phase 9 job
when the admin UI lets admins re-assign cleanly.

Phase 7: doesn't add UX for third-party consent approval. That's a
Phase 9 addition when the OIDC consent screen gets built.

Phase 8: doesn't deprecate password login. Both stay live.

Phase 9: doesn't remove the desktop's DirectDb offline mode. It
coexists with the JWT mode.

Phase 10: doesn't gate on all phases 2-9 being complete. The
deployment work can start as soon as Phase 3 lands.

## Acceptance summary

Each phase ends green on:
1. All live authentication flows continue to work (regression test).
2. The full .NET + networking-engine Rust test suites pass at
   baseline (11 known env-dependent infra probes aside).
3. The service's cargo test (where it has one) passes.
4. Phase's acceptance criteria (listed in its section above) all
   hit.
5. This doc's phase table flips the status column.

## Ownership

This plan is per-phase additive; no phase rewrites earlier phases'
contracts. Engine-contract check (Phase 10) encodes the rule into CI
so it stays honest.
