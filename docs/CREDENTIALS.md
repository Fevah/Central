# Central Platform — Credentials & Access

Last updated: 2026-04-16

---

## Security Model

**Credentials are stored in environment variables, NOT in source code.**

On the host machine (UK-Fevah), credentials are set as persistent user-level env vars:
- `CENTRAL_DSN` — PostgreSQL connection string
- `AUTH_DSN` — Auth database connection string
- `REDIS_PASSWORD` — Redis auth password
- `CENTRAL_PG_PASSWORD` — Raw PG password for setup.sh

**Remote access** is via VS Code Remote Tunnels (encrypted, Microsoft-authenticated).
DB/API ports are forwarded through the tunnel — never exposed directly to the network.

---

## Quick Reference

| What | User/Email | Password | URL/Host |
|------|-----------|----------|----------|
| **WPF Desktop** | (Windows auto-login) | (none needed) | `$CENTRAL_DSN` |
| **Angular Web** | centraladmin@central.local | Central-Adm1n-2026! | http://localhost:4200 |
| **FastAPI Web** | admin | admin | http://localhost:8080 |
| **API Swagger** | (no auth for /swagger) | — | http://localhost:5000/swagger |
| **PostgreSQL** | central | `$CENTRAL_PG_PASSWORD` | localhost:5432 (tunnel) |
| **Redis** | — | `$REDIS_PASSWORD` | localhost:6379 |
| **Switch SSH** | root | admin123 | management IPs below |

---

## Remote Development Access

**Method: VS Code Remote Tunnels (recommended)**

The host machine runs a VS Code tunnel service named `uk-fevah`.
Connect from any machine:

1. VS Code Desktop → "Remote Tunnels: Connect to Tunnel" → `uk-fevah`
2. Browser → https://vscode.dev → "Remote Tunnels: Connect to Tunnel" → `uk-fevah`

Once connected, these ports are auto-forwarded to your local machine:
| Port | Service | Auto-Forward |
|------|---------|-------------|
| 5432 | PostgreSQL | Silent |
| 6379 | Redis | Silent |
| 5000 | Central API | Silent |
| 8080 | FastAPI | Notify |
| 8081 | Auth Service | Silent |
| 4200 | Angular Dev | Notify |

**Connect to PG from remote machine** (after tunnel):
```
psql -h localhost -p 5432 -U central -d central
# Password: from $CENTRAL_PG_PASSWORD env var on host
```

---

## WPF Desktop App

**Launch:**
```powershell
# CENTRAL_DSN is already set as a persistent env var
desktop\Central.Desktop\bin\x64\Release\net10.0-windows\Central.exe
```

**API routing:** All API calls go through the gateway at `http://192.168.56.203:8000`.
Gateway routes `/api/*` to Central.Api, `/api/v1/auth/*` to auth-service, `/api/v1/tasks/*` to task-service, etc.

**API auto-connect:** Enable in backstage Settings > Server > Auto-connect = true. Default URL: `http://192.168.56.203:8000`.

**Auth method:** Windows auto-login — matches your Windows username (`cory.sharplin`) to `app_users.username`. No password needed.

**If auto-login fails:** LoginWindow appears with two options:
1. **Auth-service login** (primary): email + password → auth-service (Rust) at `192.168.56.10:30081`
   - Email: `centraladmin@central.local` / Password: `Central-Adm1n-2026!`
   - Supports MFA (TOTP) — if enabled, prompts for 6-digit code after password
2. **Local DB fallback**: username + password verified against `app_users.password_hash`

The DB user `cory.sharplin` has role `Admin` (priority 1000 = SuperAdmin).

---

## Angular Web Client

**URL:** http://localhost:4200

**Start dev server:**
```bash
cd web-client && npx ng serve --open
```

**Login credentials:**

The web client logs in via the Rust **auth-service** (Phase A as of 2026-04-20 — see
[docs/AUTH_SERVICE_BUILDOUT.md](AUTH_SERVICE_BUILDOUT.md) for the phased plan).
Seeded accounts in `secure_auth.users`:

| Account | Password | Role |
|---------|----------|------|
| `corys@central.local` | `corys-dev-pass!` | global_admin |

The password hash is stored as Argon2id in `secure_auth.users.password_hash`;
to seed an additional account, run:

```bash
cd services/identity && cargo run -p auth-service --example hash_password -- 'your-password'
# Copy the printed hash into a follow-up migration INSERT.
```

**NOTE:** The older `centraladmin@central.local` / `Central-Adm1n-2026!` row
referenced here historically never existed in any migration. `admin@central.local`
with password `admin` is seeded in `central_platform.global_users` by
[migration 027](../db/migrations/027_global_admin.sql) but that is a different
table used by the **desktop** global-admin flow, not the web login.

**Auth flow:**

1. Web client POSTs `{email, password}` with `X-Tenant-ID` header to
   `${authServiceUrl}/api/v1/auth/login`.
2. `authServiceUrl` resolves to `http://192.168.56.10:30081` in prod / K8s
   (NodePort) and `http://localhost:8081` for local dev (see
   [config/auth-service.toml](../config/auth-service.toml)).
