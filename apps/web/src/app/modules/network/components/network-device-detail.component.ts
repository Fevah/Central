import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, DeviceListRow, AuditRow,
  RenderedConfigSummary, RenderedConfigRecord, RenderDiff,
  PortListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.device-backed detail page — companion to the thin /network/devices
/// grid. Four tabs: Summary (device row + basic metadata), Audit (last
/// 100 audit entries), Renders (last 20 render summaries + body +
/// diff viewer + "Render now"), Ports (every net.port on this device
/// with interface_name, speed, port_mode, native VLAN, description).
///
/// Distinct from /network/devices/:id which is the legacy switch_guide
/// editor page — this one is the net.* authoritative surface, routed
/// under /network/net-device/:id to keep the URL unambiguous.
@Component({
  selector: 'app-network-device-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/devices" class="back-link">← Devices</a>
      <h2 *ngIf="device">{{ device.hostname }}</h2>
      <h2 *ngIf="!device">Loading…</h2>
      <small *ngIf="device" class="subtitle">
        net.device · {{ device.roleCode ?? '(no role)' }} · {{ device.buildingCode ?? '(no building)' }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && device" class="meta-grid">
      <div class="meta-row"><label>Hostname</label>    <span>{{ device.hostname }}</span></div>
      <div class="meta-row"><label>Role code</label>   <span>{{ device.roleCode ?? '—' }}</span></div>
      <div class="meta-row"><label>Building code</label><span>{{ device.buildingCode ?? '—' }}</span></div>
      <div class="meta-row"><label>Status</label>      <span>{{ device.status }}</span></div>
      <div class="meta-row"><label>Version</label>     <span>{{ device.version }}</span></div>
      <div class="meta-row"><label>UUID</label>        <code>{{ device.id }}</code></div>

      <div class="meta-row">
        <label>Ports</label>
        <span>{{ portCount === null ? '…' : portCount }}</span>
      </div>
      <div class="meta-row">
        <label>Modules</label>
        <span>{{ moduleCount === null ? '…' : moduleCount }}</span>
      </div>
      <div class="meta-row">
        <label>Aggregate-ethernet</label>
        <span>{{ aeCount === null ? '…' : aeCount }}</span>
      </div>
    </div>

    <!-- Audit tab -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="audit" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true">
        <dxi-column dataField="sequenceId"    caption="Seq"           width="70" dataType="number" sortOrder="desc" [sortIndex]="0" />
        <dxi-column dataField="createdAt"     caption="At"            width="170" dataType="datetime"
                    format="yyyy-MM-dd HH:mm:ss" />
        <dxi-column dataField="action"        caption="Action"        width="120" />
        <dxi-column dataField="actorDisplay"  caption="Actor"         width="150" />
        <dxi-column dataField="correlationId" caption="Correlation"   width="240" />
      </dx-data-grid>
    </div>

    <!-- Renders tab -->
    <div *ngIf="activeTab === 2">
      <div class="toolbar">
        <dx-button text="Render now" icon="save" type="default"
                   hint="Render + persist a fresh config for this device"
                   (onClick)="renderNow()" [disabled]="rendering" />
        <span *ngIf="renderStatus" class="status-line">{{ renderStatus }}</span>
      </div>

      <dx-data-grid [dataSource]="renders" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     (onRowClick)="onRenderClick($event)">
        <dxi-column dataField="renderedAt"       caption="Rendered"  width="170" dataType="datetime"
                    format="yyyy-MM-dd HH:mm:ss" sortOrder="desc" [sortIndex]="0" />
        <dxi-column dataField="flavorCode"       caption="Flavor"    width="100" />
        <dxi-column dataField="lineCount"        caption="Lines"     width="80"  dataType="number" />
        <dxi-column dataField="renderDurationMs" caption="ms"        width="70"  dataType="number" />
        <dxi-column dataField="bodySha256"       caption="SHA"       cellTemplate="shaTemplate" />

        <div *dxTemplate="let d of 'shaTemplate'">
          <code class="sha">{{ d.value ? d.value.slice(0, 16) + '…' : '—' }}</code>
        </div>
      </dx-data-grid>

      <div *ngIf="selectedRecord" class="render-block">
        <div class="block-header">
          <strong>Body</strong>
          <span class="block-meta">{{ selectedRecord.flavorCode }} · {{ selectedRecord.lineCount }} lines</span>
        </div>
        <pre class="config-body">{{ selectedRecord.body }}</pre>
      </div>
      <div *ngIf="selectedDiff" class="render-block">
        <div class="block-header"><strong>Diff vs previous</strong></div>
        <div class="diff-panels" *ngIf="selectedDiff.previousRenderId">
          <div class="diff-panel">
            <div class="diff-label added">+ Added ({{ selectedDiff.added.length }})</div>
            <pre class="diff-body added">{{ selectedDiff.added.join('\n') || '(none)' }}</pre>
          </div>
          <div class="diff-panel">
            <div class="diff-label removed">− Removed ({{ selectedDiff.removed.length }})</div>
            <pre class="diff-body removed">{{ selectedDiff.removed.join('\n') || '(none)' }}</pre>
          </div>
        </div>
        <div *ngIf="!selectedDiff.previousRenderId"><em>First render — no predecessor.</em></div>
      </div>
    </div>

    <!-- Ports tab — every net.port on this device with the common
         operator fields pre-resolved. Grouped by interfacePrefix
         by default so xe- / ge- / et- cohorts cluster together.
         Alphabetical ORDER BY from the server means xe-1/1/10
         sorts before xe-1/1/2 on the wire; the grid's default
         sort accepts that (a natural-sort comparator lands if it
         becomes a complaint — not worth the complexity yet). -->
    <div *ngIf="activeTab === 3">
      <dx-data-grid [dataSource]="ports" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     [headerFilter]="{ visible: true }"
                     [groupPanel]="{ visible: true }">
        <dxi-column dataField="interfaceName"   caption="Interface"  [fixed]="true" width="160"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="interfacePrefix" caption="Prefix"     width="80"  [groupIndex]="0" />
        <dxi-column dataField="speedMbps"       caption="Speed (Mb)" width="100" dataType="number" />
        <dxi-column dataField="adminUp"         caption="Admin up"   width="90"  dataType="boolean" />
        <dxi-column dataField="portMode"        caption="Mode"       width="100" />
        <dxi-column dataField="nativeVlanId"    caption="Native VLAN" width="110" dataType="number" />
        <dxi-column dataField="status"          caption="Status"     width="90" />
        <dxi-column dataField="description"     caption="Description" />
      </dx-data-grid>
      <div *ngIf="ports.length === 0 && !loadingPorts" class="empty-note">
        No ports recorded for this device.
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .tab-bar { margin: 12px 0; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .sha { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 11px; }
    .render-block { margin-top: 12px; padding: 12px; background: #0f172a; border-radius: 6px; }
    .block-header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 8px; }
    .block-header strong { color: #cbd5e1; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; }
    .block-meta { color: #64748b; font-size: 11px; }
    .config-body { font-family: ui-monospace, monospace; font-size: 12px; color: #cbd5e1; margin: 0; white-space: pre; overflow: auto; max-height: 400px; padding: 8px; background: #020617; border-radius: 4px; }
    .diff-panels { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .diff-panel { background: #020617; border-radius: 4px; padding: 8px; }
    .diff-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 4px; font-weight: 600; }
    .diff-label.added { color: #22c55e; }
    .diff-label.removed { color: #ef4444; }
    .diff-body { font-family: ui-monospace, monospace; font-size: 12px; margin: 0; white-space: pre; overflow: auto; max-height: 250px; }
    .diff-body.added { color: #22c55e; }
    .diff-body.removed { color: #ef4444; }
    .empty-note { margin-top: 12px; padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } .diff-panels { grid-template-columns: 1fr; } }
  `]
})
export class NetworkDeviceDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }, { text: 'Renders' }, { text: 'Ports' }];
  activeTab = 0;

  deviceId = '';
  device: DeviceListRow | null = null;
  audit: AuditRow[] = [];
  renders: RenderedConfigSummary[] = [];
  selectedRecord: RenderedConfigRecord | null = null;
  selectedDiff: RenderDiff | null = null;
  ports: PortListRow[] = [];
  loadingPorts = false;

  /// Summary count enrichment — populated at page load via three
  /// thin-list narrower calls in parallel. Null = still loading.
  portCount: number | null = null;
  moduleCount: number | null = null;
  aeCount: number | null = null;

  rendering = false;
  status = '';
  renderStatus = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.deviceId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.deviceId) {
      this.status = 'Missing route param — expected /network/net-device/:id.';
      return;
    }
    // Load the device row from the thin list (no per-uuid endpoint yet);
    // filter client-side. For ~5k rows this is fine, but a future
    // per-uuid endpoint lands when the list cap becomes an issue.
    this.status = 'Loading…';
    this.engine.listDevices(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.device = rows.find(r => r.id === this.deviceId) ?? null;
        this.status = this.device ? '' : 'Device not found.';
        if (this.device) {
          this.loadTabData();
          this.loadSummaryCounts();
        }
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  /// Three parallel thin-list narrower calls — feed the port /
  /// module / AE counts on the Summary tab without waiting for the
  /// Ports tab to be opened. Silent-fail on individual errors so
  /// one failing call doesn't take out the whole summary.
  private loadSummaryCounts(): void {
    this.engine.listPorts(environment.defaultTenantId, this.deviceId).subscribe({
      next: (rows) => { this.portCount = rows?.length ?? 0; },
      error: () => { this.portCount = 0; },
    });
    this.engine.listModules(environment.defaultTenantId, this.deviceId).subscribe({
      next: (rows) => { this.moduleCount = rows?.length ?? 0; },
      error: () => { this.moduleCount = 0; },
    });
    this.engine.listAggregateEthernet(environment.defaultTenantId, this.deviceId).subscribe({
      next: (rows) => { this.aeCount = rows?.length ?? 0; },
      error: () => { this.aeCount = 0; },
    });
  }

  /// Load data for the currently-active tab. Fired on init + tab
  /// click. Caching each tab's data across switches so flipping
  /// tabs doesn't refetch.
  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'Device', this.deviceId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
    if (this.activeTab === 2 && this.renders.length === 0) {
      this.engine.listDeviceRenders(this.deviceId, environment.defaultTenantId, 20)
        .subscribe({ next: (rows) => { this.renders = rows; }, error: () => {} });
    }
    if (this.activeTab === 3 && this.ports.length === 0 && !this.loadingPorts) {
      this.loadingPorts = true;
      this.engine.listPorts(environment.defaultTenantId, this.deviceId).subscribe({
        next: (rows) => { this.ports = rows; this.loadingPorts = false; },
        error: () => { this.loadingPorts = false; },
      });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }

  onRenderClick(e: { data: RenderedConfigSummary }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.selectedRecord = null;
    this.selectedDiff = null;
    this.engine.getRender(row.id, environment.defaultTenantId).subscribe({
      next: (r) => { this.selectedRecord = r; }, error: () => {},
    });
    this.engine.getRenderDiff(row.id, environment.defaultTenantId).subscribe({
      next: (d) => { this.selectedDiff = d; }, error: () => {},
    });
  }

  /// Trigger a fresh render + reload the renders list. Also
  /// populates the body viewer from the POST response so the new
  /// row is visible immediately.
  renderNow(): void {
    if (!this.deviceId) return;
    this.rendering = true;
    this.renderStatus = 'Rendering…';
    this.engine.renderDeviceConfig(this.deviceId, environment.defaultTenantId).subscribe({
      next: (resp) => {
        this.rendering = false;
        this.renderStatus = `Rendered · ${resp.lineCount} lines · SHA ${resp.bodySha256.slice(0, 16)}…`;
        // Clear + refetch the list so the new row appears.
        this.renders = [];
        this.engine.listDeviceRenders(this.deviceId, environment.defaultTenantId, 20).subscribe({
          next: (rows) => { this.renders = rows; }, error: () => {},
        });
        // Pre-populate the body viewer.
        if (resp.id) {
          this.selectedRecord = {
            id: resp.id,
            deviceId: resp.deviceId,
            flavorCode: resp.flavorCode,
            body: resp.body,
            bodySha256: resp.bodySha256,
            lineCount: resp.lineCount,
            renderDurationMs: resp.renderDurationMs ?? null,
            previousRenderId: resp.previousRenderId ?? null,
            renderedAt: resp.renderedAt,
            renderedBy: null,
          };
          this.engine.getRenderDiff(resp.id, environment.defaultTenantId).subscribe({
            next: (diff) => { this.selectedDiff = diff; }, error: () => { this.selectedDiff = null; },
          });
        }
      },
      error: (err) => {
        this.rendering = false;
        this.renderStatus = err?.status === 403
          ? 'Forbidden — lacks write:Device.'
          : `Render failed: ${err?.error?.detail ?? err?.message ?? err}`;
      },
    });
  }
}
