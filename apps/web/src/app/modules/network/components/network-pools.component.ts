import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxTreeListModule, DxButtonModule } from 'devextreme-angular';
import { forkJoin } from 'rxjs';
import {
  NetworkingEngineService,
  AsnPoolRow, AsnBlockRow,
  VlanPoolRow, VlanBlockRow,
  IpPoolRow,
} from '../../../core/services/networking-engine.service';

interface PoolNode {
  id: string;
  parentId: string | null;
  nodeType: 'AsnPool' | 'AsnBlock' | 'VlanPool' | 'VlanBlock' | 'IpPool' | 'Group';
  entityId: string | null;
  code: string;
  name: string;
  range: string;
  status: string;
}

/// Web counterpart to the WPF PoolsTreePanel (read-only). Flat tree
/// grouped by pool kind: virtual "ASN / VLAN / IP" root nodes
/// organise their pools + blocks so operators can scan the tenant's
/// numbering estate at a glance. Utilisation bars + allocation
/// actions stay WPF-only for this slice.
@Component({
  selector: 'app-network-pools',
  standalone: true,
  imports: [CommonModule, RouterModule, DxTreeListModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Pools</h2>
      <small class="subtitle">ASN / VLAN / IP numbering pools + blocks for this tenant. Read-only; allocate via the WPF client.</small>
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
      <dxi-column dataField="nodeType" caption="Type"  width="110"
                  cellTemplate="nodeTypeTemplate" />
      <dxi-column dataField="code"     caption="Code"  width="160" />
      <dxi-column dataField="name"     caption="Name"  width="240" />
      <dxi-column dataField="range"    caption="Range" width="200" />
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
    .nt-group     { color: #9ca3af; font-weight: 600; }
    .nt-asnpool   { color: #60a5fa; font-weight: 600; }
    .nt-asnblock  { color: #60a5fa; }
    .nt-vlanpool  { color: #34d399; font-weight: 600; }
    .nt-vlanblock { color: #34d399; }
    .nt-ippool    { color: #fbbf24; font-weight: 600; }
  `]
})
export class NetworkPoolsComponent implements OnInit {
  nodes: PoolNode[] = [];
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
      asnPools:   this.engine.listAsnPools(),
      asnBlocks:  this.engine.listAsnBlocks(),
      vlanPools:  this.engine.listVlanPools(),
      vlanBlocks: this.engine.listVlanBlocks(),
      ipPools:    this.engine.listIpPools(),
    }).subscribe({
      next: ({ asnPools, asnBlocks, vlanPools, vlanBlocks, ipPools }) => {
        this.nodes = this.buildNodes(asnPools, asnBlocks, vlanPools, vlanBlocks, ipPools);
        this.loading = false;
        const poolCount = asnPools.length + vlanPools.length + ipPools.length;
        const blockCount = asnBlocks.length + vlanBlocks.length;
        this.status = `${poolCount} pool${poolCount === 1 ? '' : 's'} · ` +
                      `${blockCount} block${blockCount === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.nodes = [];
      },
    });
  }

  private buildNodes(
    asnPools: AsnPoolRow[], asnBlocks: AsnBlockRow[],
    vlanPools: VlanPoolRow[], vlanBlocks: VlanBlockRow[],
    ipPools: IpPoolRow[],
  ): PoolNode[] {
    const out: PoolNode[] = [];
    // Three synthetic group roots so the three pool kinds each get
    // their own expandable branch. entityId=null on groups since
    // they have no net.* uuid to drill on.
    out.push({ id: 'Group:ASN',  parentId: null, nodeType: 'Group',
               entityId: null, code: 'ASN',  name: 'ASN pools',  range: '', status: '' });
    out.push({ id: 'Group:VLAN', parentId: null, nodeType: 'Group',
               entityId: null, code: 'VLAN', name: 'VLAN pools', range: '', status: '' });
    out.push({ id: 'Group:IP',   parentId: null, nodeType: 'Group',
               entityId: null, code: 'IP',   name: 'IP pools',   range: '', status: '' });

    for (const p of asnPools) {
      out.push({
        id: `AsnPool:${p.Id}`, parentId: 'Group:ASN',
        nodeType: 'AsnPool', entityId: p.Id,
        code: p.PoolCode, name: p.DisplayName,
        range: `${p.AsnFirst}-${p.AsnLast}`, status: p.Status,
      });
    }
    for (const b of asnBlocks) {
      out.push({
        id: `AsnBlock:${b.Id}`, parentId: `AsnPool:${b.PoolId}`,
        nodeType: 'AsnBlock', entityId: b.Id,
        code: b.BlockCode, name: b.DisplayName,
        range: `${b.AsnFirst}-${b.AsnLast}`, status: b.Status,
      });
    }
    for (const p of vlanPools) {
      out.push({
        id: `VlanPool:${p.Id}`, parentId: 'Group:VLAN',
        nodeType: 'VlanPool', entityId: p.Id,
        code: p.PoolCode, name: p.DisplayName,
        range: `${p.VlanFirst}-${p.VlanLast}`, status: p.Status,
      });
    }
    for (const b of vlanBlocks) {
      out.push({
        id: `VlanBlock:${b.Id}`, parentId: `VlanPool:${b.PoolId}`,
        nodeType: 'VlanBlock', entityId: b.Id,
        code: b.BlockCode, name: b.DisplayName,
        range: `${b.VlanStart}-${b.VlanEnd}`, status: b.Status,
      });
    }
    for (const p of ipPools) {
      out.push({
        id: `IpPool:${p.Id}`, parentId: 'Group:IP',
        nodeType: 'IpPool', entityId: p.Id,
        code: p.PoolCode, name: p.DisplayName,
        range: `${p.PoolCidr} (${p.AddressFamily})`, status: p.Status,
      });
    }
    return out;
  }

  /// Double-click drill to audit timeline. Group rows have no
  /// entity id → no-op. NodeType → engine entity_type mapping is
  /// explicit for MLAG-style divergences (MlagPool is server-side
  /// named MlagDomainPool); the rest map 1:1.
  onRowDoubleClick(e: { data: PoolNode }): void {
    const node = e?.data;
    if (!node || !node.entityId || node.nodeType === 'Group') return;
    this.router.navigate(['/network/audit', node.nodeType, node.entityId]);
  }
}
