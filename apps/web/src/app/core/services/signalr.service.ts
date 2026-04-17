import { Injectable, NgZone } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject, Observable, BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/**
 * Server-emitted events from Central.Api NotificationHub at /hubs/notify.
 * Names + arg arity must stay in sync with NotificationHub.cs and the
 * background services (ChangeNotifierService, SshOperationsService,
 * JobSchedulerService) that broadcast via the hub.
 */

/** Generic table change — fired by ChangeNotifier (pg_notify), SSH ops, jobs. */
export interface DataChangedEvent {
  table: string;
  op:    'INSERT' | 'UPDATE' | 'DELETE' | string;
  id:    string;
}

/** Per-switch ping result from SSH operations background work. */
export interface PingResultEvent {
  hostname:  string;
  success:   boolean;
  latencyMs: number | null;
}

/** Per-switch sync progress (config download, BGP sync, etc.). */
export interface SyncProgressEvent {
  hostname:   string;
  status:     string;       // e.g. "starting", "downloading", "complete", "failed"
  progressPct: number;
}

/** Detected drift between in-memory + on-switch config. */
export interface ConfigDriftEvent {
  hostname:         string;
  changedLineCount: number;
}

/** Free-form notification fan-out — used by audit, session, sync events. */
export interface NotificationEvent {
  eventType: string;
  title:     string;
  message:   string;
  severity:  'info' | 'success' | 'warning' | 'error' | string;
}

/** External webhook landed (sync engine inbound). */
export interface WebhookReceivedEvent {
  source:    string;
  webhookId: string;
}

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Wraps the @microsoft/signalr HubConnection in a per-event Subject so
 * components can `signalR.dataChanged$.subscribe(...)` without knowing
 * about the underlying transport.
 *
 * Auth: SignalR's WebSocket upgrade can't carry custom headers, so we
 * pass the bearer via the `accessTokenFactory` callback — Program.cs
 * extracts it from the `access_token` query string for /hubs/notify.
 *
 * Reconnect: SignalR has built-in reconnect; we pass an exponential
 * delay schedule capped at 30s so a long server outage doesn't flood.
 */
@Injectable({ providedIn: 'root' })
export class SignalRService {
  private connection: HubConnection | null = null;
  private readonly url = `${environment.centralApiUrl}/hubs/notify`;

  private readonly state$$ = new BehaviorSubject<ConnectionState>('disconnected');
  readonly state$ = this.state$$.asObservable();

  // Typed event streams. Components subscribe to the ones they care about.
  readonly dataChanged$      = new Subject<DataChangedEvent>();
  readonly pingResult$       = new Subject<PingResultEvent>();
  readonly syncProgress$     = new Subject<SyncProgressEvent>();
  readonly configDrift$      = new Subject<ConfigDriftEvent>();
  readonly notificationEvt$  = new Subject<NotificationEvent>();
  readonly webhookReceived$  = new Subject<WebhookReceivedEvent>();

  constructor(private auth: AuthService, private zone: NgZone) {}

  get isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  /**
   * Open the hub connection. Safe to call repeatedly — subsequent calls
   * with the same token are no-ops; if the token has changed (token
   * refresh) a new connection is opened.
   */
  async connect(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) return;
    if (!this.auth.token) return; // Don't bother if not logged in.

    this.state$$.next('connecting');

    this.connection = new HubConnectionBuilder()
      .withUrl(this.url, {
        // Server reads the token from query string for WebSocket upgrade.
        accessTokenFactory: () => this.auth.token ?? '',
      })
      // Exponential backoff capped at 30s; otherwise SignalR retries forever.
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: ctx => {
          // 0s, 2s, 5s, 10s, 30s, 30s, 30s …
          const schedule = [0, 2_000, 5_000, 10_000, 30_000];
          return schedule[Math.min(ctx.previousRetryCount, schedule.length - 1)];
        }
      })
      .configureLogging(LogLevel.Warning)
      .build();

    this.wireHandlers();

    this.connection.onreconnecting(() => this.state$$.next('reconnecting'));
    this.connection.onreconnected (() => this.state$$.next('connected'));
    this.connection.onclose       (() => this.state$$.next('disconnected'));

    try {
      await this.connection.start();
      this.state$$.next('connected');
    } catch (err) {
      // Don't throw — let the reconnect kick in. ErrorLogger captures it.
      console.warn('[SignalR] initial connect failed', err);
      this.state$$.next('disconnected');
    }
  }

  /** Stop and clean up. Call on logout. */
  async disconnect(): Promise<void> {
    if (!this.connection) return;
    try { await this.connection.stop(); } catch { /* ignore */ }
    this.connection = null;
    this.state$$.next('disconnected');
  }

  /** Type-safe `on()` used internally — runs handlers inside Angular zone. */
  private on<T>(event: string, mapper: (...args: any[]) => T, subject: Subject<T>): void {
    this.connection!.on(event, (...args: any[]) =>
      this.zone.run(() => subject.next(mapper(...args)))
    );
  }

  private wireHandlers(): void {
    if (!this.connection) return;

    this.on('DataChanged', (table: string, op: string, id: string) =>
      ({ table, op, id }), this.dataChanged$);

    this.on('PingResult', (hostname: string, success: boolean, latencyMs: number | null) =>
      ({ hostname, success, latencyMs }), this.pingResult$);

    this.on('SyncProgress', (hostname: string, status: string, progressPct: number) =>
      ({ hostname, status, progressPct }), this.syncProgress$);

    this.on('ConfigDrift', (hostname: string, changedLineCount: number) =>
      ({ hostname, changedLineCount }), this.configDrift$);

    this.on('NotificationEvent',
      (eventType: string, title: string, message: string, severity: string) =>
      ({ eventType, title, message, severity }), this.notificationEvt$);

    this.on('WebhookReceived', (source: string, webhookId: string) =>
      ({ source, webhookId }), this.webhookReceived$);
  }

  /**
   * Filter helper — `on('DataChanged').forTable('switches')` style.
   * Returns a stream of events for a specific table only.
   */
  dataChangedFor(table: string): Observable<DataChangedEvent> {
    return new Observable(sub => {
      const inner = this.dataChanged$.subscribe(e => {
        if (e.table === table) sub.next(e);
      });
      return () => inner.unsubscribe();
    });
  }
}
