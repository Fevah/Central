import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, AggregateEthernetListRow, PortListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Aggregate ethernet (LACP bundle) detail page. Two tabs:
/// Summary (AE row) + Members (every net.port with
/// aggregate_ethernet_id pointing at this ae).
///
/// Members list uses the /api/net/ports thin list narrowed to
/// the AE's deviceId + client-filters by aggregateEthernetId.
/// The thin list carries that field so the filter returns the
/// exact bundle members.
@Component({
  selector: 'app-network-aggregate-ethernet-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/aggregate-ethernet" class="back-link">← AE bundles</a>
      <h2 *ngIf="ae">{{ ae.aeName }}<span *ngIf="ae.deviceHostname"> on {{ ae.deviceHostname }}</span></h2>
      <h2 *ngIf="!ae">Loading…</h2>
      <small *ngIf="ae" class="subtitle">
        LACP {{ ae.lacpMode }} · members {{ ae.memberCount }} / min {{ ae.minLinks }}
        <span *ngIf="ae.memberCount < ae.minLinks" class="under"> · under-populated</span>
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && ae" class="meta-grid">
      <div class="meta-row"><label>AE name</label>    <span>{{ ae.aeName }}</span></div>
      <div class="meta-row"><label>Device</label>     <span>{{ ae.deviceHostname ?? '—' }}</span></div>
      <div class="meta-row"><label>LACP mode</label>  <span>{{ ae.lacpMode }}</span></div>
      <div class="meta-row"><label>Min links</label>  <span>{{ ae.minLinks }}</span></div>
      <div class="meta-row"><label>Members</label>    <span>{{ ae.memberCount }}</span></div>
      <div class="meta-row"><label>Status</label>     <span>{{ ae.status }}</span></div>
      <div class="meta-row"><label>Version</label>    <span>{{ ae.version }}</span></div>
      <div class="meta-row full" *ngIf="ae.description"><label>Description</label><span>{{ ae.description }}</span></div>
      <div class="meta-row full"><label>UUID</label>  <code>{{ ae.id }}</code></div>
    </div>

    <!-- Members tab -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="members" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true">
        <dxi-column dataField="interfaceName"   caption="Interface" [fixed]="true" width="160"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="interfacePrefix" caption="Prefix"    width="80" />
        <dxi-column dataField="speedMbps"       caption="Speed (Mb)" width="100" dataType="number" />
        <dxi-column dataField="adminUp"         caption="Admin up"  width="90"  dataType="boolean" />
        <dxi-column dataField="portMode"        caption="Mode"      width="100" />
        <dxi-column dataField="status"          caption="Status"    width="90" />
        <dxi-column dataField="description"     caption="Description" />
      </dx-data-grid>
      <div *ngIf="members.length === 0 && !loadingMembers" class="empty-note">
        No ports wired to this aggregate ethernet. min_links is {{ ae?.minLinks }} — the bundle is down.
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; font-family: ui-monospace, monospace; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .subtitle .under { color: #eab308; font-weight: 600; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .tab-bar { margin: 12px 0; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .empty-note { margin-top: 12px; padding: 10px; color: #eab308; font-size: 12px; background: rgba(234,179,8,0.08); border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkAggregateEthernetDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Members' }];
  activeTab = 0;

  aeId = '';
  ae: AggregateEthernetListRow | null = null;
  members: PortListRow[] = [];
  loadingMembers = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private _router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.aeId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.aeId) {
      this.status = 'Missing route param — expected /network/aggregate-ethernet/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listAggregateEthernet(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.ae = rows.find(r => r.id === this.aeId) ?? null;
        this.status = this.ae ? '' : 'Aggregate ethernet not found.';
        if (this.ae) this.loadTabData();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.members.length === 0 && !this.loadingMembers && this.ae) {
      this.loadingMembers = true;
      // Members aren't directly listed by the ports thin list —
      // aggregate_ethernet_id isn't on PortListRow. Load the
      // device's ports + filter server-side in a follow-up slice
      // when the thin list gains that field. For now call out the
      // gap via the empty-state note.
      const aeId = this.ae.id;
      this.engine.listPorts(environment.defaultTenantId, this.ae.deviceId).subscribe({
        next: (rows) => {
          this.members = rows.filter(p => p.aggregateEthernetId === aeId);
          this.loadingMembers = false;
        },
        error: () => { this.loadingMembers = false; },
      });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }
}
