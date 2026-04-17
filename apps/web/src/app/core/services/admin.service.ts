import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface ServiceStatus {
  name: string;
  url: string;
  healthy: boolean;
  version: string | null;
  latency_ms: number;
}

export interface PlatformDashboard {
  services: ServiceStatus[];
  system: {
    gateway_version: string;
    uptime_seconds: number;
    total_routes: number;
    platform: string;
  };
}

export interface Tenant {
  id: string;
  name: string;
  slug: string;
  status: string;
  plan: string;
  user_count: number;
  created_at: string;
}

export interface GlobalUser {
  id: string;
  email: string;
  display_name: string;
  tenant_name: string;
  roles: string;
  status: string;
  mfa_enabled: boolean;
  last_login: string | null;
}

export interface ModuleLicense {
  id: number;
  tenant_name: string;
  module_name: string;
  enabled: boolean;
  max_users: number;
  expires_at: string | null;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  constructor(private http: HttpClient, private auth: AuthService) {}

  private get headers(): HttpHeaders {
    return new HttpHeaders({
      Authorization: `Bearer ${this.auth.token}`,
      'X-Tenant-ID': this.auth.tenantId
    });
  }

  getDashboard(): Observable<PlatformDashboard> {
    return this.http.get<PlatformDashboard>(`${environment.gatewayUrl}/health`, { headers: this.headers });
  }

  getServices(): Observable<any[]> {
    return this.http.get<any[]>(`${environment.gatewayUrl}/health`, { headers: this.headers });
  }

  // ── Tenants ──

