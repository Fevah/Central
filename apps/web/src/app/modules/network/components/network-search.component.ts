import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DxDataGridModule, DxTextBoxModule, DxButtonModule, DxTagBoxModule, DxListModule } from 'devextreme-angular';
import { NetworkingEngineService, SearchResult, SavedView, SearchFacet } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Angular counterpart to the WPF SearchPanel (Phase 10b). Hits
/// `/api/net/search` via the shared engine service, renders the
/// flat ranked result set in a DxDataGrid grouped by entity type.
/// Double-click drills to the entity detail route — same
/// find-then-focus flow the WPF side uses, minus the cross-panel
/// drill infrastructure (browser navigation covers it).
@Component({
  selector: 'app-network-search',
  standalone: true,
  imports: [CommonModule, FormsModule, DxDataGridModule, DxTextBoxModule,
            DxButtonModule, DxTagBoxModule, DxListModule],
  template: `
    <div class="page-header">
      <h2>Search</h2>
      <small class="subtitle">Full-text across Device / Vlan / Subnet / Server / Link / DhcpRelayTarget.</small>
    </div>

    <div class="search-layout">
      <!-- Saved views sidebar — mirrors the WPF SearchPanel. Clicking
           a view populates query + entity-types + auto-runs. -->
      <aside class="saved-views">
        <div class="saved-views-header">
          <strong>Saved views</strong>
          <dx-button icon="refresh" stylingMode="text"
                     hint="Reload saved views"
                     (onClick)="reloadSavedViews()" />
        </div>
        <div *ngIf="savedViewsError" class="saved-views-error">
          {{ savedViewsError }}
        </div>
        <dx-list [items]="savedViews"
                 [selectionMode]="'single'"
                 displayExpr="name"
                 (onItemClick)="onSavedViewClick($event)"
                 [noDataText]="'No saved views yet — create one from the WPF client.'">
          <div *dxTemplate="let v of 'item'">
            <div class="sv-name">{{ v.name }}</div>
            <div class="sv-sub">{{ summariseView(v) }}</div>
          </div>
        </dx-list>
      </aside>

      <main class="search-main">
        <div class="search-bar">
          <dx-text-box class="query" placeholder="Query — e.g. MEP-91 or 10.11.1.0/24"
                       [(value)]="query" (onEnterKey)="run()"
                       [showClearButton]="true" />
          <dx-tag-box class="entity-types" [items]="availableEntityTypes"
                       [(value)]="selectedEntityTypes" placeholder="(all types)"
                       [showClearButton]="true" />
          <dx-button text="Search" type="default" (onClick)="run()" />
          <dx-button text="Clear" (onClick)="clear()" />
        </div>

        <div *ngIf="status" class="status-line">{{ status }}</div>

        <!-- Facet bar — per-entity-type hit counts. Click a chip to
             narrow the search to that entity type and re-run. -->
        <div *ngIf="facets.length" class="facet-bar">
          <span class="facet-label">Narrow:</span>
          <button *ngFor="let f of facets"
                  class="facet-chip"
                  [class.active]="isFacetActive(f.entityType)"
                  [disabled]="f.count === 0"
                  (click)="toggleFacet(f.entityType)">
            {{ f.entityType }}
            <span class="facet-count">{{ f.count }}</span>
          </button>
        </div>

        <dx-data-grid [dataSource]="results" [showBorders]="true" [hoverStateEnabled]="true"
                       [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                       [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                       [groupPanel]="{ visible: true }"
                       (onRowDblClick)="onRowDoubleClick($event)">
          <dxi-column dataField="entityType" caption="Entity" [groupIndex]="0" width="140" />
          <dxi-column dataField="label" caption="Label" width="280" />
          <dxi-column dataField="snippet" caption="Snippet" />
          <dxi-column dataField="rank" caption="Rank" width="80" format="#0.0000" />
          <dxi-column dataField="id" caption="Id" width="260" />
        </dx-data-grid>
      </main>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .page-header .subtitle { color: #888; }
    .search-layout { display: grid; grid-template-columns: 240px 1fr; gap: 16px; }
    .saved-views { border: 1px solid #333; padding: 6px; }
    .saved-views-header { display: flex; justify-content: space-between; align-items: center; padding: 4px; }
    .saved-views-error { color: #888; padding: 4px; font-size: 11px; }
    .sv-name { font-weight: 600; }
    .sv-sub { color: #888; font-size: 11px; }
    .search-bar { display: flex; gap: 8px; margin-bottom: 8px; align-items: center; }
    .search-bar .query { flex: 1; }
    .search-bar .entity-types { width: 260px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .facet-bar { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin: 0 0 10px; }
    .facet-label { color: #888; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .facet-chip { background: rgba(59,130,246,0.1); color: #9ca3af; border: 1px solid rgba(59,130,246,0.2); border-radius: 12px; padding: 2px 10px; font-size: 12px; cursor: pointer; }
    .facet-chip:hover:not(:disabled) { background: rgba(59,130,246,0.2); color: #d1d5db; }
    .facet-chip.active { background: rgba(59,130,246,0.3); color: #60a5fa; border-color: rgba(59,130,246,0.5); }
    .facet-chip:disabled { opacity: 0.4; cursor: not-allowed; }
    .facet-count { color: #60a5fa; margin-left: 6px; font-weight: 600; }
  `]
})
export class NetworkSearchComponent implements OnInit {
  /// Entity-types the engine's UNION covers (`is_supported_entity_type`
  /// in search.rs). Keep in sync with the server-side list.
  availableEntityTypes = ['Device', 'Vlan', 'Subnet', 'Server', 'Link', 'DhcpRelayTarget'];

