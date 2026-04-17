import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Add bearer token + tenant ID to API requests
  if (auth.isAuthenticated && (req.url.includes('/api/') || req.url.includes('/health'))) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${auth.token}`,
        'X-Tenant-ID': auth.tenantId,
      }
    });
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && auth.isAuthenticated && !req.url.includes('/auth/')) {
        // Token expired — try refresh
        return auth.refresh().pipe(
          switchMap(() => {
            // Retry with new token
            const retryReq = req.clone({
              setHeaders: {
                Authorization: `Bearer ${auth.token}`,
                'X-Tenant-ID': auth.tenantId,
              }
            });
            return next(retryReq);
          }),
          catchError(() => {
            // Refresh failed — redirect to login
            auth.logout();
            router.navigate(['/login']);
            return throwError(() => error);
          })
        );
      }
      return throwError(() => error);
    })
  );
};
