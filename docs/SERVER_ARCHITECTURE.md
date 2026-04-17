# Central Server Architecture

Multi-user infrastructure replacing WCF (TotalLink) with modern .NET 8 services.
gRPC for switch operations, REST for CRUD, SignalR for real-time push.
All containerised in Podman pods.

---

## 1. Pod Layout

Single Podman pod, four containers, shared network namespace (localhost).

```
Pod: central
  Published: 5432 (postgres), 5000 (REST+SignalR), 5001 (gRPC), 7472 (web)

  ┌────────────────────────────────────────────┐
  │  postgres (postgres:16-alpine)              │
  │  Port: 5432                                 │
  │  Volume: central-pgdata               │
  │  Init: db/schema.sql + db/migrations/*.sql  │
  └────────────────────────────────────────────┘

  ┌────────────────────────────────────────────┐
  │  api (central-api:latest)             │
  │  ASP.NET Core 8                             │
  │  Port: 5000 (REST + SignalR)                │
  │  Port: 5001 (gRPC)                          │
  │  Background: ping, config backup, BGP sync  │
  │  SSH access to switches (10.x.152.x)        │
  └────────────────────────────────────────────┘

  ┌────────────────────────────────────────────┐
  │  web (central-web:latest)             │
  │  Python FastAPI + HTMX (existing)           │
  │  Port: 7472                                 │
  │  Reads from DB + calls API for auth         │
  └────────────────────────────────────────────┘

  ┌────────────────────────────────────────────┐
  │  pgbouncer (optional — Phase 5)             │
  │  Connection pooling for API + Web           │
  │  Port: 6432 → postgres:5432                 │
  └────────────────────────────────────────────┘
```

All containers talk via `localhost` inside the pod. Only published ports
are visible from the host machine.

---

## 2. Service Protocols

| Protocol | Port | Use Case | Why |
|----------|------|----------|-----|
| REST (JSON) | 5000 | CRUD operations, admin, config gen | Simple, debuggable, curl-friendly |
| SignalR | 5000/hubs | Real-time push to WPF clients | DataChanged, PingResult, SyncProgress |
| gRPC | 5001 | Switch operations (ping, SSH, sync) | Native .NET streaming, typed, binary |

### Why all three

- **REST**: Standard CRUD. The 2,400-line `DbRepository` becomes API controllers. Debuggable with browser/curl. Works with the FastAPI web app.
- **gRPC**: Streaming switch operations. `SyncBgp` streams progress per-switch in real time. `DownloadConfig` streams SSH log entries as they happen. Native .NET, strongly typed via protobuf.
- **SignalR**: Push notifications. When User A saves a device, all connected clients get `DataChanged("device", id, "update")`. Auto-reconnects, native .NET.

---

## 3. API Project Structure

```
Central.Api/
├── Program.cs                          # Host builder, DI, auth, middleware
├── appsettings.json                    # DSN, JWT, job schedules
├── Dockerfile
├── Endpoints/                          # Minimal API route groups
│   ├── DeviceEndpoints.cs              # /api/devices
│   ├── SwitchEndpoints.cs              # /api/switches
│   ├── LinkEndpoints.cs                # /api/links (P2P, B2B, FW)
│   ├── BgpEndpoints.cs                 # /api/bgp
│   ├── VlanEndpoints.cs                # /api/vlans
│   ├── ConfigEndpoints.cs              # /api/config (generate, versions, compare)
│   ├── AdminEndpoints.cs               # /api/admin (users, roles, lookups, audit)
│   ├── AuthEndpoints.cs                # /api/auth (login, refresh, whoami)
│   ├── IdentityProviderEndpoints.cs    # /api/identity (providers, domain mappings, claim mappings, auth events)
│   ├── AppointmentEndpoints.cs         # /api/appointments (CRUD, resources)
│   ├── LocationEndpoints.cs            # /api/locations (countries, regions, references)
│   └── BackupEndpoints.cs              # /api/backup (run, history, migrations, purge)
├── Hubs/
│   └── NotificationHub.cs             # SignalR
├── GrpcServices/
│   └── SwitchOpsService.cs            # Ping, SSH, BGP sync (streaming)
├── BackgroundServices/
│   ├── ScheduledPingService.cs        # Ping all switches every N minutes
│   ├── ConfigBackupService.cs         # Nightly config download
│   ├── BgpSyncScheduler.cs            # Periodic BGP refresh
│   └── ChangeNotifier.cs             # pg_notify → SignalR broadcast
├── Auth/
│   ├── JwtService.cs                  # Issue/validate JWT
│   ├── WindowsAuthHandler.cs          # NTLM for domain environments
│   └── RequirePermissionAttribute.cs  # [RequirePermission("devices:write")]
└── Middleware/
    ├── SiteAccessFilter.cs            # Inject allowed_sites per request
    └── AuditMiddleware.cs             # Log all writes to audit_log
```

