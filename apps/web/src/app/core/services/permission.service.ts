import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Canonical permission codes — kept in sync with `permissions` table seeded
 * by 024_permissions_v2.sql. Format is `module:action`.
 *
 * If you add a row to the permissions table, add a constant here so the
 * compiler can catch typos in *hasPerm calls.
 */
export type PermissionCode =
  | 'devices:read'    | 'devices:write'    | 'devices:delete'    | 'devices:export'   | 'devices:reserved'
  | 'switches:read'   | 'switches:write'   | 'switches:delete'
  | 'switches:ping'   | 'switches:ssh'     | 'switches:sync'     | 'switches:deploy'
  | 'links:read'      | 'links:write'      | 'links:delete'
  | 'bgp:read'        | 'bgp:write'        | 'bgp:sync'
  | 'vlans:read'      | 'vlans:write'
  | 'admin:users'     | 'admin:roles'      | 'admin:lookups'     | 'admin:settings'   | 'admin:audit'
  | 'admin:keys'      | 'admin:jobs'       | 'admin:backups'
  | 'globaladmin:tenants' | 'globaladmin:users' | 'globaladmin:licenses';

/**
 * Single source of truth for "can the current user do X?".
 *
 * Backed by `auth.currentUser.permissions: string[]`. Two layers of gating:
 *  1. Module licensing (ModuleRegistryService) — what's available to the tenant
 *  2. Permission grants (this service)         — what's available to the user within those modules
 *
 * Both must allow before a UI element shows. The server enforces both
 * independently (ModuleLicenseMiddleware + endpoint authz), but the UI
 * mirrors them so users don't see dead-end nav entries.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  constructor(private auth: AuthService) {}

  /** Sync check — `true` if the current user has the given permission. */
  has(code: PermissionCode): boolean {
    const perms = this.auth.currentUser?.permissions ?? [];
    return perms.includes(code);
  }

  /** Sync check — true if the user has *any* of the given permissions. */
  hasAny(...codes: PermissionCode[]): boolean {
    const perms = this.auth.currentUser?.permissions ?? [];
    return codes.some(c => perms.includes(c));
  }

  /** Sync check — true if the user has *all* given permissions. */
  hasAll(...codes: PermissionCode[]): boolean {
    const perms = this.auth.currentUser?.permissions ?? [];
    return codes.every(c => perms.includes(c));
  }

  /** True if the current user is in the given role (case-insensitive). */
  hasRole(role: string): boolean {
    return this.auth.hasRole(role);
  }

  /** Reactive — re-emits whenever the user changes (login/logout/refresh). */
  has$(code: PermissionCode): Observable<boolean> {
    return this.auth.user$.pipe(
      map(u => (u?.permissions ?? []).includes(code))
    );
  }
}
