import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, firstValueFrom, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/**
 * Canonical list of modules shipped by the platform. Must match the
 * `module_catalog.code` column in central_platform.module_catalog.
 * If you add a new module to the DB seed, add it here too.
 */
export type ModuleCode =
  | 'devices'
  | 'switches'
  | 'links'
  | 'routing'
  | 'vlans'
  | 'admin'
  | 'tasks'
  | 'servicedesk'
  | 'audit'
  | 'globaladmin';

export interface ModuleLicense {
  moduleId:    number;
  code:        ModuleCode;
  displayName: string;
  isBase:      boolean;
  isLicensed:  boolean;
  expiresAt:   string | null;
}

/**
 * Loads and caches which modules are enabled for the current tenant.
 *
 * Source of truth is the server (`GET /api/register/modules`). The web UI
 * must never render, link to, or call an API for a disabled module — the
 * server enforces this with a 403 from ModuleLicenseMiddleware, but we
 * hide the UI too so users don't see dead-end nav entries.
 *
 * Flow:
 *  1. AuthService calls `reload()` after login succeeds.
 *  2. Layout subscribes to `modules$` and filters its nav by `isLicensed`.
 *  3. ModuleGuard calls `isEnabled(code)` before allowing navigation.
 *  4. On logout, `clear()` resets state.
 */
@Injectable({ providedIn: 'root' })
export class ModuleRegistryService {
  private readonly endpoint = `${environment.centralApiUrl}/api/register/modules`;
  private readonly modules$$ = new BehaviorSubject<ModuleLicense[]>([]);
  private loaded = false;

  /** Observable of the full module list (licensed + unlicensed). */
  readonly modules$ = this.modules$$.asObservable();

  constructor(private http: HttpClient) {}

  /** Sync snapshot — returns last cached list (may be empty pre-login). */
  get snapshot(): ModuleLicense[] {
    return this.modules$$.value;
  }

  /** True if the given module is present AND licensed for the current tenant. */
  isEnabled(code: ModuleCode): boolean {
    return this.modules$$.value.some(m => m.code === code && m.isLicensed);
  }

  /** Fetch fresh state from the server and broadcast. */
  reload(): Observable<ModuleLicense[]> {
    return this.http.get<ModuleLicense[]>(this.endpoint).pipe(
      tap(list => {
        this.modules$$.next(list);
        this.loaded = true;
      }),
      catchError(err => {
        // Don't crash the app — fall back to an empty list; guards will
        // block access. The HTTP error-logger already captures this.
        console.warn('[ModuleRegistry] failed to load modules', err);
        this.modules$$.next([]);
        this.loaded = true;
        return of([] as ModuleLicense[]);
      })
    );
  }

  /** Ensures modules are loaded before returning — used by bootstrap guards. */
  ensureLoaded(): Promise<ModuleLicense[]> {
    if (this.loaded) return Promise.resolve(this.modules$$.value);
    return firstValueFrom(this.reload());
  }

  /** Wipe cache — call on logout so next user sees their own modules. */
  clear(): void {
    this.modules$$.next([]);
    this.loaded = false;
  }
}
