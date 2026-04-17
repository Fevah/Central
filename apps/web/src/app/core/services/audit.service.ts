import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface Investigation {
  id: string;
  title: string;
  description: string;
  target_user: string;
  status: string;
  created_by: string;
  created_at: string;
}

export interface Finding {
  id: string;
  finding_type: string;
  severity: string;
  title: string;
  detail: string;
  evidence_json: any;
}

export interface GdprScore {
  overall_score: number;
  overall_grade: string;
  compliant_count: number;
  partial_count: number;
  non_compliant_count: number;
}

export interface GdprArticle {
  article: string;
  title: string;
  score: number;
  status: string;
  findings: string[];
}

@Injectable({ providedIn: 'root' })
export class AuditService {
  constructor(private http: HttpClient, private auth: AuthService) {}

  private get headers(): HttpHeaders {
    return new HttpHeaders({
      Authorization: `Bearer ${this.auth.token}`,
      'X-Tenant-ID': this.auth.tenantId
    });
  }

  getInvestigations(): Observable<Investigation[]> {
    return this.http.get<Investigation[]>(`${environment.gatewayUrl}/api/v1/audit/investigations`, { headers: this.headers });
  }

  getInvestigation(id: string): Observable<{ investigation: Investigation; findings: Finding[] }> {
    return this.http.get<any>(`${environment.gatewayUrl}/api/v1/audit/investigations/${id}`, { headers: this.headers });
  }

  createInvestigation(data: { title: string; description?: string; target_user?: string }): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/audit/investigations`, data, { headers: this.headers });
  }

  exportEvidence(id: string): Observable<any> {
    return this.http.post(`${environment.gatewayUrl}/api/v1/audit/investigations/${id}/export`, null, { headers: this.headers });
  }

  getGdprScore(): Observable<GdprScore> {
    return this.http.get<GdprScore>(`${environment.gatewayUrl}/api/v1/audit/gdpr/score`, { headers: this.headers });
  }

  getGdprArticles(): Observable<GdprArticle[]> {
    return this.http.get<GdprArticle[]>(`${environment.gatewayUrl}/api/v1/audit/gdpr/articles`, { headers: this.headers });
  }

  searchM365Logs(params: { user?: string; operation?: string }): Observable<any[]> {
    const qs = new URLSearchParams(params as any).toString();
    return this.http.get<any[]>(`${environment.gatewayUrl}/api/v1/audit/m365/logs?${qs}`, { headers: this.headers });
  }
}