  query = '';
  selectedEntityTypes: string[] = [];
  results: SearchResult[] = [];
  facets: SearchFacet[] = [];
  status = '';

  savedViews: SavedView[] = [];
  savedViewsError = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.reloadSavedViews();
  }

  reloadSavedViews(): void {
    this.savedViewsError = '';
    this.engine.listSavedViews(environment.defaultTenantId).subscribe({
      next: (views) => { this.savedViews = views; },
      error: (err) => {
        // Soft-fail — the sidebar is a nice-to-have, search still
        // works when the endpoint is unreachable.
        this.savedViews = [];
        this.savedViewsError = err?.message ?? 'failed to load';
      },
    });
  }

  onSavedViewClick(e: { itemData?: SavedView }): void {
    const view = e?.itemData;
    if (!view) return;
    this.query = view.q ?? '';
    this.selectedEntityTypes = view.entityTypes
      ? view.entityTypes.split(',').map(s => s.trim()).filter(s => s.length > 0)
      : [];
    this.run();
  }

  /// Condensed one-liner under the view name. Matches the WPF
  /// SubtitleText layout: truncated query + entity-type summary.
  summariseView(v: SavedView): string {
    const q = (v.q ?? '').trim();
    const qShort = q.length === 0 ? '(empty)' : (q.length > 40 ? q.slice(0, 40) + '…' : q);
    const types = v.entityTypes && v.entityTypes.length > 0 ? v.entityTypes : 'all types';
    return `${qShort} · ${types}`;
  }

  run(): void {
    const q = this.query.trim();
    if (!q) {
      this.status = 'Empty query — type something + Search.';
      this.results = [];
      return;
    }

    this.status = 'Searching…';
    this.engine
      .search(
        environment.defaultTenantId,
        q,
        this.selectedEntityTypes.length ? this.selectedEntityTypes : undefined,
        50,
      )
      .subscribe({
        next: (rows) => {
          this.results = rows;
          this.status = `${rows.length} match${rows.length === 1 ? '' : 'es'}`;
        },
        error: (err) => {
          this.status = `Search failed: ${err.message ?? err}`;
          this.results = [];
        },
      });

    // Fire the facets query in parallel — independent from the main
    // ranked search so a slow facet query doesn't delay the grid.
    this.engine.searchFacets(environment.defaultTenantId, q).subscribe({
      next: (rows) => { this.facets = rows; },
      error: () => { this.facets = []; },
    });
  }

  /// True when the given entity type is currently filtering the search.
  isFacetActive(entityType: string): boolean {
    return this.selectedEntityTypes.includes(entityType);
  }

  /// Click-to-narrow. When inactive, replace the filter with just this
  /// type (explicit narrow). When active, clear the filter (back to
  /// "all types"). This matches most faceted-search UX conventions.
  toggleFacet(entityType: string): void {
    if (this.isFacetActive(entityType) && this.selectedEntityTypes.length === 1) {
      this.selectedEntityTypes = [];
    } else {
      this.selectedEntityTypes = [entityType];
    }
    this.run();
  }

  clear(): void {
    this.query = '';
    this.selectedEntityTypes = [];
    this.results = [];
    this.facets = [];
    this.status = '';
  }

  /// Drill into the entity detail route. Only Device has a detail
  /// page in the web client today; everything else falls through
  /// to the audit timeline so operators at least see what changed
  /// on the row.
  onRowDoubleClick(e: { data: SearchResult }): void {
    const row = e?.data;
    if (!row) return;
    const detailRoute = this.entityTypeToRoute(row.entityType);
    if (detailRoute) {
      this.router.navigate([detailRoute, row.id]);
      return;
    }
    // Fallback: audit timeline is always available for any entity
    // type the engine audits.
    this.router.navigate(['/network/audit', row.entityType, row.id]);
  }

  private entityTypeToRoute(entityType: string): string | null {
    // Map to web-client routes. When the network module grows VLAN
    // / Subnet / Server / Link detail views these entries land.
    switch (entityType) {
      case 'Device': return '/network/device';
      default:       return null;
    }
  }
}
