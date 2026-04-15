import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface SdRequest {
  id: number;
  display_id: string;
  subject: string;
  description: string;
  status: string;
  priority: string;
  requester_name: string;
  technician_name: string;
  group_name: string;
  category: string;
  created_at: string;
  resolved_at: string | null;
}

@Injectable({ providedIn: 'root' })
export class ServiceDeskService {
  constructor(private http: HttpClient, private auth: AuthService) {}

  private get headers(): HttpHeaders {
    return new HttpHeaders({
      Authorization: `Bearer ${this.auth.token}`,
      'X-Tenant-ID': this.auth.tenantId
    });
  }

  getRequests(): Observable<SdRequest[]> {
    return this.http.get<SdRequest[]>(`${environment.centralApiUrl}/api/servicedesk/requests`, { headers: this.headers });
  }

  updateStatus(id: number, status: string): Observable<any> {
    return this.http.put(`${environment.centralApiUrl}/api/servicedesk/requests/${id}/status`,
      { status }, { headers: this.headers });
  }

  assignTechnician(id: number, techId: number): Observable<any> {
    return this.http.put(`${environment.centralApiUrl}/api/servicedesk/requests/${id}/assign`,
      { technician_id: techId }, { headers: this.headers });
  }
}
