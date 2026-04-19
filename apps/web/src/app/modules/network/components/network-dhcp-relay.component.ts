import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, DhcpRelayTargetRow, VlanListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web grid over /api/net/dhcp-relay-targets. Displays every
/// (vlan × server_ip × priority) tuple in the tenant, with an
/// optional VLAN filter driven by the engine's thin VLAN list.
/// Grouping by vlanTag is the natural shape for operator review
/// since a single VLAN typically carries two relay targets (the
/// primary + the peer).
///
/// Read-only for this slice — writes stay on the WPF BulkPanel via
/// CSV + the edit-in-place API. A popup create form can land in a
/// follow-up once the hostname → vlanUuid resolution story is
/// settled (picker + auto-complete from the VLAN thin list).
@Component({
  selector: 'app-network-dhcp-relay',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>DHCP relay targets</h2>
      <small class="subtitle">net.dhcp_relay_target rows — M:N (vlan × server_ip) with priority ordering. Rendered into every device's <code>set system dhcp-relay server</code> stanza.</small>
    </div>

    <div class="filter-bar">
      <label>VLAN</label>
      <dx-select-box class="md" [items]="vlans" [(value)]="selectedVlanId"
                     valueExpr="id" displayExpr="displayLabel"
                     [showClearButton]="true"
                     placeholder="(all VLANs)"
                     (onValueChanged)="reload()" />

      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="vlanLabel"   caption="VLAN"     width="180" [groupIndex]="0" />
      <dxi-column dataField="serverIp"    caption="Server IP" width="160" [fixed]="true" />
      <dxi-column dataField="priority"    caption="Priority"  width="90"  dataType="number" sortOrder="asc" />
      <dxi-column dataField="status"      caption="Status"    width="90" />
      <dxi-column dataField="version"     caption="v"         width="50"  dataType="number" />
      <dxi-column dataField="notes"       caption="Notes" />
      <dxi-column dataField="createdAt"   caption="Created"   dataType="datetime" width="170"
                  format="yyyy-MM-dd HH:mm" />
      <dxi-column dataField="id"          caption="UUID"      width="260" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .subtitle code { background: #1e293b; padding: 1px 6px; border-radius: 3px; font-size: 11px; }
    .filter-bar { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .md { width: 260px; }
    .status-line { color: #666; font-size: 12px; }
  `]
})
export class NetworkDhcpRelayComponent implements OnInit {
  /// Decorated VLAN list for the filter combo — the thin list
  /// returns id + vlan_id + display_name; the combo needs a
  /// human-readable `displayLabel` pre-computed.
  vlans: Array<VlanListRow & { displayLabel: string }> = [];
  selectedVlanId: string | null = null;

  /// Rows decorated with a `vlanLabel` projection so the group panel
  /// shows "VLAN 120 (Servers)" rather than a raw uuid.
  rows: Array<DhcpRelayTargetRow & { vlanLabel: string }> = [];

  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    // Load VLANs first so the filter combo + row labels have a
    // resolved uuid → tag mapping before the row query completes.
    // Both reloads go through reload() once the VLANs are in.
    this.engine.listVlans(environment.defaultTenantId).subscribe({
      next: (vs) => {
        this.vlans = vs.map(v => ({
          ...v,
          displayLabel: `VLAN ${v.vlanId} · ${v.displayName}`,
        }));
        this.reload();
      },
      error: () => {
        this.vlans = [];
        this.reload();   // still attempt the row load — raw uuids is better than nothing
      },
    });
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine
      .listDhcpRelayTargets(environment.defaultTenantId, this.selectedVlanId ?? undefined)
      .subscribe({
        next: (rows) => {
          const vlanLookup = new Map<string, { vlanId: number; displayName: string }>();
          for (const v of this.vlans) {
            vlanLookup.set(v.id, { vlanId: v.vlanId, displayName: v.displayName });
          }
          this.rows = rows.map(r => {
            const v = vlanLookup.get(r.vlanId);
            return {
              ...r,
              vlanLabel: v ? `VLAN ${v.vlanId} · ${v.displayName}` : r.vlanId,
            };
          });
          this.loading = false;
          this.status = `${rows.length} relay target${rows.length === 1 ? '' : 's'}`;
        },
        error: (err) => {
          this.loading = false;
          this.status = err?.status === 403
            ? 'Forbidden — your user lacks read:DhcpRelayTarget.'
            : `Load failed: ${err?.message ?? err}`;
          this.rows = [];
        },
      });
  }

  onRowDoubleClick(e: { data: DhcpRelayTargetRow }): void {
    const r = e?.data;
    if (!r?.id) return;
    this.router.navigate(['/network/audit', 'DhcpRelayTarget', r.id]);
  }
}
