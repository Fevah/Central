import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, SiteRow, BuildingRow,
} from '../../../core/services/networking-engine.service';

/// Site detail — parent of building detail (8fa342397). Two tabs:
/// Summary (row fields) + Buildings (every building where
/// SiteId matches). Double-click a building drills to the
/// building detail page.
///
/// Routed at /network/site/:id. Operators land here from the
/// hierarchy tree's Site nodes.
///
/// Uses Central.Api's listSites + listBuildings (PascalCase wire
/// shape) — filtered client-side by SiteId. No engine work.
@Component({
  selector: 'app-network-site-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/hierarchy" class="back-link">← Hierarchy</a>
      <h2 *ngIf="site">{{ site.SiteCode }} · {{ site.DisplayName }}</h2>
      <h2 *ngIf="!site">Loading…</h2>
      <small *ngIf="site" class="subtitle">Site · {{ site.Status }}</small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && site" class="meta-grid">
      <div class="meta-row"><label>Site code</label>    <span>{{ site.SiteCode }}</span></div>
      <div class="meta-row"><label>Display name</label> <span>{{ site.DisplayName }}</span></div>
      <div class="meta-row"><label>Region id</label>    <code>{{ site.RegionId }}</code></div>
      <div class="meta-row"><label>Status</label>       <span>{{ site.Status }}</span></div>
      <div class="meta-row full"><label>UUID</label>    <code>{{ site.Id }}</code></div>
      <div class="meta-row" *ngIf="buildingsLoaded">
        <label>Buildings</label>
        <span>{{ buildings.length }}</span>
      </div>
    </div>

    <!-- Buildings tab -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="buildings" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     (onRowDblClick)="onBuildingDblClick($event)">
        <dxi-column dataField="BuildingCode" caption="Building code" [fixed]="true" width="180"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="DisplayName"  caption="Display name"  width="260" />
        <dxi-column dataField="Status"       caption="Status"        width="90" />
        <dxi-column dataField="Id"           caption="UUID" />
      </dx-data-grid>
      <div *ngIf="buildings.length === 0 && !loadingBuildings" class="empty-note">
        No buildings under this site.
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
export class NetworkSiteDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Buildings' }];
  activeTab = 0;

  siteId = '';
  site: SiteRow | null = null;
  buildings: BuildingRow[] = [];
  loadingBuildings = false;
  buildingsLoaded = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.siteId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.siteId) {
      this.status = 'Missing route param — expected /network/site/:id.';
      return;
    }
    this.status = 'Loading…';

    this.engine.listSites().subscribe({
      next: (rows) => {
        this.site = rows.find(r => r.Id === this.siteId) ?? null;
        this.status = this.site ? '' : 'Site not found.';
        if (this.site) this.loadBuildings();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  /// Use the engine's built-in siteId narrower on listBuildings
  /// (no client filter needed). Eager-loaded so the Summary count
  /// is populated before the operator clicks the Buildings tab.
  private loadBuildings(): void {
    if (!this.site) return;
    this.loadingBuildings = true;
    this.engine.listBuildings(this.site.Id).subscribe({
      next: (rows) => {
        this.buildings = rows;
        this.loadingBuildings = false;
        this.buildingsLoaded = true;
      },
      error: () => { this.loadingBuildings = false; this.buildingsLoaded = true; },
    });
  }

  onBuildingDblClick(e: { data: BuildingRow }): void {
    const row = e?.data;
    if (!row?.Id) return;
    this.router.navigate(['/network/building', row.Id]);
  }
}
