import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorLoggingInterceptor } from './core/interceptors/error-logging.interceptor';
import { retryInterceptor } from './core/interceptors/retry.interceptor';
import { GlobalErrorHandler } from './core/handlers/global-error.handler';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Order matters:
    //   1. authInterceptor      — adds bearer + tenant header, refreshes token on 401
    //   2. retryInterceptor     — backs off on 503/504/429/network, only for safe methods
    //   3. errorLoggingInterceptor — records the FINAL outcome to app_log (so a successful
    //                                retry doesn't get logged as an error)
    provideHttpClient(withInterceptors([authInterceptor, retryInterceptor, errorLoggingInterceptor])),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
  ]
};