  getTenants(): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(`${environment.gatewayUrl}/api/v1/admin/tenants`, { headers: this.headers });
  }

  createTenant(data: { name: string; slug?: string; plan?: string; admin_email: string }): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/admin/tenants`, data, { headers: this.headers });
  }

  updateTenantStatus(id: string, status: string): Observable<any> {
    return this.http.put(`${environment.gatewayUrl}/api/v1/admin/tenants/${id}/status`, { status }, { headers: this.headers });
  }

  // ── Global Users ──

  getGlobalUsers(): Observable<GlobalUser[]> {
    return this.http.get<GlobalUser[]>(`${environment.gatewayUrl}/api/v1/admin/users`, { headers: this.headers });
  }

  resetUserPassword(userId: string): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/admin/users/${userId}/reset-password`, null, { headers: this.headers });
  }

  toggleUserAdmin(userId: string): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/admin/users/${userId}/toggle-admin`, null, { headers: this.headers });
  }

  // ── Licenses ──

  getLicenses(): Observable<ModuleLicense[]> {
    return this.http.get<ModuleLicense[]>(`${environment.gatewayUrl}/api/v1/admin/licenses`, { headers: this.headers });
  }

  updateLicense(id: number, data: Partial<ModuleLicense>): Observable<any> {
    return this.http.put(`${environment.gatewayUrl}/api/v1/admin/licenses/${id}`, data, { headers: this.headers });
  }

  grantModule(tenantId: string, moduleId: string): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/admin/licenses/grant`,
      { tenant_id: tenantId, module_id: moduleId }, { headers: this.headers });
  }

  revokeModule(tenantId: string, moduleId: string): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/admin/licenses/revoke`,
      { tenant_id: tenantId, module_id: moduleId }, { headers: this.headers });
  }

  // ──────────────────────────────────────────────────────────────────────
  // Tenant-scoped admin endpoints — these hit Central.Api directly, not
  // the Rust gateway. The interceptor adds Authorization + X-Tenant-ID.
  // ──────────────────────────────────────────────────────────────────────

  // ── Tenant Users (Central.Api) ──

  getTenantUsers(): Observable<TenantUser[]> {
    return this.http.get<TenantUser[]>(`${environment.centralApiUrl}/api/admin/users`);
  }

  saveTenantUser(user: Partial<TenantUser>): Observable<{ id: number }> {
    return this.http.put<{ id: number }>(`${environment.centralApiUrl}/api/admin/users`, user);
  }

  deleteTenantUser(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.centralApiUrl}/api/admin/users/${id}`);
  }

  resetTenantUserPassword(id: number, password: string): Observable<{ id: number; password_reset: boolean }> {
    return this.http.post<{ id: number; password_reset: boolean }>(
      `${environment.centralApiUrl}/api/admin/users/${id}/reset-password`,
      { password }
    );
  }

  // ── API Keys ──

  getApiKeys(): Observable<ApiKey[]> {
    return this.http.get<ApiKey[]>(`${environment.centralApiUrl}/api/keys/`);
  }

  generateApiKey(name: string, role?: string): Observable<{ key: string; name: string; role: string }> {
    return this.http.post<{ key: string; name: string; role: string }>(
      `${environment.centralApiUrl}/api/keys/generate`, { name, role });
  }

  revokeApiKey(id: number): Observable<void> {
    return this.http.post<void>(`${environment.centralApiUrl}/api/keys/${id}/revoke`, null);
  }

  deleteApiKey(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.centralApiUrl}/api/keys/${id}`);
  }

  // ── Jobs / Scheduler ──

  getJobs(): Observable<JobSchedule[]> {
    return this.http.get<JobSchedule[]>(`${environment.centralApiUrl}/api/jobs/`);
  }

  enableJob(id: number): Observable<{ enabled: boolean }> {
    return this.http.put<{ enabled: boolean }>(`${environment.centralApiUrl}/api/jobs/${id}/enable`, null);
  }

  disableJob(id: number): Observable<{ enabled: boolean }> {
    return this.http.put<{ enabled: boolean }>(`${environment.centralApiUrl}/api/jobs/${id}/disable`, null);
  }

  setJobInterval(id: number, minutes: number): Observable<{ interval_minutes: number }> {
    return this.http.put<{ interval_minutes: number }>(
      `${environment.centralApiUrl}/api/jobs/${id}/interval`, { minutes });
  }

  runJob(id: number): Observable<{ history_id: number; status: string; summary: string }> {
    return this.http.post<{ history_id: number; status: string; summary: string }>(
      `${environment.centralApiUrl}/api/jobs/${id}/run`, null);
  }

  getJobHistory(): Observable<JobHistoryEntry[]> {
    return this.http.get<JobHistoryEntry[]>(`${environment.centralApiUrl}/api/jobs/history`);
  }

  // ── Backups ──

  getBackupHistory(): Observable<BackupHistoryEntry[]> {
    return this.http.get<BackupHistoryEntry[]>(`${environment.centralApiUrl}/api/backup/history`);
  }

  runBackup(): Observable<BackupHistoryEntry> {
    return this.http.post<BackupHistoryEntry>(`${environment.centralApiUrl}/api/backup/run`, null);
  }

  getPurgeCounts(): Observable<Record<string, number>> {
    return this.http.get<Record<string, number>>(`${environment.centralApiUrl}/api/backup/purge-counts`);
  }

  purgeTable(table: string): Observable<{ table: string; purged: number }> {
    return this.http.post<{ table: string; purged: number }>(
      `${environment.centralApiUrl}/api/backup/purge/${table}`, null);
  }
}

// ── Tenant-scoped admin DTOs (mirror the Central.Api endpoint shapes) ──

export interface TenantUser {
  id?:           number;
  username:      string;
  display_name?: string;
  role:          string;
  email?:        string;
  is_active?:    boolean;
  last_login?:   string | null;
}

export interface ApiKey {
  id:         number;
  name:       string;
  role:       string;
  created_at: string;
  last_used?: string | null;
  is_revoked?: boolean;
}

export interface JobSchedule {
  id:               number;
  job_type:         string;
  name:             string;
  is_enabled:       boolean;
  interval_minutes: number;
  last_run_at:      string | null;
  next_run_at:      string | null;
  last_status?:     string | null;
}

export interface JobHistoryEntry {
  id:           number;
  job_id:       number;
  job_type:     string;
  status:       string;
  started_at:   string;
  duration_ms?: number;
  summary?:     string;
  error?:       string;
}

export interface BackupHistoryEntry {
  id:               number;
  file_path:        string;
  file_size_bytes:  number;
  status:           string;
  started_at:       string;
  duration_ms?:     number;
  error?:           string;
}
