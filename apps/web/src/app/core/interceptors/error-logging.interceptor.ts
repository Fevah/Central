import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { ErrorLoggerService } from '../services/error-logger.service';

/**
 * Logs every failed HTTP response to Central's app_log, then re-emits the
 * error so other interceptors / subscribers see it as normal.
 *
 * Skipped:
 *   - the log endpoint itself (no recursion if it 500s — the catch in
 *     ErrorLoggerService.flush handles that)
 *   - 401s on the auth refresh path (handled by auth.interceptor)
 *
 * Tag: "ng-http". GlobalErrorHandler uses "ng-runtime-http" for HTTP
 * errors that escape rxjs into the zone, so we can distinguish.
 */
export const errorLoggingInterceptor: HttpInterceptorFn = (req, next) => {
  const logger = inject(ErrorLoggerService);

  return next(req).pipe(
    tap({
      error: (err) => {
        if (!(err instanceof HttpErrorResponse)) return;
        if (req.url.includes('/api/log/client')) return;       // avoid recursion
        if (err.status === 401 && req.url.includes('/auth/')) return; // expected during refresh

        const detailLines = [
          `Method:  ${req.method}`,
          `URL:     ${req.urlWithParams}`,
          `Status:  ${err.status} ${err.statusText}`,
        ];
        if (err.error) {
          try {
            detailLines.push('Body:    ' + (typeof err.error === 'string' ? err.error : JSON.stringify(err.error)));
          } catch { /* ignore */ }
        }

        // 0 = network error / CORS / server unreachable
        // 4xx = client problem (often expected — log as warning)
        // 5xx = server problem (always error)
        const level = err.status === 0 || err.status >= 500 ? 'error' : 'warning';
        const msg = `${err.status || 'NETWORK'} ${req.method} ${stripQuery(req.url)}`;

        if (level === 'error') logger.error('ng-http', msg, detailLines.join('\n'));
        else                   logger.warning('ng-http', msg, detailLines.join('\n'));
      }
    })
  );
};

function stripQuery(url: string): string {
  const i = url.indexOf('?');
  return i < 0 ? url : url.substring(0, i);
}
