import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DxDataGridModule, DxToolbarModule, DxTabPanelModule } from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { NetworkService, NetworkLink, LinkKind } from '../../../core/services/network.service';

/**
 * Unified Links page — three tabs (P2P, B2B, FW) backed by /api/links/*.
 *
 * Mirrors the three WPF panels in Central.Module.Links. We use a single
 * route + tabbed UI rather than separate routes because users typically
 * want to compare across link types.
 *
 * Edit-on-row + delete are wired via DxDataGrid built-in editing — the
 * server endpoints accept arbitrary column dictionaries so adding a column
 * here is a no-backend change.
 */
@Component({
  selector: 'app-link-grid',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxToolbarModule, DxTabPanelModule],
  template: `
    <dx-toolbar class="page-toolbar">
      <dxi-item location="before"><div class="page-title">Network Links</div></dxi-item>
    </dx-toolbar>

    <dx-tab-panel [items]="tabs" [selectedIndex]="0" [animationEnabled]="false"
                  (onSelectionChanged)="onTabChange($event)">
      <div *dxTemplate="let tab of 'item'">
        <dx-data-grid [dataSource]="rows" [showBorders]="true" [rowAlternationEnabled]="true"
                      [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                      [searchPanel]="{ visible: true }" [headerFilter]="{ visible: true }"
                      [editing]="{ mode: 'row', allowAdding: true, allowUpdating: true, allowDeleting: true, useIcons: true }"
                      (onRowInserting)="onInsert($event)"
                      (onRowUpdating)="onUpdate($event)"
                      (onRowRemoving)="onRemove($event)"
                      height="600">
          <dxi-column dataField="id" caption="ID" width="60" [allowEditing]="false" />
          <dxi-column dataField="description" caption="Description" />
          <dxi-column dataField="switch_a" caption="Switch A" width="160" />
          <dxi-column dataField="port_a"   caption="Port A"   width="120" />
          <dxi-column dataField="switch_b" caption="Switch B" width="160" />
          <dxi-column dataField="port_b"   caption="Port B"   width="120" />
          <dxi-column dataField="vlan"     caption="VLAN"     width="80" />
          <dxi-column dataField="link_type" caption="Type"    width="100" />
          <dxi-column dataField="status"    caption="Status"  width="100" />
        </dx-data-grid>

        <div class="empty-state" *ngIf="!loading && rows.length === 0">
          No {{ kind.toUpperCase() }} links yet — use the + button on the grid to add one.
        </div>
      </div>
    </dx-tab-panel>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .page-title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
  `]
})
export class LinkGridComponent implements OnInit {
  readonly tabs = [
    { title: 'P2P (Point-to-Point)', kind: 'p2p' as LinkKind },
    { title: 'B2B (Building-to-Building)', kind: 'b2b' as LinkKind },
    { title: 'FW (Firewall)', kind: 'fw' as LinkKind },
  ];
  kind: LinkKind = 'p2p';
  rows: NetworkLink[] = [];
  loading = false;

  constructor(private network: NetworkService) {}

  ngOnInit(): void { this.load(); }

  onTabChange = (e: any): void => {
    this.kind = (e.addedItems?.[0] as any)?.kind ?? 'p2p';
    this.load();
  };

  private load(): void {
    this.loading = true;
    this.network.getLinks(this.kind).subscribe({
      next: r => { this.rows = r; this.loading = false; },
      error: () => { this.loading = false; notify(`Failed to load ${this.kind} links`, 'error', 3000); }
    });
  }

  onInsert = (e: any): void => {
    // Block default UI re-render until the server confirms.
    e.cancel = (async () => {
      try {
        const result = await this.network.saveLink(this.kind, null, e.data).toPromise();
        e.data.id = result?.id;
        notify('Link added', 'success', 1500);
      } catch {
        notify('Failed to add link', 'error', 3000);
        return true;  // cancel the row
      }
      return false;
    })();
  };

  onUpdate = (e: any): void => {
    const merged = { ...e.oldData, ...e.newData };
    e.cancel = (async () => {
      try {
        await this.network.saveLink(this.kind, merged.id, e.newData).toPromise();
        notify('Link updated', 'success', 1500);
      } catch {
        notify('Failed to update link', 'error', 3000);
        return true;
      }
      return false;
    })();
  };

  onRemove = (e: any): void => {
    e.cancel = (async () => {
      try {
        await this.network.deleteLink(this.kind, e.data.id).toPromise();
        notify('Link deleted', 'info', 1500);
      } catch {
        notify('Failed to delete link', 'error', 3000);
        return true;
      }
      return false;
    })();
  };
}
