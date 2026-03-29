import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // Add bearer token + tenant ID to API requests
  if (auth.isAuthenticated && (req.url.includes('/api/') || req.url.includes('/health'))) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${auth.token}`,
        'X-Tenant-ID': auth.tenantId,
      }
    });
  }

  return next(req);
};
