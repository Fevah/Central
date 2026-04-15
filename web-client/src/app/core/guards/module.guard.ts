import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ModuleCode, ModuleRegistryService } from '../services/module-registry.service';

/**
 * Route guard that blocks navigation to disabled modules.
 *
 * Usage in routes:
 *   { path: 'tasks', canActivate: [moduleGuard('tasks')], loadChildren: ... }
 *
 * If the module list hasn't loaded yet (deep link on cold start), we wait
 * on `ensureLoaded()` before deciding. If the module is disabled we redirect
 * to /dashboard rather than showing a blank page — the sidebar won't show
 * the link anyway, so this only fires when someone types the URL directly.
 */
export function moduleGuard(code: ModuleCode): CanActivateFn {
  return async () => {
    const registry = inject(ModuleRegistryService);
    const router = inject(Router);

    await registry.ensureLoaded();

    if (registry.isEnabled(code)) return true;

    // Not licensed — send them somewhere safe.
    return router.createUrlTree(['/dashboard'], {
      queryParams: { disabled_module: code }
    });
  };
}
