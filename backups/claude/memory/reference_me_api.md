---
name: ManageEngine SDP API reference
description: ME API quirks — Zoho EU OAuth, fields_required for priority, completed_time for resolved_at, refresh token rotation, group categories
type: reference
---

## ManageEngine ServiceDesk Plus On-Demand API

- **API base**: `https://sdpondemand.manageengine.eu/api/v3/`
- **OAuth**: Zoho EU — `https://accounts.zoho.eu/oauth/v2/token`
- **Auth header**: `Authorization: Zoho-oauthtoken {access_token}`
- **Accept header**: `application/vnd.manageengine.sdp.v3+json`
- **Portal URL** (for ticket links): `https://itsupport.immunocore.com`
- **Ticket link format**: `{portal_url}/app/itdesk/ui/requests/{id}/details`

### Critical quirks
- `priority`, `urgency`, `impact` are NOT in the default list response — **MUST** use `fields_required` in `list_info` to include them
- `completed_time` is the actual resolution timestamp — use for `resolved_at`, NOT `synced_at` (which is when we synced). If `completed_time` is null for a Resolved status, fall back to `updated_time`.
- Status "Resolved" and "Closed" are both treated as "closed" in all metrics — unified in queries and charts
- Auth codes from Zoho Self Client expire in **60 seconds** — exchange immediately for refresh token
- Zoho **may rotate refresh tokens** — always capture the new `refresh_token` from the token response and persist it back to the DB. Missing this causes auth to break silently on next refresh cycle.
- Custom domain (`itsupport.immunocore.com`) doesn't work for API calls — use `sdpondemand.manageengine.eu`
- `status.name` returns display name (Open, Closed, etc.), `status.internal_name` is the system name
- Group names come from `group.name` in the API response
- `sd_group_categories` table maps parent categories to ME group names (for nested group filtering in UI)

### Zoho credentials location
- Zoho API Console: `api-console.zoho.eu` > Self Client
- Client ID: `1000.EDISWAA62PA36A8P1UIV7DHGS02VLG`
- Scopes: `SDPOnDemand.requests.ALL,SDPOnDemand.setup.ALL` (and others)

### Refresh token auto-rotation flow
1. Exchange refresh token for new access token via `https://accounts.zoho.eu/oauth/v2/token`
2. Response may include a new `refresh_token` — if present, persist it to `integration_credentials` table
3. If refresh fails (e.g., expired/revoked token), generate a new auth code from Zoho Self Client (expires in 60s) and exchange for a new refresh token
4. Store encrypted credentials via `CredentialEncryptor` (AES-256)
