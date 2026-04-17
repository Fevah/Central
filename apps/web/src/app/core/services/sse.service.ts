import { Injectable, NgZone, OnDestroy } from '@angular/core';
import { Subject, Observable, BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface SseEvent {
  event_type: string;
  tenant_id:  string;
  task_id?:   number;
  project_id?: number;
  payload:    any;
}

export type SseConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

const TASK_EVENT_TYPES = ['task_created', 'task_updated', 'task_deleted', 'sprint_changed'] as const;

/**
 * SSE consumer for the Rust task-service `/api/v1/tasks/stream` endpoint.
 *
 * Events are routed into a single `events$` stream — components subscribe
 * and filter by `event_type` themselves (matches the SignalR pattern).
 *
 * Reconnect strategy: exponential backoff capped at 30s. Each new
 * EventSource gets fresh listeners so we don't double-fire after a reconnect.
 */
@Injectable({ providedIn: 'root' })
export class SseService implements OnDestroy {
  private eventSource: EventSource | null = null;
  private reconnectTimer: any = null;
  private reconnectAttempt = 0;
  private explicitlyDisconnected = false;

  private readonly events$$ = new Subject<SseEvent>();
  readonly events$ = this.events$$.asObservable();

  private readonly state$$ = new BehaviorSubject<SseConnectionState>('disconnected');
  readonly state$ = this.state$$.asObservable();

  constructor(private auth: AuthService, private zone: NgZone) {}

  ngOnDestroy(): void { this.disconnect(); }

  /** Open the SSE connection. Idempotent — second call is a no-op. */
  connect(): void {
    if (this.eventSource) return;
    if (!this.auth.isAuthenticated) return;

    this.explicitlyDisconnected = false;
    this.openConnection();
  }

  /** Stop reconnect loop and close the connection. */
  disconnect(): void {
    this.explicitlyDisconnected = true;
    this.closeConnection();
    this.state$$.next('disconnected');
  }

  /** Filtered stream — only events of the requested type. */
  on(eventType: string): Observable<SseEvent> {
    return new Observable(sub => {
      const inner = this.events$$.subscribe(e => {
        if (e.event_type === eventType) sub.next(e);
      });
      return () => inner.unsubscribe();
    });
  }

  // ── internals ─────────────────────────────────────────────────────────

  private openConnection(): void {
    this.state$$.next(this.reconnectAttempt === 0 ? 'connecting' : 'reconnecting');

    // Note: EventSource can't carry custom headers, so the task-service
    // route must accept tenant from the URL or be configured to allow
    // anonymous reads with tenant-scoping done inside the stream.
    const url = `${environment.taskServiceUrl}/api/v1/tasks/stream`;
    const es = new EventSource(url);
    this.eventSource = es;

    es.onopen = () => {
      this.reconnectAttempt = 0;
      this.state$$.next('connected');
    };

    // Listen only for the typed events we care about — generic onmessage
    // would fire for keep-alive pings too.
    for (const eventType of TASK_EVENT_TYPES) {
      es.addEventListener(eventType, this.handleEvent);
    }

    es.onerror = () => this.scheduleReconnect();
  }

  /** Bound so addEventListener/removeEventListener match. */
  private handleEvent = (e: MessageEvent): void => {
    this.zone.run(() => {
      try {
        const data = JSON.parse(e.data) as SseEvent;
        this.events$$.next(data);
      } catch {
        // Ignore unparseable events — server should never emit non-JSON.
      }
    });
  };

  private scheduleReconnect(): void {
    this.closeConnection();
    if (this.explicitlyDisconnected) return;

    this.state$$.next('reconnecting');
    this.reconnectAttempt += 1;
    // Same schedule shape as SignalR: 0/2/5/10/30 then 30 forever.
    const schedule = [0, 2_000, 5_000, 10_000, 30_000];
    const delay = schedule[Math.min(this.reconnectAttempt - 1, schedule.length - 1)];

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.openConnection();
    }, delay);
  }

  private closeConnection(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.eventSource) {
      // Remove typed listeners so the EventSource can be garbage-collected.
      for (const eventType of TASK_EVENT_TYPES) {
        this.eventSource.removeEventListener(eventType, this.handleEvent);
      }
      this.eventSource.close();
      this.eventSource = null;
    }
  }
}
