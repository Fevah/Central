import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxTreeListModule, DxButtonModule } from 'devextreme-angular';
import { forkJoin } from 'rxjs';
import {
  NetworkingEngineService,
  RegionRow, SiteRow, BuildingRow, FloorRow,
} from '../../../core/services/networking-engine.service';

/// Flat tree node shape — DxTreeList binds Id + ParentId to build
/// the hierarchy. Same pattern the WPF HierarchyTreePanel uses
/// (synthetic "{NodeType}:{guid}" ids avoid uuid collisions when
/// two different levels happen to share an underlying id).
interface HierarchyNode {
  id: string;
  parentId: string | null;
  nodeType: 'Region' | 'Site' | 'Building' | 'Floor';
  entityId: string;
  code: string;
  name: string;
  status: string;
}

/// Web counterpart to the WPF HierarchyTreePanel. Read-only for
/// this slice — four levels (Region → Site → Building → Floor).
/// Room + Rack land in a follow-up when operators need them on
/// the web. Double-click a Building → Device grid filtered to
/// that building (mirrors the WPF hierarchy → devices drill
/// shipped in 8bbc6bcec); clicking a row opens the audit timeline
/// for that entity.
@Component({
  selector: 'app-network-hierarchy',
  standalone: true,
  imports: [CommonModule, RouterModule, DxTreeListModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Hierarchy</h2>
      <small class="subtitle">Region → Site → Building → Floor tree. Read-only; edit via the WPF client.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-tree-list [dataSource]="nodes"
                  keyExpr="id" parentIdExpr="parentId"
                  [showBorders]="true" [columnAutoWidth]="true"
                  [searchPanel]="{ visible: true }"
                  [filterRow]="{ visible: true }"
                  [rootValue]="null"
                  [autoExpandAll]="true"
                  (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="nodeType" caption="Type"  width="100"
                  cellTemplate="nodeTypeTemplate" />
      <dxi-column dataField="code"     caption="Code"  width="160" />
      <dxi-column dataField="name"     caption="Name"  width="260" />
      <dxi-column dataField="status"   caption="Status" width="90" />

      <div *dxTemplate="let d of 'nodeTypeTemplate'">
        <span [class]="'nt-' + d.value.toLowerCase()">{{ d.value }}</span>
      </div>
    </dx-tree-list>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    .nt-region   { color: #60a5fa; font-weight: 600; }
    .nt-site     { color: #34d399; }
    .nt-building { color: #fbbf24; }
    .nt-floor    { color: #a78bfa; }
  `]
})
export class NetworkHierarchyComponent implements OnInit {
  nodes: HierarchyNode[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    forkJoin({
      regions:   this.engine.listRegions(),
      sites:     this.engine.listSites(),
      buildings: this.engine.listBuildings(),
      floors:    this.engine.listFloors(),
    }).subscribe({
      next: ({ regions, sites, buildings, floors }) => {
        this.nodes = this.buildNodes(regions, sites, buildings, floors);
        this.loading = false;
        this.status = `${this.nodes.length} node${this.nodes.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.nodes = [];
      },
    });
  }

  /// Flatten the 4 level lists into a single node array with
  /// Id/ParentId for DxTreeList — matches the WPF HierarchyTreePanel
  /// convention of synthetic composite keys.
  private buildNodes(
    regions: RegionRow[], sites: SiteRow[],
    buildings: BuildingRow[], floors: FloorRow[],
  ): HierarchyNode[] {
    const out: HierarchyNode[] = [];
    for (const r of regions) {
      out.push({
        id: `Region:${r.Id}`, parentId: null,
        nodeType: 'Region', entityId: r.Id,
        code: r.RegionCode, name: r.DisplayName, status: r.Status,
      });
    }
    for (const s of sites) {
      out.push({
        id: `Site:${s.Id}`, parentId: `Region:${s.RegionId}`,
        nodeType: 'Site', entityId: s.Id,
        code: s.SiteCode, name: s.DisplayName, status: s.Status,
      });
    }
    for (const b of buildings) {
      out.push({
        id: `Building:${b.Id}`, parentId: `Site:${b.SiteId}`,
        nodeType: 'Building', entityId: b.Id,
        code: b.BuildingCode, name: b.DisplayName, status: b.Status,
      });
    }
    for (const f of floors) {
      out.push({
        id: `Floor:${f.Id}`, parentId: `Building:${f.BuildingId}`,
        nodeType: 'Floor', entityId: f.Id,
        code: f.FloorCode, name: f.DisplayName ?? '', status: f.Status,
      });
    }
    return out;
  }

  /// Double-click drill: any node → audit timeline for that
  /// entity type + uuid. Parallel to the WPF "Show audit
  /// history" context-menu item on hierarchy nodes (984e95769).
  /// Region / Room audit rows may be thin until .NET-side
  /// audit emission lands (plan 10b gap).
  onRowDoubleClick(e: { data: HierarchyNode }): void {
    const node = e?.data;
    if (!node) return;
    this.router.navigate(['/network/audit', node.nodeType, node.entityId]);
  }
}