### API Client Library (for WPF)

```
Central.Api.Client/
├── ApiClientOptions.cs                 # BaseUrl, timeout, JWT storage
├── IApiClient.cs
├── ApiClient.cs
├── Services/
│   ├── DeviceApiClient.cs
│   ├── SwitchApiClient.cs
│   ├── LinkApiClient.cs
│   ├── BgpApiClient.cs
│   ├── ConfigApiClient.cs
│   └── AuthApiClient.cs
└── Realtime/
    ├── NotificationClient.cs          # SignalR connection + events
    └── SwitchOpsGrpcClient.cs         # gRPC streaming wrapper
```

### Shared Protobuf Definitions

```
Central.Protos/
└── Protos/
    ├── switch_ops.proto               # Ping, SSH, BGP sync (server streaming)
    └── config.proto                   # Config generation streaming
```

---

## 4. Authentication Flow

### JWT with Windows Auto-Login

```
WPF Client                        API Server
    │                                 │
    ├─ POST /api/auth/login ─────────>│
    │  { username: "cory.sharplin" }  │
    │                                 ├─ Lookup app_users by username
    │                                 ├─ Load role + permissions + sites
    │                                 ├─ Issue JWT with claims:
    │                                 │    sub, role, sites[], permissions[]
    │<─ { token, expiresAt } ─────────┤
    │                                 │
    ├─ GET /api/devices ─────────────>│  (Bearer token)
    │  Authorization: Bearer <jwt>    ├─ Validate JWT
    │                                 ├─ Extract sites from claims
    │                                 ├─ WHERE building = ANY(@sites)
    │<─ [ device1, device2... ] ──────┤
```

- JWT expires in 8 hours (work day), silent refresh via `SessionRefreshService` (20min timer)
- WPF stores token in memory only (not disk)
- Offline mode: if API unreachable, fallback to direct Npgsql + `AuthContext.SetOfflineAdmin()`

### Multi-Provider Authentication (Enterprise)

5 auth providers, all converging to the same JWT/session model:

| Provider | Protocol | Desktop Flow |
|---|---|---|
| Windows | NTLM/Kerberos | `Environment.UserName` → DB lookup → SetSession |
| Local Password | SHA256+salt | Username/password → PasswordHasher.Verify → SetSession |
| Microsoft Entra ID | OIDC+PKCE | System browser → localhost callback → JWT claims → JIT provision |
| Okta | OIDC+PKCE | System browser → localhost callback → JWT claims → JIT provision |
| SAML2/Duo | SAML+MFA | System browser → ACS POST → assertion parse → optional Duo prompt |

Architecture:
```
LoginWindow → AuthenticationService.AuthenticateAsync(providerId, request)
    → IAuthenticationProvider.AuthenticateAsync() [provider-specific]
    → AuthenticationResult (normalized claims)
    → ClaimsMappingService.MapClaimsToRoleAsync() [DB-driven rules]
    → UserProvisioningService.FindOrProvisionUserAsync() [JIT creation]
    → AuthContext.SetSession(user, perms, sites, authState)
```

Key tables: `identity_providers`, `idp_domain_mappings`, `claim_mappings`, `user_external_identities`, `auth_events`

IdP discovery: user enters email → domain extracted → `idp_domain_mappings` lookup → route to correct provider.
Brute-force lockout: 5 failed attempts → 30min lock (`failed_login_count`, `locked_until` on `app_users`).

### Permission Middleware

```csharp
// On each request:
[RequirePermission("devices:write")]
app.MapPut("/api/devices/{id}", async (int id, DeviceRecord d, IAuthContext auth) =>
{
    // auth.HasPermission already checked by middleware
    // auth.AllowedSites used to filter queries
    ...
});
```

---

## 5. Real-Time Updates (Multi-User)

### PostgreSQL LISTEN/NOTIFY → SignalR

```sql
-- Trigger on all key tables
CREATE OR REPLACE FUNCTION notify_change() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('central_changes',
        json_build_object(
            'table', TG_TABLE_NAME,
            'action', TG_OP,
            'id', COALESCE(NEW.id, OLD.id)
        )::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_devices_notify AFTER INSERT OR UPDATE OR DELETE
    ON switch_guide FOR EACH ROW EXECUTE FUNCTION notify_change();
-- Repeat for: p2p_links, b2b_links, fw_links, switches, bgp_config, vlan_inventory
```

