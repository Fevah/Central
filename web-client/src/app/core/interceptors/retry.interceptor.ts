import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { Observable, throwError, timer } from 'rxjs';
import { mergeMap, retryWhen, take, scan } from 'rxjs/operators';

/**
 * Retry transient failures with exponential backoff.
 *
 * What we retry:
 *   - 503 Service Unavailable (server/upstream is overloaded)
 *   - 504 Gateway Timeout
 *   - 429 Too Many Requests (honors `Retry-After` header if present)
 *   - status 0 (network error, CORS, server unreachable)
 *
 * What we DON'T retry:
 *   - 4xx other than 429 (the client is wrong; retrying won't help)
 *   - Non-idempotent methods (POST/PATCH) on 5xx — could double-execute
 *   - The /api/log/client endpoint (avoid recursion if logging itself fails)
 *
 * Schedule: 0.5s, 1s, 2s — three attempts max. Anything that takes longer
 * than ~3s of retries is unlikely to recover quickly; let the user retry.
 */
export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  if (!shouldRetry(req)) return next(req);

  const maxRetries = 3;

  return next(req).pipe(
    retryWhen(errors =>
      errors.pipe(
        scan((acc: { count: number; err: any }, err: any) => ({ count: acc.count + 1, err }),
             { count: 0, err: null }),
        mergeMap(({ count, err }) => {
          if (count > maxRetries)                 return throwError(() => err);
          if (!isRetriable(err))                  return throwError(() => err);

          const delayMs = computeDelay(err, count);
          return timer(delayMs);
        }),
        take(maxRetries + 1),
      )
    )
  );
};

function shouldRetry(req: HttpRequest<unknown>): boolean {
  if (req.url.includes('/api/log/client')) return false;
  // Only safely-idempotent methods. A POST that creates a row and 503s
  // mid-write may have actually succeeded — replaying could double-create.
  return req.method === 'GET' || req.method === 'HEAD' || req.method === 'OPTIONS';
}

function isRetriable(err: unknown): boolean {
  if (!(err instanceof HttpErrorResponse)) return false;
  return err.status === 0 || err.status === 503 || err.status === 504 || err.status === 429;
}

/**
 * Backoff schedule. For 429, prefer the server's Retry-After header
 * (seconds). Otherwise exponential 500/1000/2000 ms.
 */
function computeDelay(err: unknown, attempt: number): number {
  if (err instanceof HttpErrorResponse && err.status === 429) {
    const ra = err.headers?.get('Retry-After');
    if (ra) {
      const secs = Number(ra);
      if (Number.isFinite(secs) && secs > 0) return Math.min(secs * 1000, 10_000);
    }
  }
  // attempt is 1-based after first retry
  const schedule = [500, 1000, 2000];
  return schedule[Math.min(attempt - 1, schedule.length - 1)];
}
