import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DxDataGridModule, DxTextBoxModule, DxButtonModule, DxTagBoxModule } from 'devextreme-angular';
import { NetworkingEngineService, SearchResult } from '../../../core/services/networking-engine.service';
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
            DxButtonModule, DxTagBoxModule],
  template: `
    <div class="page-header">
      <h2>Search</h2>
      <small class="subtitle">Full-text across Device / Vlan / Subnet / Server / Link / DhcpRelayTarget.</small>
    </div>

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
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .page-header .subtitle { color: #888; }
    .search-bar { display: flex; gap: 8px; margin-bottom: 8px; align-items: center; }
    .search-bar .query { flex: 1; }
    .search-bar .entity-types { width: 260px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
  `]
})
export class NetworkSearchComponent {
  /// Entity-types the engine's UNION covers (`is_supported_entity_type`
  /// in search.rs). Keep in sync with the server-side list.
  availableEntityTypes = ['Device', 'Vlan', 'Subnet', 'Server', 'Link', 'DhcpRelayTarget'];

  query = '';
  selectedEntityTypes: string[] = [];
  results: SearchResult[] = [];
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

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
  }

  clear(): void {
    this.query = '';
    this.selectedEntityTypes = [];
    this.results = [];
    this.status = '';
  }

  /// Drill into the entity detail route. Only Device has a detail
  /// page in the web client today; everything else will eventually
  /// grow one. Unmapped entity types surface a status hint rather
  /// than a broken navigation.
  onRowDoubleClick(e: { data: SearchResult }): void {
    const row = e?.data;
    if (!row) return;
    const route = this.entityTypeToRoute(row.entityType);
    if (!route) {
      this.status = `No detail route for entity type '${row.entityType}'.`;
      return;
    }
    this.router.navigate([route, row.id]);
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
