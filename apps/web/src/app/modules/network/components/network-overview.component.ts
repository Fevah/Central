import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, AuditRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

interface CountTile {
  label: string;
  count: number;
  route: string;
  kind: 'primary' | 'secondary' | 'numbering' | 'hierarchy' | 'governance';
  loading: boolean;
}

interface StatusBreakdown {
  label: string;
  active: number;
  decommissioned: number;
  planned: number;
  locked: number;
  other: number;
}

/// Single-glance tenant overview. Groups every entity count into five
/// tile rows (primary / secondary / numbering / hierarchy / governance),
/// pulls the latest validation run summary, and surfaces the top five
/// rules by violation count. Each tile double-clicks through to its
/// grid page; validation badge drills to /network/validation.
@Component({
  selector: 'app-network-overview',
  standalone: true,
  imports: [CommonModule, RouterModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Network overview</h2>
      <small class="subtitle">
        Tenant-wide entity counts + latest validation summary. Click a
        tile to drill into the grid; click the validation badge to
        re-run the full catalog.
      </small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <dx-button text="Run validation" type="default"
                 (onClick)="runValidation()" [disabled]="validationLoading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <!-- Quick-access bar surfacing the self-serve + audit workflows
         that don't have a natural home in the entity tile rows
         below. Each chip drills to its page; keeps /network/overview
         the single landing surface without forcing operators to
         memorise /network/my-activity and friends. -->
    <div class="quicklinks">
      <a class="chip" routerLink="/network/my-activity">
        <span class="chip-icon">🕑</span>
        <span>My activity</span>
      </a>
      <a class="chip" routerLink="/network/my-grants">
        <span class="chip-icon">🔑</span>
        <span>My grants</span>
      </a>
      <a class="chip" routerLink="/network/my-changesets">
        <span class="chip-icon">📋</span>
        <span>My change sets</span>
      </a>
      <a class="chip" routerLink="/network/correlations">
        <span class="chip-icon">🔗</span>
        <span>Correlations</span>
      </a>
      <a class="chip" routerLink="/network/audit-search">
        <span class="chip-icon">🔍</span>
        <span>Audit search</span>
      </a>
      <a class="chip" routerLink="/network/data-quality">
        <span class="chip-icon">✓</span>
        <span>Data quality</span>
      </a>
      <a class="chip" routerLink="/network/carver-preview">
        <span class="chip-icon">✂</span>
        <span>Carver preview</span>
      </a>
    </div>

    <ng-container *ngFor="let section of sections">
      <h3 class="section-header">{{ section.label }}</h3>
      <div class="tile-row">
        <a class="tile" [class.loading]="t.loading"
           [ngClass]="'tile-' + t.kind"
           [routerLink]="t.route"
           *ngFor="let t of section.tiles">
          <div class="tile-label">{{ t.label }}</div>
          <div class="tile-count">{{ t.loading ? '…' : t.count }}</div>
        </a>
      </div>
    </ng-container>

    <h3 class="section-header">Validation</h3>
    <div class="validation-row">
      <a class="validation-card" routerLink="/network/validation">
        <div class="vc-label">Rules run</div>
        <div class="vc-count">{{ validationLoading ? '…' : rulesRun }}</div>
      </a>
      <a class="validation-card" routerLink="/network/validation">
        <div class="vc-label">Rules with findings</div>
        <div class="vc-count" [class.warn]="rulesWithFindings > 0">
          {{ validationLoading ? '…' : rulesWithFindings }}
        </div>
      </a>
      <a class="validation-card" routerLink="/network/validation">
        <div class="vc-label">Total violations</div>
        <div class="vc-count" [class.warn]="totalViolations > 0">
          {{ validationLoading ? '…' : totalViolations }}
        </div>
      </a>
      <a class="validation-card" routerLink="/network/validation">
        <div class="vc-label">Errors</div>
        <div class="vc-count" [class.error]="errorCount > 0">
          {{ validationLoading ? '…' : errorCount }}
        </div>
      </a>
      <a class="validation-card" routerLink="/network/validation">
        <div class="vc-label">Warnings</div>
        <div class="vc-count" [class.warn]="warningCount > 0">
          {{ validationLoading ? '…' : warningCount }}
        </div>
      </a>
    </div>

    <h3 class="section-header" *ngIf="topRules.length">Top violating rules</h3>
    <table class="top-rules" *ngIf="topRules.length">
      <thead>
        <tr>
          <th>Rule</th><th>Severity</th><th class="num">Violations</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let r of topRules">
          <td>{{ r.ruleCode }}</td>
          <td [ngClass]="'sev-' + r.severity.toLowerCase()">{{ r.severity }}</td>
          <td class="num">{{ r.count }}</td>
        </tr>
      </tbody>
    </table>

    <h3 class="section-header">
      Recent activity
      <a routerLink="/network/audit-search" class="section-link">see all →</a>
    </h3>
    <div *ngIf="recentLoading" class="empty-note">Loading recent activity…</div>
    <div *ngIf="!recentLoading && !recentActivity.length" class="empty-note">
      No recent audit entries.
    </div>
    <table class="recent" *ngIf="recentActivity.length">
      <thead>
        <tr>
          <th>At</th><th>Actor</th><th>Entity</th><th>Action</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let r of recentActivity"
            class="recent-row"
            (click)="openRecent(r)">
          <td>{{ r.createdAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
          <td>{{ r.actorDisplay ?? (r.actorUserId ?? '(service)') }}</td>
          <td>{{ r.entityType }}</td>
          <td><code>{{ r.action }}</code></td>
        </tr>
      </tbody>
    </table>
  `,
  styles: [`
    :host { display: block; padding: 12px 16px; }
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 16px; }
    .status-line { color: #666; font-size: 12px; }

    .quicklinks {
      display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 16px;
    }
    .chip {
      display: inline-flex; gap: 6px; align-items: center;
      padding: 6px 12px; border-radius: 16px; text-decoration: none;
      background: #f6f8fa; border: 1px solid #d0d7de; color: #24292f;
      font-size: 12px; transition: background 0.1s;
    }
    .chip:hover { background: #eaeef2; }
    .chip-icon { font-size: 13px; }

    .section-header {
      margin: 20px 0 8px 0; font-size: 13px; text-transform: uppercase;
      letter-spacing: 0.6px; color: #6b6b6b;
    }

    .tile-row {
      display: grid; gap: 10px;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      margin-bottom: 4px;
    }
    .tile {
      display: block; padding: 14px 16px; border-radius: 6px;
      background: #ffffff; border: 1px solid #e1e4e8;
      text-decoration: none; color: inherit; transition: transform 0.1s, box-shadow 0.1s;
    }
    .tile:hover {
      transform: translateY(-1px);
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    }
    .tile.loading { opacity: 0.6; }
    .tile-label { font-size: 12px; color: #57606a; margin-bottom: 6px; }
    .tile-count { font-size: 24px; font-weight: 600; color: #24292f; }
    .tile-primary    { border-left: 3px solid #0969da; }
    .tile-secondary  { border-left: 3px solid #8250df; }
    .tile-numbering  { border-left: 3px solid #1a7f37; }
    .tile-hierarchy  { border-left: 3px solid #bf8700; }
    .tile-governance { border-left: 3px solid #cf222e; }

    .validation-row {
      display: grid; gap: 10px; grid-template-columns: repeat(5, 1fr);
    }
    .validation-card {
      display: block; padding: 16px; background: #f6f8fa; border-radius: 6px;
      border: 1px solid #d0d7de; text-decoration: none; color: inherit;
    }
    .validation-card:hover { background: #eaeef2; }
    .vc-label { font-size: 12px; color: #57606a; margin-bottom: 6px; }
    .vc-count { font-size: 22px; font-weight: 600; color: #24292f; }
    .vc-count.warn  { color: #bf8700; }
    .vc-count.error { color: #cf222e; }

    .top-rules {
      width: 100%; border-collapse: collapse; margin-top: 8px;
      background: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px;
    }
    .top-rules th, .top-rules td {
      text-align: left; padding: 8px 12px; border-bottom: 1px solid #e1e4e8;
    }
    .top-rules th.num, .top-rules td.num { text-align: right; }
    .top-rules tr:last-child td { border-bottom: none; }
    .sev-error   { color: #cf222e; font-weight: 600; }
    .sev-warning { color: #bf8700; }
    .sev-info    { color: #6b6b6b; }

    .section-link {
      float: right; font-size: 11px; font-weight: normal;
      color: #3b82f6; text-decoration: none; text-transform: none;
      letter-spacing: normal;
    }
    .section-link:hover { text-decoration: underline; }
    .empty-note {
      padding: 12px; color: #888; font-size: 12px; font-style: italic;
      text-align: center;
      background: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px;
    }
    .recent {
      width: 100%; border-collapse: collapse; margin-top: 8px;
      background: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px;
    }
    .recent th, .recent td {
      text-align: left; padding: 6px 12px; border-bottom: 1px solid #e1e4e8;
      font-size: 12px;
    }
    .recent tr:last-child td { border-bottom: none; }
    .recent-row { cursor: pointer; }
    .recent-row:hover { background: #f6f8fa; }
    .recent code { font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 11px;
                   background: rgba(148,163,184,0.1); padding: 1px 6px; border-radius: 3px; }
  `],
})
export class NetworkOverviewComponent implements OnInit {
  sections: { label: string; tiles: CountTile[] }[] = [];
  loading = false;
  status = '';

  validationLoading = false;
  rulesRun = 0;
  rulesWithFindings = 0;
  totalViolations = 0;
  errorCount = 0;
  warningCount = 0;
  topRules: { ruleCode: string; severity: string; count: number }[] = [];

  recentActivity: AuditRow[] = [];
  recentLoading = false;

  private tenantId = environment.defaultTenantId;

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {
    this.buildSections();
  }

  ngOnInit(): void {
    this.reload();
    this.runValidation();
    this.loadRecentActivity();
  }

  /// Last 10 audit entries across the tenant. Tiny widget —
  /// click a row to drill into the entity's full timeline.
  private loadRecentActivity(): void {
    this.recentLoading = true;
    this.engine.listAudit(this.tenantId, { limit: 10 }).subscribe({
      next: (rows) => {
        this.recentActivity = rows ?? [];
        this.recentLoading = false;
      },
      error: () => { this.recentLoading = false; },
    });
  }

  /// Double-click a recent activity row → drill to the entity's
  /// audit timeline (falls back silently when entityId is null,
  /// e.g. tenant-scope audit entries).
  openRecent(r: AuditRow): void {
    if (!r?.entityType || !r?.entityId) return;
    this.router.navigate(['/network/audit', r.entityType, r.entityId]);
  }

  private buildSections(): void {
    const mk = (label: string, route: string,
                kind: CountTile['kind']): CountTile =>
      ({ label, count: 0, route, kind, loading: true });

    this.sections = [
      {
        label: 'Primary entities',
        tiles: [
          mk('Devices',      '/network/devices',      'primary'),
          mk('Servers',      '/network/servers',      'primary'),
          mk('VLANs',        '/network/vlans',        'primary'),
          mk('Links',        '/network/links-grid',   'primary'),
          mk('Subnets',      '/network/subnets',      'primary'),
          mk('IP addresses', '/network/ip-addresses', 'primary'),
        ],
      },
      {
        label: 'Secondary',
        tiles: [
          mk('Ports',               '/network/ports',               'secondary'),
          mk('Aggregate-ethernet',  '/network/aggregate-ethernet',  'secondary'),
          mk('Modules',             '/network/modules',             'secondary'),
          mk('DHCP relay targets',  '/network/dhcp-relay',          'secondary'),
        ],
      },
      {
        label: 'Numbering',
        tiles: [
          mk('ASN allocations',      '/network/asn-allocations',    'numbering'),
          mk('VLAN blocks',          '/network/vlan-blocks',        'numbering'),
          mk('ASN blocks',           '/network/asn-blocks',         'numbering'),
          mk('MLAG domains',         '/network/mlag-domains',       'numbering'),
          mk('MSTP rules',           '/network/mstp-rules',         'numbering'),
          mk('Reservation shelf',    '/network/reservation-shelf',  'numbering'),
        ],
      },
      {
        label: 'Hierarchy',
        tiles: [
          mk('Rooms',         '/network/rooms',         'hierarchy'),
          mk('Racks',         '/network/racks',         'hierarchy'),
        ],
      },
      {
        label: 'Governance',
        tiles: [
          mk('Change sets',   '/network/change-sets',   'governance'),
          mk('Scope grants',  '/network/scope-grants',  'governance'),
          mk('Locks',         '/network/locks',         'governance'),
        ],
      },
    ];
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading counts…';

    // Single compound call — replaces the 21-parallel listX pattern
    // with one SQL statement on the engine side. Tiles that map to
    // entity types not covered by the summary endpoint (Locks,
    // VLAN blocks, ASN blocks) fall back to their own call after.
    this.engine.tenantSummary(this.tenantId).subscribe({
      next: (s) => {
        const setCount = (label: string, count: number) => {
          for (const section of this.sections) {
            const tile = section.tiles.find(x => x.label === label);
            if (tile) { tile.count = count; tile.loading = false; return; }
          }
        };

        setCount('Devices',             s.devices);
        setCount('Servers',             s.servers);
        setCount('VLANs',               s.vlans);
        setCount('Links',               s.links);
        setCount('Subnets',             s.subnets);
        setCount('IP addresses',        s.ipAddresses);
        setCount('Ports',               s.ports);
        setCount('Aggregate-ethernet',  s.aggregateEthernet);
        setCount('Modules',             s.modules);
        setCount('DHCP relay targets',  s.dhcpRelayTargets);
        setCount('ASN allocations',     s.asnAllocations);
        setCount('MLAG domains',        s.mlagDomains);
        setCount('MSTP rules',          s.mstpRules);
        setCount('Reservation shelf',   s.reservationShelf);
        setCount('Rooms',               s.rooms);
        setCount('Racks',               s.racks);
        setCount('Change sets',         s.changeSets);
        setCount('Scope grants',        s.scopeGrants);

        this.loading = false;
        this.status = 'Counts loaded.';
        this.loadExtraCounts();
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        for (const s of this.sections) {
          for (const t of s.tiles) { t.loading = false; }
        }
      },
    });
  }

  /// Three tiles — VLAN blocks, ASN blocks, Locks — aren't in the
  /// tenant-summary payload because they compute per-block
  /// availability or span non-net.* tables. Fetch separately so
  /// the main summary call is still one round-trip.
  private loadExtraCounts(): void {
    const t = this.tenantId;
    const setCount = (label: string, count: number) => {
      for (const section of this.sections) {
        const tile = section.tiles.find(x => x.label === label);
        if (tile) { tile.count = count; tile.loading = false; return; }
      }
    };
    this.engine.listVlanBlockAvailability(t).subscribe({
      next: (rows) => setCount('VLAN blocks', rows?.length ?? 0),
      error: () => setCount('VLAN blocks', 0),
    });
    this.engine.listAsnBlockAvailability(t).subscribe({
      next: (rows) => setCount('ASN blocks', rows?.length ?? 0),
      error: () => setCount('ASN blocks', 0),
    });
    this.engine.listLockedRows(t).subscribe({
      next: (rows) => setCount('Locks', rows?.length ?? 0),
      error: () => setCount('Locks', 0),
    });
  }

  runValidation(): void {
    this.validationLoading = true;
    this.engine.runValidation(this.tenantId).subscribe({
      next: (res) => {
        this.rulesRun          = res.rulesRun;
        this.rulesWithFindings = res.rulesWithFindings;
        this.totalViolations   = res.totalViolations;
        this.errorCount   = res.violations.filter(v => v.severity === 'Error').length;
        this.warningCount = res.violations.filter(v => v.severity === 'Warning').length;

        const byRule = new Map<string, { ruleCode: string; severity: string; count: number }>();
        for (const v of res.violations) {
          const existing = byRule.get(v.ruleCode);
          if (existing) { existing.count += 1; }
          else { byRule.set(v.ruleCode,
            { ruleCode: v.ruleCode, severity: v.severity, count: 1 }); }
        }
        this.topRules = [...byRule.values()]
          .sort((a, b) => b.count - a.count)
          .slice(0, 5);

        this.validationLoading = false;
      },
      error: (err) => {
        this.validationLoading = false;
        this.status = `Validation failed: ${err?.message ?? err}`;
      },
    });
  }
}
