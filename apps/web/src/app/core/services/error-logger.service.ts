import { Injectable, NgZone } from '@angular/core';
import { environment } from '../../../environments/environment';

/**
 * Pushes browser errors into Central's app_log table via /api/log/client.
 *
 * Uses a small in-memory queue with a debounced flush so a burst of errors
 * (e.g. one bad component re-rendering) becomes one HTTP call rather than
 * dozens. Falls back to navigator.sendBeacon on page unload so we don't
 * lose the last error when the user closes the tab.
 *
 * Bypasses Angular's HttpClient on purpose:
 *   - avoids re-triggering the auth interceptor (which logs to us)
 *   - avoids creating a recursive error loop if HttpClient itself is broken
 */
@Injectable({ providedIn: 'root' })
export class ErrorLoggerService {
  private readonly endpoint = `${environment.centralApiUrl}/api/log/client`;
  private readonly queue: ClientLogEntry[] = [];
  private readonly maxQueue = 50;
  private readonly flushDelayMs = 1000;
  private flushTimer: any = null;
  private currentUsername: string | null = null;

  constructor(private zone: NgZone) {
    // Best-effort flush when the page is being closed.
    if (typeof window !== 'undefined') {
      window.addEventListener('beforeunload', () => this.flushBeacon());
      window.addEventListener('pagehide',     () => this.flushBeacon());
    }
  }

  /** Called by AuthService when login state changes — stamps logs with the user. */
  setCurrentUser(username: string | null): void {
    this.currentUsername = username;
  }

  log(entry: ClientLogEntry): void {
    // Drop if queue is full — we'd rather lose a few errors than runaway memory.
    if (this.queue.length >= this.maxQueue) return;

    this.queue.push({
      ...entry,
      url:       entry.url       ?? (typeof window !== 'undefined' ? window.location.href : ''),
      userAgent: entry.userAgent ?? (typeof navigator !== 'undefined' ? navigator.userAgent : ''),
      username:  entry.username  ?? this.currentUsername ?? 'anonymous',
    });

    this.scheduleFlush();
  }

  /** Convenience helpers mirroring AppLogger on the .NET side. */
  error(tag: string, message: string, detail?: string)   { this.log({ level: 'Error',   tag, message, detail }); }
  warning(tag: string, message: string, detail?: string) { this.log({ level: 'Warning', tag, message, detail }); }
  info(tag: string, message: string, detail?: string)    { this.log({ level: 'Info',    tag, message, detail }); }

  private scheduleFlush(): void {
    if (this.flushTimer) return;
    // Run outside Angular zone so we don't keep change detection alive on a timer.
    this.zone.runOutsideAngular(() => {
      this.flushTimer = setTimeout(() => this.flush(), this.flushDelayMs);
    });
  }

  private async flush(): Promise<void> {
    this.flushTimer = null;
    if (this.queue.length === 0) return;

    const batch = { entries: this.queue.splice(0, this.queue.length) };

    try {
      // Plain fetch — bypasses HttpClient/interceptors.
      await fetch(this.endpoint, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(batch),
        // Don't send auth cookies — endpoint is anonymous.
        credentials: 'omit',
        keepalive:   true,
      });
    } catch {
      // Swallow — logging must never throw.
    }
  }

  /** Synchronous best-effort flush used during page-unload. */
  private flushBeacon(): void {
    if (this.queue.length === 0) return;
    const batch = { entries: this.queue.splice(0, this.queue.length) };
    try {
      const blob = new Blob([JSON.stringify(batch)], { type: 'application/json' });
      navigator.sendBeacon?.(this.endpoint, blob);
    } catch {
      // ignore
    }
  }
}

export interface ClientLogEntry {
  level:     'Error' | 'Warning' | 'Info';
  tag:       string;
  message:   string;
  detail?:   string;
  source?:   string;
  url?:      string;
  userAgent?: string;
  username?: string;
}