3. auth-service Argon2-verifies against `secure_auth.users`, signs a JWT,
   returns the `LoginResponse` shape the Angular client expects.

**Running auth-service locally:**

```bash
# From repo root. Reads config/auth-service.toml.
export AUTH_SERVICE_JWT_SECRET='dev-only-change-me'   # required for real use
cd services/identity && cargo run -p auth-service
```

Phase A only implements `/api/v1/auth/login` + `/health`. Refresh / logout /
MFA return 501/204 placeholders until Phases B + C ship.

---

## FastAPI/HTMX Web App

**URL:** http://localhost:8080

**Start:**
```bash
export CENTRAL_DSN="postgresql://central:central@192.168.56.10:30432/central"
python -m uvicorn web.app:app --host 127.0.0.1 --port 8080
```

**Login:** HTTP Basic Auth
- Username: `admin`
- Password: `admin`

Override via env vars: `CENTRAL_USER` / `CENTRAL_PASS`

---

## API Server (K8s)

**Swagger UI:** http://192.168.56.200:5000/swagger
- No auth required to browse
- Bearer JWT required for API calls

**Health check (no auth):**
- http://192.168.56.200:5000/api/health

**Get JWT token:**
```bash
curl -X POST http://192.168.56.10:30081/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: 00000000-0000-0000-0000-000000000001" \
  -d '{"email":"centraladmin@central.local","password":"Central-Adm1n-2026!"}'
```

**Use JWT:**
```bash
curl -H "Authorization: Bearer <token>" http://192.168.56.200:5000/api/devices
```

---

## Auth Service (Rust)

**Health:** http://192.168.56.10:30081/health

**Endpoints:**
- POST `/api/v1/auth/login` — email + password → JWT
- POST `/api/v1/auth/register` — create new user
- POST `/api/v1/auth/refresh` — refresh JWT
- POST `/api/v1/auth/logout` — revoke session

**Default tenant ID:** `00000000-0000-0000-0000-000000000001`

---

## PostgreSQL

**K8s access (from Windows host):**
```
Host: 192.168.56.10
Port: 30432
Username: central
Password: central
```

**Databases:**
| Database | Purpose | Tables |
|----------|---------|--------|
| `central` | Main app data (devices, switches, links, tasks, etc.) | 128+ |
| `secure_auth` | Auth-service (users, sessions, roles, MFA) | 26 |

**Connect:**
```bash
psql -h 192.168.56.10 -p 30432 -U central -d central
psql -h 192.168.56.10 -p 30432 -U central -d secure_auth
```

**Or via kubectl:**
```bash
export KUBECONFIG=~/.kube/central-local.conf
kubectl -n central exec -it postgres-0 -- psql -U central -d central
```

**Desktop app DSN:**
```
Host=192.168.56.10;Port=30432;Database=central;Username=central;Password=central
```

---

## Switch SSH (PicOS)

| Switch | Management IP (VLAN-152) | SSH User | SSH Password | SSH Port |
|--------|--------------------------|----------|-------------|----------|
| MEP-91-CORE02 | 10.11.152.2 | root | admin123 | 22 |
| MEP-92-CORE01 | 10.12.152.1 | root | admin123 | 22 |
| MEP-93-L1-CORE02 | 10.13.152.2 | root | admin123 | 22 |
| MEP-94-CORE01 | 10.14.152.1 | root | admin123 | 22 |
| MEP-96-L2-CORE02 | 10.16.152.2 | root | admin123 | 22 |

---

## K8s Cluster

**Kubeconfig:** `~/.kube/central-local.conf`

```bash
export KUBECONFIG=~/.kube/central-local.conf
kubectl get nodes
kubectl -n central get pods
```

**VM SSH (via Vagrant):**
```bash
cd infra/vagrant
vagrant ssh k8s-master
vagrant ssh k8s-worker-01
```

**Container registry:** `192.168.56.10:30500` (insecure, no auth)

---

## Service Endpoints Summary

| Service | Internal (K8s) | External (from Windows) | Via Gateway |
|---------|---------------|------------------------|-------------|
| **API Gateway** | gateway:8000 | http://192.168.56.203:8000 | — |
| Central API | central-api:5000 | http://192.168.56.200:5000 | /api/* |
| Auth Service | auth-service:8081 | http://192.168.56.10:30081 | /api/v1/auth/* |
| Task Service | task-service:8085 | (internal) | /api/v1/tasks/* |
| Storage Service | storage-service:8084 | (internal) | /api/v1/storage/* |
| Sync Service | sync-service:8083 | (internal) | /api/v1/sync/* |
| Audit Service | audit-service:8082 | (internal) | /api/v1/audit/* |
| Admin Service | admin-service:8080 | (internal) | /api/v1/admin/* |
| MinIO (S3) | minio:9000 | (internal) | — |
| PostgreSQL | postgres:5432 | 192.168.56.10:30432 | — |
| Redis | redis:6379 | (internal) | — |
| Container Registry | — | 192.168.56.10:30500 | — |

**Gateway is the single entry point.** All API calls can go through `http://192.168.56.203:8000`.

---

## Default Tenant

- **ID:** `00000000-0000-0000-0000-000000000001`
- **Name:** Default
- **Tier:** free (auth-service V013 seed)
