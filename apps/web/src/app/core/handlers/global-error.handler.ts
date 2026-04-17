import { ErrorHandler, Injectable, Injector } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ErrorLoggerService } from '../services/error-logger.service';

/**
 * Catches every uncaught error in the Angular zone and forwards it to
 * Central's app_log via ErrorLoggerService.
 *
 * We use Injector (not constructor injection of ErrorLoggerService) because
 * GlobalErrorHandler is itself a provider that DI may instantiate before the
 * full provider tree is ready — late-resolving the logger avoids a cycle.
 *
 * After logging we also delegate to the default ErrorHandler so the error
 * still surfaces in the dev console — we want centralized capture, not
 * silent swallowing.
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly fallback = new ErrorHandler();

  constructor(private injector: Injector) {}

  handleError(error: any): void {
    try {
      const logger = this.injector.get(ErrorLoggerService);

      if (error instanceof HttpErrorResponse) {
        // HTTP errors are also caught by the interceptor — but if one slips
        // through (e.g. an uncaught throwError from a component) we still
        // want it. The interceptor stamps "ng-http"; we use "ng-runtime-http"
        // here so we can tell them apart in the log.
        logger.error(
          'ng-runtime-http',
          `${error.status} ${error.statusText} ${error.url ?? ''}`.trim(),
          this.formatHttpDetail(error)
        );
      } else {
        const err = this.unwrap(error);
        logger.error(
          'ng-runtime',
          err.message || String(error),
          err.stack || undefined
        );
      }
    } catch {
      // Logger itself blew up — never throw from an error handler.
    }

    // Still let Angular log the error to the console (dev visibility).
    this.fallback.handleError(error);
  }

  private unwrap(error: any): { message: string; stack?: string } {
    if (!error) return { message: 'unknown error' };
    if (error instanceof Error) return { message: error.message, stack: error.stack };
    // Angular sometimes wraps the original error in `.rejection` (promise) or `.originalError`.
    if (error.rejection)     return this.unwrap(error.rejection);
    if (error.originalError) return this.unwrap(error.originalError);
    if (typeof error === 'object') {
      try { return { message: JSON.stringify(error) }; }
      catch { return { message: String(error) }; }
    }
    return { message: String(error) };
  }

  private formatHttpDetail(error: HttpErrorResponse): string {
    const lines = [
      `Method+URL: ${error.url ?? '(unknown)'}`,
      `Status:     ${error.status} ${error.statusText}`,
      `Message:    ${error.message}`,
    ];
    if (error.error) {
      try {
        lines.push('Body:       ' + (typeof error.error === 'string' ? error.error : JSON.stringify(error.error)));
      } catch { /* ignore */ }
    }
    return lines.join('\n');
  }
}
