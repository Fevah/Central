import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AuthUser {
  id: string;
  email: string;
  display_name: string;
  first_name?: string;
  last_name?: string;
  roles: string[];
  permissions: string[];
  mfa_enabled: boolean;
}

export interface LoginResponse {
  access_token: string;
  refresh_token: string;
  session_id: string;
  expires_in: number;
  token_type: string;
  mfa_required: boolean;
  mfa_methods: string[];
  user: AuthUser;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private currentUser$ = new BehaviorSubject<AuthUser | null>(null);
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private refreshTimer: any;

  user$ = this.currentUser$.asObservable();

  constructor(private http: HttpClient) {
    this.loadFromStorage();
  }

  get isAuthenticated(): boolean {
    return !!this.accessToken;
  }

  get token(): string | null {
    return this.accessToken;
  }

  get tenantId(): string {
    return environment.defaultTenantId;
  }

  get currentUser(): AuthUser | null {
    return this.currentUser$.value;
  }

  login(email: string, password: string): Observable<LoginResponse> {
    const headers = new HttpHeaders({ 'X-Tenant-ID': this.tenantId });
    return this.http.post<LoginResponse>(
      `${environment.authServiceUrl}/api/v1/auth/login`,
      { email, password },
      { headers }
    ).pipe(
      tap(resp => {
        if (!resp.mfa_required) {
          this.setSession(resp);
        }
      })
    );
  }

  verifyMfa(sessionId: string, code: string, method = 'totp'): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(
      `${environment.authServiceUrl}/api/v1/auth/mfa/verify`,
      { session_id: sessionId, code, method }
    ).pipe(tap(resp => this.setSession(resp)));
  }

  refresh(): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(
      `${environment.authServiceUrl}/api/v1/auth/refresh`,
      { refresh_token: this.refreshToken }
    ).pipe(tap(resp => this.setSession(resp)));
  }

  logout(): void {
    if (this.accessToken) {
      this.http.post(`${environment.authServiceUrl}/api/v1/auth/logout`, null, {
        headers: new HttpHeaders({ Authorization: `Bearer ${this.accessToken}` })
      }).subscribe();
    }
    this.clearSession();
  }

  hasRole(role: string): boolean {
    return this.currentUser?.roles?.includes(role) ?? false;
  }

  hasPermission(perm: string): boolean {
    return this.currentUser?.permissions?.includes(perm) ?? false;
  }

  private setSession(resp: LoginResponse): void {
    this.accessToken = resp.access_token;
    this.refreshToken = resp.refresh_token;
    this.currentUser$.next(resp.user);
    localStorage.setItem('central_access_token', resp.access_token);
    localStorage.setItem('central_refresh_token', resp.refresh_token);
    localStorage.setItem('central_user', JSON.stringify(resp.user));
    this.scheduleRefresh(resp.expires_in);
  }

  private clearSession(): void {
    this.accessToken = null;
    this.refreshToken = null;
    this.currentUser$.next(null);
    localStorage.removeItem('central_access_token');
    localStorage.removeItem('central_refresh_token');
    localStorage.removeItem('central_user');
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
  }

  private loadFromStorage(): void {
    const token = localStorage.getItem('central_access_token');
    const user = localStorage.getItem('central_user');
    if (token && user) {
      this.accessToken = token;
      this.refreshToken = localStorage.getItem('central_refresh_token');
      this.currentUser$.next(JSON.parse(user));
      this.scheduleRefresh(600); // refresh in 10 min
    }
  }

  private scheduleRefresh(expiresIn: number): void {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    // Refresh 60 seconds before expiry
    const delay = Math.max((expiresIn - 60) * 1000, 30000);
    this.refreshTimer = setTimeout(() => {
      this.refresh().subscribe({ error: () => this.clearSession() });
    }, delay);
  }
}
