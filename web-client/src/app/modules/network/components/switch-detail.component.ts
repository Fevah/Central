import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxToolbarModule, DxTabPanelModule, DxButtonModule } from 'devextreme-angular';
import { Subscription } from 'rxjs';
import { auditTime, filter } from 'rxjs/operators';
import notify from 'devextreme/ui/notify';
import { NetworkService, SwitchInterface, ConfigVersion } from '../../../core/services/network.service';
import { SignalRService } from '../../../core/services/signalr.service';

/**
 * Switch detail page — mirrors the WPF Asset Details panel.
 *
 * Loaded when the user clicks a switch on the network dashboard.
 * Tabs: Overview, Interfaces, Config Versions. BGP/VLAN tabs
 * will be added in Phase 2 once their endpoints are ready.
 *
 * The :host route param is actually the numeric switch ID (we kept
 * the param name `host` for nicer URLs but it's an int).
 */
@Component({
  selector: 'app-switch-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxToolbarModule, DxTabPanelModule, DxButtonModule],
  template: `
    <dx-toolbar class="page-toolbar">
      <dxi-item location="before">
        <a routerLink="/network" class="back-link">← Network</a>
      </dxi-item>
      <dxi-item location="before">
        <div class="page-title">
          <strong>{{ sw?.hostname || 'Switch' }}</strong>
          <span class="meta">{{ sw?.site }} · {{ sw?.role }} · {{ sw?.management_ip }}</span>
        </div>
      </dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', text: 'Ping',     onClick: doPing }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'download', text: 'Download config', onClick: doDownload }"></dxi-item>
    </dx-toolbar>

    <!-- Live sync progress banner — shows while a config download is in flight -->
    <div class="sync-banner" *ngIf="syncStatus">
      <span class="dot pulse"></span>
      <span class="sync-text">{{ syncStatus.status }}</span>
      <span class="sync-pct" *ngIf="syncStatus.pct >= 0">{{ syncStatus.pct }}%</span>
    </div>

    <div class="status-pills" *ngIf="sw">
      <span class="pill" [class.ok]="sw.last_ping_ok === true" [class.err]="sw.last_ping_ok === false">
        Ping: <strong>{{ sw.last_ping_ok === null ? '–' : sw.last_ping_ok ? 'OK' : 'FAIL' }}</strong>
        <span *ngIf="sw.last_ping_ms != null">({{ sw.last_ping_ms }} ms)</span>
      </span>
      <span class="pill" [class.ok]="sw.last_ssh_ok === true" [class.err]="sw.last_ssh_ok === false">
        SSH: <strong>{{ sw.last_ssh_ok === null ? '–' : sw.last_ssh_ok ? 'OK' : 'FAIL' }}</strong>
      </span>
      <span class="pill">PicOS: <strong>{{ sw.picos_version || 'unknown' }}</strong></span>
    </div>

    <dx-tab-panel [items]="tabs" [selectedIndex]="0" [animationEnabled]="false" class="detail-tabs">
      <div *dxTemplate="let tab of 'item'">
        <ng-container [ngSwitch]="tab.key">

          <!-- Overview -->
          <div *ngSwitchCase="'overview'" class="tab-body">
            <table class="kv-table" *ngIf="sw">
              <tr *ngFor="let row of overviewRows">
                <th>{{ row.label }}</th>
                <td>{{ row.value ?? '–' }}</td>
              </tr>
            </table>
          </div>

          <!-- Interfaces -->
          <div *ngSwitchCase="'interfaces'" class="tab-body">
            <dx-data-grid [dataSource]="interfaces" [showBorders]="true" [columnAutoWidth]="true"
                          [searchPanel]="{ visible: true }" [filterRow]="{ visible: true }"
                          height="500">
              <dxi-column dataField="interface_name" caption="Interface" [fixed]="true" width="160" />
              <dxi-column dataField="description" caption="Description" />
              <dxi-column dataField="speed" caption="Speed" width="90" />
              <dxi-column dataField="vlan" caption="VLAN" width="80" />
              <dxi-column dataField="mode" caption="Mode" width="100" />
              <dxi-column dataField="admin_status" caption="Admin" width="80" />
            </dx-data-grid>
            <div class="empty-state" *ngIf="!interfacesLoading && interfaces.length === 0">
              No interfaces returned for this switch.
            </div>
          </div>

          <!-- Config versions -->
          <div *ngSwitchCase="'configs'" class="tab-body">
            <dx-data-grid [dataSource]="configs" [showBorders]="true" [columnAutoWidth]="true"
                          height="500">
              <dxi-column dataField="version" caption="Version" width="100" />
              <dxi-column dataField="created_at" caption="Captured" dataType="datetime" width="180" />
              <dxi-column dataField="byte_count" caption="Size" width="100" />
              <dxi-column dataField="diff_summary" caption="Diff Summary" />
            </dx-data-grid>
            <div class="empty-state" *ngIf="!configsLoading && configs.length === 0">
              No config snapshots have been captured yet — use “Download config” above.
            </div>
          </div>

        </ng-container>
      </div>
    </dx-tab-panel>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .back-link { color: #60a5fa; text-decoration: none; margin-right: 12px; font-size: 13px; }
    .back-link:hover { text-decoration: underline; }
    .page-title { font-size: 16px; color: #f9fafb; }
    .page-title .meta { color: #9ca3af; font-size: 12px; margin-left: 12px; }
    .status-pills { display: flex; gap: 8px; margin-bottom: 16px; }
    .pill { background: rgba(107,114,128,0.15); border: 1px solid rgba(107,114,128,0.3); color: #d1d5db;
            padding: 4px 12px; border-radius: 999px; font-size: 12px; }
    .pill.ok  { background: rgba(34,197,94,0.15);  border-color: rgba(34,197,94,0.4);  color: #22c55e; }
    .pill.err { background: rgba(239,68,68,0.15);  border-color: rgba(239,68,68,0.4);  color: #ef4444; }
    .sync-banner {
      display: flex; align-items: center; gap: 10px; padding: 8px 14px;
      background: rgba(59,130,246,0.1); border: 1px solid rgba(59,130,246,0.3);
      border-radius: 6px; margin-bottom: 12px; color: #93c5fd; font-size: 13px;
    }
    .sync-banner .dot { width: 8px; height: 8px; border-radius: 50%; background: #60a5fa; }
    .sync-banner .pulse { animation: pulse 1.4s ease-in-out infinite; }
    .sync-banner .sync-text { text-transform: capitalize; }
    .sync-banner .sync-pct  { margin-left: auto; font-weight: 600; }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50%      { opacity: 0.3; }
    }
    .detail-tabs { margin-top: 8px; }
    .tab-body { padding: 12px 0; }
    .kv-table { width: 100%; max-width: 720px; }
    .kv-table th { text-align: left; padding: 6px 16px 6px 0; color: #9ca3af; font-weight: 500; width: 180px; }
    .kv-table td { padding: 6px 0; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
  `]
})
export class SwitchDetailComponent implements OnInit, OnDestroy {
  sw: any = null;
  interfaces: SwitchInterface[] = [];
  configs: ConfigVersion[] = [];
  interfacesLoading = false;
  configsLoading = false;

