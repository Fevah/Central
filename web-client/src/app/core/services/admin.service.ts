import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

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

export interface TenantInfo {
  id: string;
  name: string;
  slug: string;
  status: string;
  tier: string;
  created_at: string;
  user_count: number;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  constructor(private http: HttpClient) {}

  getDashboard(): Observable<PlatformDashboard> {
    return this.http.get<PlatformDashboard>(`${environment.gatewayUrl}/admin/dashboard`);
  }

  getServices(): Observable<any[]> {
    return this.http.get<any[]>(`${environment.gatewayUrl}/admin/services`);
  }

  getTenants(): Observable<TenantInfo[]> {
    return this.http.get<TenantInfo[]>(`${environment.authServiceUrl}/api/v1/admin/tenants`);
  }
}