### ChangeNotifier Background Service

```
PostgreSQL ──pg_notify──> ChangeNotifier ──SignalR──> All WPF Clients
                                                          │
                                                          ├─ DataChanged("switch_guide", 42, "UPDATE")
                                                          ├─ User A's grid refreshes automatically
                                                          └─ User B sees the change within 1s
```

### SignalR Hub Events

| Event | Payload | Trigger |
|-------|---------|---------|
| `DataChanged` | `(table, id, action)` | Any DB write via pg_notify |
| `PingResult` | `(hostname, ok, latencyMs)` | ScheduledPingService |
| `SyncProgress` | `(hostname, stage, message)` | BGP sync / config download |
| `ConfigDeployed` | `(hostname, success, timestamp)` | Config push to switch |

---

## 6. gRPC Switch Operations

```protobuf
// switch_ops.proto

service SwitchOps {
    // Single switch ping
    rpc Ping (PingRequest) returns (PingResponse);

    // Ping all switches — server streams results as each completes
    rpc PingAll (PingAllRequest) returns (stream PingResponse);

    // Download running config via SSH — streams log entries
    rpc DownloadConfig (SshRequest) returns (stream SshLogEntry);

    // Sync BGP from switch — streams progress per-step
    rpc SyncBgp (SyncRequest) returns (stream SyncProgress);

    // Push config to switch — streams command results
    rpc DeployConfig (DeployRequest) returns (stream DeployProgress);
}

message PingRequest { string hostname = 1; string management_ip = 2; }
message PingResponse { string hostname = 1; bool ok = 2; int32 latency_ms = 3; }
message SshRequest { string hostname = 1; string management_ip = 2; }
message SshLogEntry { string timestamp = 1; string line = 2; bool is_error = 3; }
message SyncRequest { string hostname = 1; }
message SyncProgress { string hostname = 1; string stage = 2; string message = 3; bool done = 4; }
message DeployRequest { string hostname = 1; repeated string commands = 2; }
message DeployProgress { string command = 1; bool ok = 2; string response = 3; }
```

**Key advantage**: gRPC streaming means the WPF client sees real-time SSH output
as it happens, not just a final result. The UX is equivalent to watching an SSH
terminal — each line streams back as the switch responds.

---

## 7. Background Services

| Service | Schedule | What |
|---------|----------|------|
| `ScheduledPingService` | Every 5 min | Ping all switches, update DB, broadcast via SignalR |
| `ConfigBackupService` | Nightly 02:00 | SSH download running config from all switches |
| `BgpSyncScheduler` | Every 30 min | Sync BGP tables from core switches |
| `ChangeNotifier` | Continuous | pg_notify listener → SignalR broadcast |

All services are `IHostedService` implementations running in the API container.
No separate worker container needed at this scale.

---

## 8. Infrastructure as Code

### Pod Definition

```yaml
# infra/pod.yaml
apiVersion: v1
kind: Pod
metadata:
  name: central
  labels:
    app: central
spec:
  restartPolicy: Always
  containers:
    - name: postgres
      image: docker.io/library/postgres:16-alpine
      ports:
        - containerPort: 5432
          hostPort: 5432
      env:
        - name: POSTGRES_DB
          value: central
        - name: POSTGRES_USER
          value: central
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: central-db-password
              key: password
        - name: PGDATA
          value: /var/lib/postgresql/data/pgdata
      volumeMounts:
        - name: pgdata
          mountPath: /var/lib/postgresql/data
        - name: initdb
          mountPath: /docker-entrypoint-initdb.d
          readOnly: true

    - name: api
      image: localhost/central-api:latest
      ports:
        - containerPort: 5000
          hostPort: 5000
        - containerPort: 5001
          hostPort: 5001
      env:
        - name: CENTRAL_DSN
          value: "Host=localhost;Port=5432;Database=central;Username=central;Password=central"
        - name: ASPNETCORE_URLS
          value: "http://+:5000;http://+:5001"
        - name: JWT_SECRET
          valueFrom:
            secretKeyRef:
              name: central-jwt-secret
              key: secret
        - name: PING_INTERVAL_MINUTES
          value: "5"
        - name: CONFIG_BACKUP_CRON
          value: "0 2 * * *"

    - name: web
      image: localhost/central-web:latest
      ports:
        - containerPort: 7472
          hostPort: 7472
      env:
        - name: CENTRAL_DSN
          value: "postgresql://central:central@localhost:5432/central"
        - name: CENTRAL_API
          value: "http://localhost:5000"

  volumes:
    - name: pgdata
      persistentVolumeClaim:
        claimName: central-pgdata
    - name: initdb
      hostPath:
        path: ./db
        type: Directory
```

