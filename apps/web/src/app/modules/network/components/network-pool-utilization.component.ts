import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, PoolUtilizationRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// "Am I about to run out of VLANs?" dashboard — single grid with
/// usage vs capacity across ASN / VLAN / IP pool families, driven
/// by /api/net/pools/utilization. IP pools emit two rows (Subnets
/// + Addresses) so the operator sees both dimensions without a
/// second query.
///
/// Visual: DxDataGrid grouped by poolKind with a progress-bar cell
/// template on percentFull so the scan surfaces hot pools fast.
/// The progress bar colours itself by threshold: green < 50%, amber
/// 50-80%, red > 80%.
@Component({
  selector: 'app-network-pool-utilization',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/pools" class="back-link">← Pools</a>
      <h2>Pool utilization</h2>
      <small class="subtitle">Used vs capacity across every ASN / VLAN / IP pool in the tenant. Percent full capped at 999 — a value over 100% is a data-quality issue worth investigating.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="poolKind"    caption="Kind"      width="130" [groupIndex]="0" />
      <dxi-column dataField="poolCode"    caption="Pool code" [fixed]="true" width="200" sortOrder="asc" />
      <dxi-column dataField="displayName" caption="Name"      width="260" />
      <dxi-column dataField="used"        caption="Used"      width="100" dataType="number" />
      <dxi-column dataField="capacity"    caption="Capacity"  width="120" dataType="number"
                  cellTemplate="capacityTemplate" />
      <dxi-column dataField="percentFull" caption="Full"      width="160"
                  cellTemplate="percentTemplate" />
      <dxi-column dataField="status"      caption="Status"    width="90" />
      <dxi-column dataField="poolId"      caption="UUID"      width="260" />

      <div *dxTemplate="let d of 'capacityTemplate'">
        <span *ngIf="d.value > 0">{{ d.value | number }}</span>
        <span *ngIf="d.value === 0" class="muted">—</span>
      </div>

      <div *dxTemplate="let d of 'percentTemplate'">
        <div *ngIf="d.data.capacity > 0" class="bar-wrap">
          <div class="bar"
               [class.low]="d.value < 50"
               [class.mid]="d.value >= 50 && d.value < 80"
               [class.high]="d.value >= 80"
               [style.width.%]="clampBar(d.value)">
          </div>
          <span class="bar-label">{{ d.value }}%</span>
        </div>
        <span *ngIf="d.data.capacity === 0" class="muted">—</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    .muted { color: #64748b; font-size: 12px; }
    .bar-wrap { position: relative; height: 20px; background: rgba(148,163,184,0.1); border-radius: 3px; overflow: hidden; }
    .bar { height: 100%; transition: width 0.2s ease; }
    .bar.low  { background: rgba(34,197,94,0.6); }
    .bar.mid  { background: rgba(234,179,8,0.6); }
    .bar.high { background: rgba(239,68,68,0.6); }
    .bar-label { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 600; color: #f8fafc; }
  `]
})
export class NetworkPoolUtilizationComponent implements OnInit {
  rows: PoolUtilizationRow[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void { this.reload(); }

  /// IP pool rows drill to /network/ip-pool/:id. VLAN + ASN + MLAG
  /// pools stay on this page for now — they don't have dedicated
  /// detail pages yet. Works for both the "IP:Subnets" and
  /// "IP:Addresses" rows (both share the same poolId).
  onRowDoubleClick(e: { data: PoolUtilizationRow }): void {
    const kind = e?.data?.poolKind ?? '';
    if (!kind.startsWith('IP')) return;
    const id = e?.data?.poolId;
    if (!id) return;
    this.router.navigate(['/network/ip-pool', id]);
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.poolUtilization(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} pool dimension${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Cap the bar width at 100% visually even when percentFull is
  /// higher (e.g. 150% from a data-quality issue) — the label still
  /// shows the real number, but the bar doesn't overflow.
  clampBar(percent: number): number {
    return Math.min(100, Math.max(0, percent));
  }
}