  /** SyncProgress display while a config download/sync is in flight. */
  syncStatus: { status: string; pct: number } | null = null;
  private rtSub?: Subscription;

  readonly tabs = [
    { title: 'Overview',        key: 'overview' },
    { title: 'Interfaces',      key: 'interfaces' },
    { title: 'Config Versions', key: 'configs' },
  ];

  /** Rendered as the Overview tab — keeps row order stable. */
  get overviewRows(): { label: string; value: any }[] {
    if (!this.sw) return [];
    return [
      { label: 'Hostname',       value: this.sw.hostname },
      { label: 'Site',           value: this.sw.site },
      { label: 'Role',           value: this.sw.role },
      { label: 'Management IP',  value: this.sw.management_ip },
      { label: 'Loopback IP',    value: this.sw.loopback_ip },
      { label: 'PicOS Version',  value: this.sw.picos_version },
      { label: 'SSH Username',   value: this.sw.ssh_username },
      { label: 'SSH Port',       value: this.sw.ssh_port },
      { label: 'Last Ping',      value: this.sw.last_ping_at },
      { label: 'Last SSH Check', value: this.sw.last_ssh_at },
    ];
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private network: NetworkService,
    private signalR: SignalRService,
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('host'));
    if (!Number.isFinite(id) || id <= 0) {
      notify('Invalid switch ID', 'error', 3000);
      this.router.navigate(['/network']);
      return;
    }
    this.loadAll(id);
    this.subscribeRealtime(id);
  }

  ngOnDestroy(): void {
    this.rtSub?.unsubscribe();
  }

  /**
   * Live feed:
   *  - PingResult for THIS switch → patch the status pill in place
   *  - SyncProgress for THIS switch → show inline progress
   *  - DataChanged on running_configs/bgp_config for this id → reload configs tab
   *  - ConfigDrift → toast (operator should compare versions)
   */
  private subscribeRealtime(id: number): void {
    this.rtSub = this.signalR.pingResult$
      .pipe(filter(p => this.sw?.hostname && p.hostname === this.sw.hostname))
      .subscribe(p => {
        this.sw = { ...this.sw, last_ping_ok: p.success, last_ping_ms: p.latencyMs };
      });

    this.rtSub.add(
      this.signalR.syncProgress$
        .pipe(filter(s => this.sw?.hostname && s.hostname === this.sw.hostname))
        .subscribe(s => {
          this.syncStatus = { status: s.status, pct: s.progressPct };
          if (s.status === 'complete' || s.status === 'failed') {
            // Clear the banner after a beat so the user sees the final state.
            setTimeout(() => this.syncStatus = null, 2500);
            if (s.status === 'complete') this.loadAll(id);
          }
        })
    );

    this.rtSub.add(
      this.signalR.configDrift$
        .pipe(filter(d => this.sw?.hostname && d.hostname === this.sw.hostname))
        .subscribe(d => {
          notify(`Config drift detected: ${d.changedLineCount} line(s) changed`,
                 'warning', 4000);
          this.loadAll(id);
        })
    );

    // Reload configs tab when a new running_configs row is inserted for this switch.
    this.rtSub.add(
      this.signalR.dataChanged$
        .pipe(
          filter(e => e.table === 'running_configs' && Number(e.id) === id),
          auditTime(500)
        )
        .subscribe(() => this.loadAll(id))
    );
  }

  private loadAll(id: number): void {
    this.network.getSwitch(id).subscribe({
      next: s => this.sw = s,
      error: () => notify('Failed to load switch', 'error', 3000)
    });

    this.interfacesLoading = true;
    this.network.getSwitchInterfaces(id).subscribe({
      next: i => { this.interfaces = i; this.interfacesLoading = false; },
      error: () => { this.interfacesLoading = false; notify('Failed to load interfaces', 'error', 3000); }
    });

    this.configsLoading = true;
    this.network.getSwitchConfigVersions(id).subscribe({
      next: c => { this.configs = c; this.configsLoading = false; },
      error: () => { this.configsLoading = false; /* config versions are optional */ }
    });
  }

  doPing = (): void => {
    if (!this.sw?.id) return;
    this.network.pingSwitch(this.sw.id).subscribe({
      next: () => { notify('Ping queued', 'success', 1500); this.loadAll(this.sw.id); },
      error: () => notify('Ping failed', 'error', 3000)
    });
  };

  doDownload = (): void => {
    if (!this.sw?.id) return;
    this.network.downloadSwitchConfig(this.sw.id).subscribe({
      next: () => { notify('Config download started', 'success', 1500); this.loadAll(this.sw.id); },
      error: () => notify('Config download failed', 'error', 3000)
    });
  };
}