### Secrets

```bash
# Create secrets (one-time)
echo -n "central" | podman secret create central-db-password -
openssl rand -base64 32 | podman secret create central-jwt-secret -
```

### Build & Deploy Script

```bash
# infra/setup.sh extensions

build-api)
    podman build -t central-api:latest \
        -f services/api/Dockerfile \
        
    ;;

build-web)
    podman build -t central-web:latest web/
    ;;

build)
    $0 build-api
    $0 build-web
    ;;

deploy)
    podman play kube --replace infra/pod.yaml
    ;;

logs-api)
    podman logs -f central-api
    ;;
```

---

## 9. WPF Client Dual-Mode

The WPF client must work in three modes:

| Mode | Data Source | Auth | Real-Time |
|------|-----------|------|-----------|
| **API** (default) | REST → API → PostgreSQL | JWT | SignalR + gRPC |
| **Direct DB** (fallback) | Npgsql → PostgreSQL | AuthContext offline | None |
| **Offline** (no DB, no API) | Cached data (read-only) | Admin fallback | None |

### IDataService Abstraction

```csharp
// libs/engine/Data/IDataService.cs
public interface IDataService
{
    // Mirrors DbRepository methods — same signatures
    Task<List<DeviceRecord>> GetDevicesAsync(List<string>? sites = null);
    Task InsertDeviceAsync(DeviceRecord d);
    Task UpdateDeviceAsync(DeviceRecord d);
    Task DeleteDeviceAsync(int id);
    // ... all other domain methods
}

// Two implementations:
// 1. DirectDbDataService — wraps existing DbRepository (unchanged)
// 2. ApiDataService — wraps Central.Api.Client
```

### ConnectivityManager Evolution

```csharp
public enum ConnectionMode { Api, DirectDb, Offline }

public class ConnectivityManager
{
    public ConnectionMode Mode { get; private set; }

    public async Task InitialiseAsync()
    {
        // Try API first
        if (await TestApiAsync("http://localhost:5000/health"))
        {
            Mode = ConnectionMode.Api;
            return;
        }

        // Fallback to direct DB
        if (await TestDbAsync())
        {
            Mode = ConnectionMode.DirectDb;
            return;
        }

        // Offline
        Mode = ConnectionMode.Offline;
        StartRetryLoop();
    }
}
```

---

## 10. Concurrency Model

**Last-write-wins + notification** (appropriate for 2-5 operators):

1. User A loads device grid
2. User B edits device 42, saves via API
3. API writes to DB, pg_notify fires
4. ChangeNotifier broadcasts `DataChanged("switch_guide", 42, "UPDATE")` via SignalR
5. User A's client receives event, refreshes device 42 in the grid
6. User A sees the change within ~1 second

**Future (if needed)**: Optimistic concurrency via `updated_at` timestamp. API returns
HTTP 409 Conflict if the row changed since the client loaded it.

---

## 11. Migration Path

### Phase A: API Server (no WPF changes)

Create `Central.Api`, containerise, deploy alongside existing pod.
API is fully functional but WPF still uses direct DB.

### Phase B: API Client + Dual-Mode WPF

Create `Central.Api.Client`, add `IDataService` abstraction,
WPF can switch between API and direct DB.

### Phase C: Server-Side Switch Operations

Move ping/SSH/BGP sync to API via gRPC. SSH credentials stay server-side.
WPF no longer needs `SSH.NET` dependency.

### Phase D: Background Jobs

Add scheduled services. Automated config backup, periodic ping,
BGP sync. Admin UI for job management.

### Phase E: Retire Direct DB from WPF

Remove `DbRepository` from Desktop. WPF exclusively uses API.
Database credentials no longer distributed to clients.

---

## 12. Network Requirements

The API container needs to reach the switch management network:

| Source | Destination | Protocol | Purpose |
|--------|------------|----------|---------|
| API container | 10.x.152.x | ICMP | Ping switches |
| API container | 10.x.152.x:22 | TCP/SSH | Config download, BGP sync |
| WPF clients | localhost:5000 | HTTP | REST + SignalR |
| WPF clients | localhost:5001 | HTTP/2 | gRPC |
| WPF clients | localhost:5432 | TCP | Direct DB fallback |

Since the Podman pod runs in WSL2, the API container shares the WSL2 network
which has access to the host's network routes. If switches are on a separate VLAN,
ensure the Windows host has routing to 10.x.152.x/24 management subnets.
