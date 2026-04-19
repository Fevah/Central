import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, RenderedConfigSummary, RenderedConfigRecord,
  RenderDiff, DeviceListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Per-device config render history — list + body viewer + line-set
/// diff against the previous render. Read-only; config-gen render
/// writes still go through the WPF render panel (+ API POST) for
/// this slice.
///
/// Route `/network/render-history?device=uuid` — device uuid in a
/// query param so the device picker can swap without a new route.
/// On load with no device, fetches every device + preselects the
/// first; with a device id, fetches + loads the list immediately.
@Component({
  selector: 'app-network-render-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>Render history</h2>
      <small class="subtitle">Persisted config renders per device. Hash-chained via previousRenderId; line-set diff surfaces what changed across versions.</small>
    </div>

    <div class="top-bar">
      <label>Device</label>
      <dx-select-box class="lg" [items]="devices" [(value)]="deviceId"
                     valueExpr="id" displayExpr="hostname"
                     [searchEnabled]="true"
                     placeholder="Pick a device"
                     (onValueChanged)="onDeviceChanged()" />
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading || !deviceId" />
      <dx-button text="Render now" icon="save" type="default" stylingMode="contained"
                 hint="Render + persist a fresh config for this device. Requires write:Device."
                 (onClick)="renderNow()" [disabled]="rendering || !deviceId" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid *ngIf="deviceId"
                   [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   (onRowClick)="onRowClick($event)">
      <dxi-column dataField="renderedAt"       caption="Rendered at" width="180" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" [sortIndex]="0" sortOrder="desc" />
      <dxi-column dataField="flavorCode"       caption="Flavor"      width="100" />
      <dxi-column dataField="lineCount"        caption="Lines"       width="80"  dataType="number" />
      <dxi-column dataField="renderDurationMs" caption="ms"          width="70"  dataType="number" />
      <dxi-column dataField="renderedBy"       caption="By"          width="70"  dataType="number" />
      <dxi-column dataField="bodySha256"       caption="SHA-256"     width="260"
                  cellTemplate="shaTemplate" />
      <dxi-column dataField="previousRenderId" caption="Previous" />

      <div *dxTemplate="let d of 'shaTemplate'">
        <code class="sha">{{ d.value ? d.value.slice(0, 16) + '…' : '—' }}</code>
      </div>
    </dx-data-grid>

    <!-- Body viewer (selected render) -->
    <div *ngIf="selectedRecord" class="detail-block">
      <div class="detail-header">
        <strong>Render body</strong>
        <div class="detail-meta">
          <span>{{ selectedRecord.flavorCode }}</span>
          <span>{{ selectedRecord.lineCount }} lines</span>
          <span>SHA <code>{{ selectedRecord.bodySha256 }}</code></span>
        </div>
      </div>
      <pre class="config-body">{{ selectedRecord.body }}</pre>
    </div>

    <!-- Diff viewer (selected render vs previous) -->
    <div *ngIf="selectedDiff" class="detail-block">
      <div class="detail-header">
        <strong>Diff vs previous render</strong>
        <div class="detail-meta" *ngIf="selectedDiff.previousRenderId">
          <span>prev <code>{{ selectedDiff.previousRenderId }}</code></span>
          <span>{{ selectedDiff.unchangedCount }} unchanged</span>
        </div>
        <div class="detail-meta" *ngIf="!selectedDiff.previousRenderId">
          <em>No predecessor — first render for this device.</em>
        </div>
      </div>
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
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .top-bar { display: flex; gap: 8px; align-items: center; margin-bottom: 10px; }
    .top-bar label { color: #888; font-size: 12px; }
    .top-bar .lg { width: 320px; }
    .status-line { color: #666; font-size: 12px; }
    .sha { font-family: ui-monospace, monospace; font-size: 11px; color: #94a3b8; }
    .detail-block { margin-top: 16px; padding: 12px; background: #0f172a; border-radius: 6px; }
    .detail-header { display: flex; justify-content: space-between; align-items: baseline; gap: 12px; margin-bottom: 8px; }
    .detail-header strong { color: #cbd5e1; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; }
    .detail-meta { display: flex; gap: 12px; color: #64748b; font-size: 11px; }
    .detail-meta code { background: rgba(148,163,184,0.1); padding: 1px 4px; border-radius: 2px; color: #94a3b8; }
    .config-body { font-family: ui-monospace, monospace; font-size: 12px; color: #cbd5e1; margin: 0; white-space: pre; overflow: auto; max-height: 500px; padding: 8px; background: #020617; border-radius: 4px; }
    .diff-panels { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .diff-panel { background: #020617; border-radius: 4px; padding: 8px; }
    .diff-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 4px; font-weight: 600; }
    .diff-label.added    { color: #22c55e; }
    .diff-label.removed  { color: #ef4444; }
    .diff-body { font-family: ui-monospace, monospace; font-size: 12px; margin: 0; white-space: pre; overflow: auto; max-height: 300px; }
    .diff-body.added     { color: #22c55e; }
    .diff-body.removed   { color: #ef4444; }
    @media (max-width: 1100px) { .diff-panels { grid-template-columns: 1fr; } }
  `]
})
export class NetworkRenderHistoryComponent implements OnInit {
  devices: DeviceListRow[] = [];
  deviceId: string | null = null;
  rows: RenderedConfigSummary[] = [];
  selectedRecord: RenderedConfigRecord | null = null;
  selectedDiff: RenderDiff | null = null;

  loading = false;
  rendering = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.engine.listDevices(environment.defaultTenantId).subscribe({
      next: (devs) => {
        // Sort so the picker is predictable regardless of engine
        // list order; the thin list is already hostname-ordered but
        // sorting client-side keeps the UX stable if that changes.
        this.devices = [...devs].sort((a, b) =>
          a.hostname.localeCompare(b.hostname));

        const fromQuery = this.route.snapshot.queryParamMap.get('device');
        if (fromQuery && this.devices.some(d => d.id === fromQuery)) {
          this.deviceId = fromQuery;
        } else if (this.devices.length > 0) {
          this.deviceId = this.devices[0].id;
        }
        if (this.deviceId) this.reload();
      },
      error: (err) => {
        this.status = `Device list failed: ${err?.message ?? err}`;
      },
    });
  }

  /// Thread the device id through the query string so the view is
  /// shareable / bookmarkable. Router replaceUrl avoids a new
  /// history entry per device swap.
  onDeviceChanged(): void {
    this.selectedRecord = null;
    this.selectedDiff = null;
    this.rows = [];
    if (!this.deviceId) return;
    this.router.navigate([], {
      queryParams: { device: this.deviceId },
      replaceUrl: true,
    });
    this.reload();
  }

  reload(): void {
    if (!this.deviceId) return;
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listDeviceRenders(this.deviceId, environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} render${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = err?.status === 403
          ? 'Forbidden — your user lacks read:Device on this device.'
          : `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// POST /api/net/devices/:id/render-config to persist a fresh
  /// render, then reload the grid so the new row appears at the top
  /// (ordered by renderedAt DESC). The response body is used to
  /// populate the body/diff viewer so the operator sees exactly
  /// what just rendered without an extra click.
  renderNow(): void {
    if (!this.deviceId) return;
    this.rendering = true;
    this.status = 'Rendering…';
    this.engine.renderDeviceConfig(this.deviceId, environment.defaultTenantId).subscribe({
      next: (resp) => {
        this.rendering = false;
        this.status = `Rendered · ${resp.lineCount} lines · SHA ${resp.bodySha256.slice(0, 16)}…`;
        this.reload();
        // Populate the body viewer immediately from the response so
        // the operator isn't left clicking through the new row.
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
          // Diff requires the render's id — fetch it since the
          // POST response doesn't include the diff body.
          this.engine.getRenderDiff(resp.id, environment.defaultTenantId).subscribe({
            next: (diff) => { this.selectedDiff = diff; },
            error: () => { this.selectedDiff = null; },
          });
        }
      },
      error: (err) => {
        this.rendering = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.status = 'Forbidden — your user lacks write:Device on this device.';
        } else {
          this.status = `Render failed: ${err?.error?.detail ?? err?.message ?? err}`;
        }
      },
    });
  }

  /// Row click → fetch the full body + diff. Fired in parallel so a
  /// slow diff endpoint doesn't delay the body view.
  onRowClick(e: { data: RenderedConfigSummary }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.selectedRecord = null;
    this.selectedDiff = null;

    this.engine.getRender(row.id, environment.defaultTenantId).subscribe({
      next: (record) => { this.selectedRecord = record; },
      error: () => { this.selectedRecord = null; },
    });

    this.engine.getRenderDiff(row.id, environment.defaultTenantId).subscribe({
      next: (diff) => { this.selectedDiff = diff; },
      error: () => { this.selectedDiff = null; },
    });
  }
}
