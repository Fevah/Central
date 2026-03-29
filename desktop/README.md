# Central Desktop Platform

Enterprise infrastructure management platform — .NET 10 / WPF / DevExpress 25.2 / PostgreSQL 18.3.

## Quick Start

```powershell
# 1. Start database
podman play kube ../infra/pod.yaml

# 2. Setup database (first time only)
.\db\setup.ps1
# Or on Linux/Mac: ./db/setup.sh

# 3. Build
dotnet build Central.sln -c Release -p:Platform=x64

# 4. Run desktop app
cd Central.Desktop/bin/x64/Release/net10.0-windows
./Central.exe

# 5. Run API server (optional, for multi-user)
cd Central.Api
dotnet run
# Swagger: http://localhost:5000/swagger

# 6. Run tests
dotnet test Central.Tests -c Release
```

## Solution Structure (14 projects)

| Project | Purpose |
|---|---|
| `Central.Core` | Engine framework — auth, models, services, integration |
| `Central.Data` | PostgreSQL repos (14 partial classes) |
| `Central.Api` | REST API (29 endpoint groups) + SignalR |
| `Central.Api.Client` | Typed HTTP + SignalR client |
| `Central.Desktop` | WPF shell — MainWindow, auth, services |
| `Central.Module.Devices` | IPAM: devices, ASN, servers |
| `Central.Module.Switches` | Switch management + deploy |
| `Central.Module.Links` | P2P, B2B, FW links + builder |
| `Central.Module.Routing` | BGP + network diagram |
| `Central.Module.VLANs` | VLAN inventory |
| `Central.Module.Admin` | 31 admin views — full enterprise admin |
| `Central.Module.Tasks` | Task management with tree hierarchy |
| `Central.Module.ServiceDesk` | ManageEngine SD integration |
| `Central.Tests` | 329 unit tests |

## Key Features

- **5 Auth Providers**: Windows, Local Password, Microsoft Entra ID, Okta, SAML2/Duo
- **TOTP MFA** with QR enrollment + recovery codes
- **Password Policy**: complexity, expiry, history, brute-force lockout
- **Sync Engine**: 3 agents (ManageEngine, CSV, REST API), 7 field converters
- **Enterprise Mediator**: typed pub/sub with pipeline behaviors + cross-panel linking
- **Audit Trail**: before/after JSONB snapshots on all CRUD operations
- **Grid Customizer**: 10 right-click items on all 22 grids
- **Panel Floating**: drag any panel to a second monitor
- **Dashboard**: KPI cards for platform, service desk, system health
- **29 API Endpoints** with Swagger, rate limiting, API key auth, security headers
- **14 Keyboard Shortcuts**: Ctrl+N/S/Z/Y/F/I/E/P/D/G/R, F5, Delete, Ctrl+Tab

## Default Login

- **Username**: admin
- **Password**: admin (change immediately via Set Password)

## Environment Variables

| Variable | Purpose | Default |
|---|---|---|
| `CENTRAL_DSN` | PostgreSQL connection string | localhost |
| `CENTRAL_DSN` | Fallback DSN | localhost |
| `CENTRAL_CREDENTIAL_KEY` | AES-256 encryption key | machine-derived |

## Database Migrations

55 migrations in `db/migrations/` (026-055). Auto-applied on startup.

## API Documentation

Swagger UI at `http://localhost:5000/swagger` when API server is running.
Health check (no auth): `GET /api/health`
