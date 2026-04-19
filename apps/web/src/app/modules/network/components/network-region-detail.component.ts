import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, RegionRow, SiteRow,
} from '../../../core/services/networking-engine.service';

/// Region detail — top of the hierarchy drill chain (region →
/// site → building → devices/servers). Two tabs: Summary (row
/// fields) + Sites (every net.site under this region).
///
/// Routed at /network/region/:id. Uses Central.Api's listRegions
/// + listSites with the built-in regionId narrower; no engine work.
@Component({
  selector: 'app-network-region-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/hierarchy" class="back-link">← Hierarchy</a>
      <h2 *ngIf="region">{{ region.RegionCode }} · {{ region.DisplayName }}</h2>
      <h2 *ngIf="!region">Loading…</h2>
      <small *ngIf="region" class="subtitle">Region · {{ region.Status }}</small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && region" class="meta-grid">
      <div class="meta-row"><label>Region code</label>  <span>{{ region.RegionCode }}</span></div>
      <div class="meta-row"><label>Display name</label> <span>{{ region.DisplayName }}</span></div>
      <div class="meta-row"><label>Status</label>       <span>{{ region.Status }}</span></div>
      <div class="meta-row full"><label>UUID</label>    <code>{{ region.Id }}</code></div>
      <div class="meta-row" *ngIf="sitesLoaded">
        <label>Sites</label>
        <span>{{ sites.length }}</span>
      </div>
    </div>

    <!-- Sites tab -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="sites" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     (onRowDblClick)="onSiteDblClick($event)">
        <dxi-column dataField="SiteCode"    caption="Site code"    [fixed]="true" width="180"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="DisplayName" caption="Display name" width="260" />
        <dxi-column dataField="Status"      caption="Status"       width="90" />
        <dxi-column dataField="Id"          caption="UUID" />
      </dx-data-grid>
      <div *ngIf="sites.length === 0 && !loadingSites" class="empty-note">
        No sites under this region.
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
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .empty-note { margin-top: 12px; padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkRegionDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Sites' }];
  activeTab = 0;

  regionId = '';
  region: RegionRow | null = null;
  sites: SiteRow[] = [];
  loadingSites = false;
  sitesLoaded = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.regionId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.regionId) {
      this.status = 'Missing route param — expected /network/region/:id.';
      return;
    }
    this.status = 'Loading…';

    this.engine.listRegions().subscribe({
      next: (rows) => {
        this.region = rows.find(r => r.Id === this.regionId) ?? null;
        this.status = this.region ? '' : 'Region not found.';
        if (this.region) this.loadSites();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  /// listSites already accepts a regionId narrower — no client
  /// filter needed.
  private loadSites(): void {
    if (!this.region) return;
    this.loadingSites = true;
    this.engine.listSites(this.region.Id).subscribe({
      next: (rows) => {
        this.sites = rows;
        this.loadingSites = false;
        this.sitesLoaded = true;
      },
      error: () => { this.loadingSites = false; this.sitesLoaded = true; },
    });
  }

  onSiteDblClick(e: { data: SiteRow }): void {
    const row = e?.data;
    if (!row?.Id) return;
    this.router.navigate(['/network/site', row.Id]);
  }
}
