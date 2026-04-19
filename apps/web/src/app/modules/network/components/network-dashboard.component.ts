import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxToolbarModule, DxChartModule } from 'devextreme-angular';
import { Subject, Subscription, merge } from 'rxjs';
import { auditTime, filter } from 'rxjs/operators';
import { NetworkService, SwitchDevice, DeviceRecord } from '../../../core/services/network.service';
import { NetworkingEngineService, WhoAmI } from '../../../core/services/networking-engine.service';
import { ModuleRegistryService } from '../../../core/services/module-registry.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { environment } from '../../../../environments/environment';
import notify from 'devextreme/ui/notify';

@Component({
  selector: 'app-network-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxToolbarModule, DxChartModule],
  template: `
    <!-- Session banner — resolves via /api/net/whoami on init.
         Hidden when whoami returns userId=null (service-call fallback). -->
    <div *ngIf="whoami && whoami.userId !== null" class="session-banner">
      <div class="banner-left">
        <span class="banner-label">Signed in as</span>
        <span class="banner-user">user {{ whoami.userId }}</span>
      </div>
      <div class="banner-right">
        <span class="banner-chip">{{ whoami.grantCount }} grants</span>
        <span *ngIf="whoami.actions.length" class="banner-chip actions"
              title="Actions granted across all scopes">
          {{ whoami.actions.join(' · ') }}
        </span>
        <span *ngIf="whoami.entityTypes.length" class="banner-chip entities"
              title="Entity types granted across all scopes">
          {{ whoami.entityTypes.join(' · ') }}
        </span>
      </div>
    </div>

    <!-- Sub-nav: only show modules the tenant has licensed -->
    <div class="sub-nav">
      <a routerLink="/network" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Overview</a>
      <a routerLink="/network/links" routerLinkActive="active" *ngIf="modules.isEnabled('links')">Links</a>
      <a routerLink="/network/bgp"   routerLinkActive="active" *ngIf="modules.isEnabled('routing')">BGP</a>
      <a routerLink="/network/search" routerLinkActive="active">Search</a>
      <a routerLink="/network/validation" routerLinkActive="active">Validation</a>
      <a routerLink="/network/data-quality" routerLinkActive="active">Data quality</a>
      <a routerLink="/network/scope-grants" routerLinkActive="active">Scope grants</a>
      <a routerLink="/network/hierarchy" routerLinkActive="active">Hierarchy</a>
      <a routerLink="/network/pools" routerLinkActive="active">Pools</a>
      <a routerLink="/network/bulk" routerLinkActive="active">Bulk</a>
      <a routerLink="/network/devices" routerLinkActive="active">Devices (net.*)</a>
      <a routerLink="/network/vlans" routerLinkActive="active">VLANs</a>
      <a routerLink="/network/servers" routerLinkActive="active">Servers</a>
      <a routerLink="/network/links-grid" routerLinkActive="active">Links (net.*)</a>
      <a routerLink="/network/subnets" routerLinkActive="active">Subnets</a>
      <a routerLink="/network/ports" routerLinkActive="active">Ports</a>
      <a routerLink="/network/aggregate-ethernet" routerLinkActive="active">AE bundles</a>
      <a routerLink="/network/mlag-domains" routerLinkActive="active">MLAG</a>
      <a routerLink="/network/modules" routerLinkActive="active">Modules</a>
      <a routerLink="/network/mstp-rules" routerLinkActive="active">MSTP</a>
      <a routerLink="/network/reservation-shelf" routerLinkActive="active">Shelf</a>
      <a routerLink="/network/vlan-blocks" routerLinkActive="active">VLAN blocks</a>
      <a routerLink="/network/asn-blocks" routerLinkActive="active">ASN blocks</a>
      <a routerLink="/network/asn-allocations" routerLinkActive="active">ASN alloc</a>
      <a routerLink="/network/rooms" routerLinkActive="active">Rooms</a>
      <a routerLink="/network/racks" routerLinkActive="active">Racks</a>
      <a routerLink="/network/ip-addresses" routerLinkActive="active">IPs</a>
      <a routerLink="/network/dhcp-relay" routerLinkActive="active">DHCP relay</a>
      <a routerLink="/network/change-sets" routerLinkActive="active">Change sets</a>
      <a routerLink="/network/locks" routerLinkActive="active">Locks</a>
      <a routerLink="/network/naming-preview" routerLinkActive="active">Naming preview</a>
      <a routerLink="/network/naming-overrides" routerLinkActive="active">Naming overrides</a>
      <a routerLink="/network/naming-resolve" routerLinkActive="active">Naming resolve</a>
      <a routerLink="/network/render-history" routerLinkActive="active">Render history</a>
      <a routerLink="/network/render-pack" routerLinkActive="active">Turn-up pack</a>
      <a routerLink="/network/pool-utilization" routerLinkActive="active">Pool utilization</a>
      <a routerLink="/network/cli-flavors" routerLinkActive="active">CLI flavors</a>
      <a routerLink="/network/audit-stats" routerLinkActive="active">Audit stats</a>
      <a routerLink="/network/audit-search" routerLinkActive="active">Audit search</a>
    </div>

    <!-- Summary cards -->
    <div class="stat-cards">
      <div class="stat-card online"><div class="stat-value">{{ onlineCount }}</div><div class="stat-label">Online</div></div>
      <div class="stat-card offline"><div class="stat-value">{{ offlineCount }}</div><div class="stat-label">Offline</div></div>
      <div class="stat-card devices"><div class="stat-value">{{ devices.length }}</div><div class="stat-label">Devices</div></div>
    </div>

    <!-- Switch Grid -->
    <dx-toolbar class="section-toolbar">
      <dxi-item location="before"><div class="section-title">Switches</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refreshSwitches }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="switches" [showBorders]="true" [rowAlternationEnabled]="true"
                   [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                   [searchPanel]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [hoverStateEnabled]="true"
                   (onRowClick)="onSwitchClick($event)"
                   height="320">
      <dxi-column dataField="hostname" caption="Switch" [fixed]="true" width="160"
                  cellTemplate="hostnameTemplate" />
      <dxi-column dataField="site" caption="Site" width="100" />
      <dxi-column dataField="role" caption="Role" width="80" />
      <dxi-column dataField="management_ip" caption="Mgmt IP" width="130" />
      <dxi-column dataField="last_ping_ok" caption="Ping" width="60" cellTemplate="pingTemplate" />
      <dxi-column dataField="last_ping_ms" caption="Latency" width="80" cellTemplate="latencyTemplate" />
      <dxi-column dataField="last_ssh_ok" caption="SSH" width="60" cellTemplate="sshTemplate" />
      <dxi-column dataField="picos_version" caption="PicOS" width="100" />

      <div *dxTemplate="let d of 'hostnameTemplate'">
        <strong>{{ d.value }}</strong>
      </div>
      <div *dxTemplate="let d of 'pingTemplate'">
        <span [class]="d.value === true ? 'status-ok' : d.value === false ? 'status-err' : 'status-unk'">●</span>
      </div>
      <div *dxTemplate="let d of 'latencyTemplate'">
        <span *ngIf="d.value != null">{{ d.value | number:'1.0-0' }}ms</span>
      </div>
      <div *dxTemplate="let d of 'sshTemplate'">
        <span [class]="d.value === true ? 'status-ok' : d.value === false ? 'status-err' : 'status-unk'">●</span>
      </div>
    </dx-data-grid>

    <!-- Device Grid -->
    <dx-toolbar class="section-toolbar" style="margin-top: 24px;">
      <dxi-item location="before"><div class="section-title">IPAM Devices</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refreshDevices }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="devices" [showBorders]="true" [rowAlternationEnabled]="true"
                   [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                   [searchPanel]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   [hoverStateEnabled]="true"
                   (onRowClick)="onDeviceClick($event)"
                   height="400">
      <dxi-column dataField="switch_name" caption="Device" width="160" />
      <dxi-column dataField="building" caption="Building" width="100" [groupIndex]="0" />
      <dxi-column dataField="device_type" caption="Type" width="100" />
      <dxi-column dataField="primary_ip" caption="Primary IP" width="130" />
      <dxi-column dataField="management_ip" caption="Mgmt IP" width="130" />
      <dxi-column dataField="status" caption="Status" width="90" cellTemplate="statusTemplate" />
      <dxi-column dataField="asn" caption="ASN" width="80" />
      <dxi-column dataField="region" caption="Region" width="90" />

      <div *dxTemplate="let d of 'statusTemplate'">
        <span [class]="'badge badge-' + (d.value || 'unknown').toLowerCase()">{{ d.value }}</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .session-banner { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 8px 12px; margin-bottom: 12px; background: rgba(59,130,246,0.08); border: 1px solid rgba(59,130,246,0.2); border-radius: 4px; font-size: 12px; flex-wrap: wrap; }
    .banner-label { color: #9ca3af; }
    .banner-user { color: #60a5fa; font-weight: 600; font-family: ui-monospace, monospace; margin-left: 4px; }
    .banner-right { display: flex; gap: 6px; flex-wrap: wrap; }
    .banner-chip { padding: 2px 8px; border-radius: 10px; background: rgba(148,163,184,0.1); color: #cbd5e1; font-size: 11px; }
    .banner-chip.actions  { color: #22c55e; background: rgba(34,197,94,0.08); }
    .banner-chip.entities { color: #a855f7; background: rgba(168,85,247,0.08); }
    .sub-nav { display: flex; gap: 8px; margin-bottom: 16px; border-bottom: 1px solid #1f2937; padding-bottom: 8px; }
    .sub-nav a { color: #9ca3af; text-decoration: none; padding: 6px 12px; border-radius: 6px; font-size: 13px; }
    .sub-nav a:hover { background: rgba(59,130,246,0.1); color: #d1d5db; }
    .sub-nav a.active { background: rgba(59,130,246,0.2); color: #60a5fa; }
    .stat-cards { display: flex; gap: 16px; margin-bottom: 16px; }
    .stat-card { flex: 1; padding: 20px; border-radius: 8px; text-align: center; }
    .stat-card.online { background: rgba(34,197,94,0.1); border: 1px solid rgba(34,197,94,0.3); }
    .stat-card.offline { background: rgba(239,68,68,0.1); border: 1px solid rgba(239,68,68,0.3); }
    .stat-card.devices { background: rgba(59,130,246,0.1); border: 1px solid rgba(59,130,246,0.3); }
    .stat-value { font-size: 32px; font-weight: bold; }
    .stat-label { font-size: 12px; color: #9ca3af; text-transform: uppercase; letter-spacing: 1px; }
    .online .stat-value { color: #22c55e; } .offline .stat-value { color: #ef4444; } .devices .stat-value { color: #3b82f6; }
    .section-toolbar { margin-bottom: 8px; }
    .section-title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .status-ok { color: #22c55e; font-size: 16px; } .status-err { color: #ef4444; font-size: 16px; } .status-unk { color: #6b7280; font-size: 16px; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-active { background: rgba(34,197,94,0.2); color: #22c55e; }
    .badge-reserved { background: rgba(234,179,8,0.2); color: #eab308; }
    .badge-decommissioned { background: rgba(107,114,128,0.2); color: #6b7280; }
  `]
})
export class NetworkDashboardComponent implements OnInit, OnDestroy {
  switches: SwitchDevice[] = [];
  devices: DeviceRecord[] = [];
  /// Resolved via /api/net/whoami on init. Null until the call
  /// completes; the banner only renders when whoami.userId != null
  /// so service-call fallbacks don't flash an empty banner.
  whoami: WhoAmI | null = null;

