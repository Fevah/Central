import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  DxSelectBoxModule, DxNumberBoxModule, DxButtonModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, IpPoolRow, CarvePreview,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Read-only subnet carver preview. Operator picks an IP pool and a
/// target prefix length; the page calls POST
/// /api/net/allocate/subnet/preview and shows the CIDR the engine
/// WOULD carve next — without inserting a net.subnet row. Pairs with
/// the WPF Pools tree which is still the place actual allocations
/// get committed.
@Component({
  selector: 'app-network-carver-preview',
  standalone: true,
  imports: [CommonModule, FormsModule,
            DxSelectBoxModule, DxNumberBoxModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Subnet carver preview</h2>
      <small class="subtitle">
        Dry-run the subnet carver. Pick a pool + prefix length and
        the engine will show the CIDR it would allocate next —
        nothing is inserted. Use /network/pools to commit an
        allocation.
      </small>
    </div>

    <div class="picker">
      <label>IP pool</label>
      <dx-select-box
        [dataSource]="pools"
        displayExpr="DisplayName"
        valueExpr="Id"
        [(value)]="selectedPoolId"
        [searchEnabled]="true"
        [showClearButton]="true"
        placeholder="Select a pool…"
        width="360"
        (onValueChanged)="onPoolChanged()" />

      <label>Prefix length</label>
      <dx-number-box
        [(value)]="prefixLength"
        [min]="1"
        [max]="128"
        [step]="1"
        [showSpinButtons]="true"
        width="120" />

      <dx-button text="Preview" type="default"
                 [disabled]="!selectedPoolId || loading"
                 (onClick)="preview()" />
      <dx-button text="Clear" stylingMode="text"
                 [disabled]="loading"
                 (onClick)="clear()" />
    </div>

    <div *ngIf="status" class="status-line" [class.error]="error">
      {{ status }}
    </div>

    <div *ngIf="currentPool" class="pool-info">
      <span class="label">Pool CIDR:</span>
      <span class="mono">{{ currentPool.PoolCidr }}</span>
      <span class="family">{{ currentPool.AddressFamily }}</span>
    </div>

    <div *ngIf="result" class="result-card">
      <h3>Next available / {{ result.requestedPrefixLength }}</h3>
      <div class="result-cidr">{{ result.candidateCidr }}</div>
      <div class="result-meta">
        <div><span class="label">Pool:</span> <span class="mono">{{ result.poolCidr }}</span></div>
        <div><span class="label">Pool prefix:</span> /{{ result.poolPrefixLength }}</div>
        <div><span class="label">Requested prefix:</span> /{{ result.requestedPrefixLength }}</div>
        <div><span class="label">Family:</span> {{ result.isIpv6 ? 'IPv6' : 'IPv4' }}</div>
      </div>
      <p class="hint">
        This is a dry-run — no subnet row has been inserted. Switch
        to the Pools page (or the WPF Pools tree) to commit.
      </p>
    </div>
  `,
  styles: [`
    :host { display: block; padding: 12px 16px; }
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }

    .picker {
      display: grid;
      grid-template-columns: auto auto auto auto auto auto auto;
      gap: 12px; align-items: center; margin-bottom: 12px;
    }
    .picker label { color: #57606a; font-size: 13px; }

    .status-line { color: #6b6b6b; font-size: 12px; margin-bottom: 8px; }
    .status-line.error { color: #cf222e; font-weight: 600; }

    .pool-info {
      display: flex; gap: 10px; align-items: center;
      background: #f6f8fa; border: 1px solid #d0d7de;
      padding: 8px 12px; border-radius: 6px; margin-bottom: 12px;
    }
    .label { color: #57606a; font-size: 12px; }
    .mono { font-family: ui-monospace, Menlo, Consolas, monospace; }
    .family {
      margin-left: auto;
      padding: 2px 8px; border-radius: 10px;
      background: #ddf4ff; color: #0969da; font-size: 11px;
    }

    .result-card {
      background: #ffffff; border: 1px solid #d0d7de;
      border-left: 4px solid #1a7f37; border-radius: 6px;
      padding: 16px; max-width: 520px;
    }
    .result-card h3 { margin: 0 0 8px 0; color: #57606a; font-weight: 500; font-size: 13px; text-transform: uppercase; letter-spacing: 0.6px; }
    .result-cidr {
      font-family: ui-monospace, Menlo, Consolas, monospace;
      font-size: 28px; font-weight: 600;
      color: #1a7f37; margin-bottom: 14px;
    }
    .result-meta { display: grid; grid-template-columns: 1fr 1fr; gap: 4px 14px; font-size: 13px; margin-bottom: 10px; }
    .hint { color: #57606a; font-size: 12px; margin: 0; }
  `],
})
export class NetworkCarverPreviewComponent implements OnInit {
  pools: IpPoolRow[] = [];
  selectedPoolId: string | null = null;
  prefixLength = 24;

  loading = false;
  status = '';
  error = false;
  result: CarvePreview | null = null;

  constructor(private engine: NetworkingEngineService) {}

  get currentPool(): IpPoolRow | null {
    return this.pools.find(p => p.Id === this.selectedPoolId) ?? null;
  }

  ngOnInit(): void {
    this.engine.listIpPools().subscribe({
      next: (rows) => { this.pools = rows ?? []; },
      error: (err) => { this.status = `Pool load failed: ${err?.message ?? err}`; this.error = true; },
    });
  }

  onPoolChanged(): void {
    this.result = null;
    this.status = '';
    this.error = false;
    const p = this.currentPool;
    if (p?.AddressFamily === 'v6' && this.prefixLength < 48) {
      this.prefixLength = 64;
    } else if (p?.AddressFamily === 'v4' && this.prefixLength > 32) {
      this.prefixLength = 24;
    }
  }

  preview(): void {
    if (!this.selectedPoolId) return;
    this.loading = true;
    this.status = 'Previewing…';
    this.error = false;
    this.engine.previewSubnetCarve({
      poolId:         this.selectedPoolId,
      organizationId: environment.defaultTenantId,
      prefixLength:   this.prefixLength,
    }).subscribe({
      next: (r) => {
        this.result = r;
        this.status = `Next /${r.requestedPrefixLength} would be ${r.candidateCidr}.`;
        this.loading = false;
      },
      error: (err) => {
        this.result = null;
        this.status = err?.error?.message
          ?? err?.message
          ?? 'Preview failed.';
        this.error = true;
        this.loading = false;
      },
    });
  }

  clear(): void {
    this.result = null;
    this.status = '';
    this.error = false;
  }
}
