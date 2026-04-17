import { Directive, Input, OnDestroy, OnInit, TemplateRef, ViewContainerRef } from '@angular/core';
import { Subscription } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PermissionCode, PermissionService } from '../services/permission.service';

/**
 * Structural directive that renders content only if the current user has
 * the given permission. Re-checks reactively when the user changes.
 *
 * Usage:
 *   <button *appHasPerm="'admin:users'">Add User</button>
 *   <a *appHasPerm="['admin:keys', 'admin:jobs']">Settings</a>
 *
 * String form requires exactly one permission. Array form requires *any*
 * of the listed permissions (use [appHasPerm]="['admin:keys']" syntactically).
 *
 * Pairs with ModuleRegistryService — module gating is checked at the route
 * + sidebar level, permission gating is checked per-control inside a module.
 */
@Directive({
  selector: '[appHasPerm]',
  standalone: true,
})
export class HasPermDirective implements OnInit, OnDestroy {
  private codes: PermissionCode[] = [];
  private hasView = false;
  private sub?: Subscription;

  @Input() set appHasPerm(value: PermissionCode | PermissionCode[]) {
    this.codes = Array.isArray(value) ? value : [value];
    this.update();
  }

  constructor(
    private tpl: TemplateRef<unknown>,
    private vcr: ViewContainerRef,
    private perms: PermissionService,
    private auth: AuthService,
  ) {}

  ngOnInit(): void {
    // Re-evaluate when the user changes (login/logout/refresh).
    this.sub = this.auth.user$.subscribe(() => this.update());
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private update(): void {
    const allowed = this.codes.length === 0 || this.perms.hasAny(...this.codes);
    if (allowed && !this.hasView) {
      this.vcr.createEmbeddedView(this.tpl);
      this.hasView = true;
    } else if (!allowed && this.hasView) {
      this.vcr.clear();
      this.hasView = false;
    }
  }
}