  get onlineCount(): number { return this.switches.filter(s => s.last_ping_ok === true).length; }
  get offlineCount(): number { return this.switches.filter(s => s.last_ping_ok === false).length; }

  /** Coalesce bursts of DataChanged events into a single reload per source. */
  private readonly switchReload$ = new Subject<void>();
  private readonly deviceReload$ = new Subject<void>();
  private rtSub?: Subscription;

  constructor(
    private networkService: NetworkService,
    private router: Router,
    public modules: ModuleRegistryService,
    private signalR: SignalRService,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.loadSwitches();
    this.loadDevices();
    this.subscribeRealtime();
    this.loadWhoAmI();
  }

  /// Fetches the session summary. Soft-fails to null on error —
  /// banner just doesn't render rather than showing an error state,
  /// since the page is still functional without it.
  private loadWhoAmI(): void {
    this.engine.whoAmI(environment.defaultTenantId).subscribe({
      next: (w) => { this.whoami = w; },
      error: () => { this.whoami = null; },
    });
  }

  ngOnDestroy(): void {
    this.rtSub?.unsubscribe();
  }

  /**
   * React to live DataChanged + PingResult events. We funnel into Subjects
   * with `auditTime(500)` so a burst of 50 row updates from a config sync
   * triggers ONE re-fetch, not 50.
   */
  private subscribeRealtime(): void {
    // Reload triggers — coalesced.
    this.rtSub = this.switchReload$.pipe(auditTime(500))
      .subscribe(() => this.loadSwitches());
    this.rtSub.add(this.deviceReload$.pipe(auditTime(500))
      .subscribe(() => this.loadDevices()));

    // Filter SignalR DataChanged events for tables we care about.
    this.rtSub.add(
      this.signalR.dataChanged$.pipe(
        filter(e => ['switches', 'switch_guide', 'running_configs', 'bgp_config'].includes(e.table))
      ).subscribe(e => {
        if (e.table === 'switch_guide') this.deviceReload$.next();
        else                            this.switchReload$.next();
      })
    );

    // PingResult patches an in-memory row so the green/red dot flips
    // immediately without a full grid re-fetch.
    this.rtSub.add(
      this.signalR.pingResult$.subscribe(p => {
        const sw = this.switches.find(s => s.hostname === p.hostname);
        if (!sw) return;
        sw.last_ping_ok = p.success;
        sw.last_ping_ms = p.latencyMs;
        // Force grid to notice the mutation by replacing the array reference.
        this.switches = [...this.switches];
      })
    );
  }

  /** Drill into the switch detail page on row click. */
  onSwitchClick(e: any): void {
    const id = e?.data?.id;
    if (id) this.router.navigate(['/network/switches', id]);
  }

  /** Drill into the device detail page on row click. */
  onDeviceClick(e: any): void {
    const id = e?.data?.id;
    if (id) this.router.navigate(['/network/devices', id]);
  }

  loadSwitches(): void {
    this.networkService.getSwitches().subscribe({
      next: s => this.switches = s,
      error: () => notify('Failed to load switches', 'error', 3000)
    });
  }

  loadDevices(): void {
    this.networkService.getDevices().subscribe({
      next: d => this.devices = d,
      error: () => notify('Failed to load devices', 'error', 3000)
    });
  }

  refreshSwitches = (): void => { this.loadSwitches(); notify('Switches refreshed', 'info', 1000); };
  refreshDevices = (): void => { this.loadDevices(); notify('Devices refreshed', 'info', 1000); };
}
