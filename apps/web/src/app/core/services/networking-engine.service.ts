import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/// One ranked hit from the engine's global search.
/// Matches `SearchResult` in services/networking-engine/src/search.rs
/// (camelCase on the wire via `#[serde(rename_all = "camelCase")]`).
export interface SearchResult {
  entityType: string;
  id: string;
  label: string;
  rank: number;
  snippet: string;
}

/// Saved search — per-user named query state. Matches
/// `SavedViewDto` in the engine's saved_views module.
export interface SavedView {
  id: string;
  organizationId: string;
  userId: number;
  name: string;
  q: string;
  entityTypes: string | null;
  filters: unknown;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  notes: string | null;
}

/// Audit row — matches `AuditRowDto` from the engine.
export interface AuditRow {
  sequenceId: number;
  createdAt: string;
  entityType: string;
  entityId: string | null;
  action: string;
  actorUserId: number | null;
  actorDisplay: string | null;
  correlationId: string | null;
  details: unknown;
}

/// Thin device-list row — matches `DeviceListRow` in the engine's
/// list_devices handler. Used by callers that need hostname → uuid
/// resolution (e.g. device-detail auditing where the WPF/legacy
/// model carries switch_guide's numeric id, not net.device.id).
export interface DeviceListRow {
  id: string;
  hostname: string;
  roleCode: string | null;
  buildingCode: string | null;
  status: string;
  version: number;
}

/// One violation from the validation rule engine. Matches `Violation`
/// in services/networking-engine/src/validation.rs.
export interface Violation {
  ruleCode: string;
  severity: 'Error' | 'Warning' | 'Info';
  entityType: string;
  entityId: string | null;
  message: string;
}

/// Outcome of a validation run. Matches `ValidationRunResult`.
export interface ValidationRunResult {
  violations: Violation[];
  rulesRun: number;
  rulesWithFindings: number;
  totalViolations: number;
}

/// Thin Phase-10 surface for the Angular web client. Parallel to
/// the WPF `NetworkingEngineClient`; covers the operator-facing
/// endpoints (search, saved views, audit timeline) that web
/// features consume. Bulk / scope-grants / CRUD are out of scope
/// for this slice — they'd land alongside matching components.
@Injectable({ providedIn: 'root' })
export class NetworkingEngineService {
  private readonly base = environment.networkingEngineUrl;

  constructor(private http: HttpClient) {}

  /// Full-text search across 6 tenant-owned entities. Empty q →
  /// empty result set server-side, matches the engine contract.
  search(
    organizationId: string,
    q: string,
    entityTypes?: string[],
    limit?: number,
  ): Observable<SearchResult[]> {
    let params = new HttpParams()
      .set('organizationId', organizationId)
      .set('q', q);
    if (entityTypes && entityTypes.length > 0) {
      params = params.set('entityTypes', entityTypes.join(','));
    }
    if (limit !== undefined) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<SearchResult[]>(`${this.base}/api/net/search`, { params });
  }

  /// List the caller's saved views (X-User-Id scoped on the engine
  /// side; service calls without the header get an empty list).
  listSavedViews(organizationId: string): Observable<SavedView[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<SavedView[]>(`${this.base}/api/net/saved-views`, { params });
  }

  /// Thin device list — capped at 5000 rows per tenant. Used by
  /// callers needing hostname → net.device uuid resolution (the
  /// WPF grid's selectId handler does the same thing).
  listDevices(organizationId: string): Observable<DeviceListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<DeviceListRow[]>(`${this.base}/api/net/devices`, { params });
  }

  /// Run the validation rule engine. Empty `ruleCode` runs every
  /// enabled rule; a specific code runs just that rule (useful for
  /// fix-it + re-run-to-confirm flows).
  runValidation(organizationId: string, ruleCode?: string): Observable<ValidationRunResult> {
    const body: Record<string, unknown> = { organizationId };
    if (ruleCode) body['ruleCode'] = ruleCode;
    return this.http.post<ValidationRunResult>(`${this.base}/api/net/validation/run`, body);
  }

  /// Fetch the entity's full audit timeline — no 500-row cap that
  /// the generic /api/net/audit list applies.
  getEntityTimeline(
    organizationId: string,
    entityType: string,
    entityId: string,
    limit?: number,
  ): Observable<AuditRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (limit !== undefined) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<AuditRow[]>(
      `${this.base}/api/net/audit/entity/${encodeURIComponent(entityType)}/${entityId}`,
      { params },
    );
  }
}
