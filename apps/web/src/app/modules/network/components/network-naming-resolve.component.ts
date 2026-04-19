import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxButtonModule, DxSelectBoxModule, DxTextBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, NamingResolveResponse,
  RegionRow, SiteRow, BuildingRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// "Which naming template applies to entity X at building Y?"
/// answer page. Useful for debugging override precedence —
/// operators can see exactly which tier won without reading the
/// resolver source.
///
/// The sibling pages in the naming workflow are /network/naming-preview
/// (token expansion for an already-decided template) +
/// /network/naming-overrides (persist a template at a scope).
/// This page bridges them: pick entity type + hierarchy context,
/// see which scope's template won.
@Component({
  selector: 'app-network-naming-resolve',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxButtonModule, DxSelectBoxModule, DxTextBoxModule],
  template: `
    <div class="page-header">
      <h2>Naming resolve</h2>
      <small class="subtitle">Walk the override precedence (Building → Site → Region → Global → Default) to see which template applies. Siblings: <a routerLink="/network/naming-preview">naming preview</a> · <a routerLink="/network/naming-overrides">overrides CRUD</a>.</small>
    </div>

    <div class="form-grid">
      <div class="form-row">
        <label>Entity type *</label>
        <dx-select-box [items]="entityTypes" [(value)]="req.entityType" />
      </div>
      <div class="form-row">
        <label>Subtype code</label>
        <dx-text-box [(value)]="req.subtypeCode"
                     placeholder="role_code / type_code / profile_code" />
      </div>
      <div class="form-row">
        <label>Region</label>
        <dx-select-box [items]="regions" [(value)]="req.regionId"
                       valueExpr="Id" displayExpr="DisplayName"
                       [searchEnabled]="true" [showClearButton]="true" placeholder="(none)" />
      </div>
      <div class="form-row">
        <label>Site</label>
        <dx-select-box [items]="sites" [(value)]="req.siteId"
                       valueExpr="Id" displayExpr="DisplayName"
                       [searchEnabled]="true" [showClearButton]="true" placeholder="(none)" />
      </div>
      <div class="form-row">
        <label>Building</label>
        <dx-select-box [items]="buildings" [(value)]="req.buildingId"
                       valueExpr="Id" displayExpr="DisplayName"
                       [searchEnabled]="true" [showClearButton]="true" placeholder="(none)" />
      </div>
      <div class="form-row">
        <label>Default template (fallback)</label>
        <dx-text-box [(value)]="req.defaultTemplate"
                     placeholder="e.g. {building_code}-{role_code}{instance}" />
      </div>
    </div>

    <div class="actions">
      <dx-button text="Resolve" type="default" (onClick)="run()"
                 [disabled]="loading || !req.entityType" />
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <div *ngIf="result" class="result-block">
      <div class="row">
        <span class="label">Template</span>
        <code class="template">{{ result.template || '(empty)' }}</code>
      </div>
      <div class="row">
        <span class="label">Winning tier</span>
        <span [class]="'tier tier-' + result.source.toLowerCase()">{{ result.source }}</span>
      </div>
      <div class="row" *ngIf="result.overrideId">
        <span class="label">Override id</span>
        <code>{{ result.overrideId }}</code>
      </div>
      <div class="row" *ngIf="!result.overrideId">
        <span class="label">Override id</span>
        <span class="muted">— (using catalog / caller default)</span>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .subtitle a { color: #60a5fa; }
    .form-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px 16px; padding: 12px; background: #1e293b; border-radius: 6px; margin-bottom: 12px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .actions { display: flex; gap: 8px; margin-bottom: 10px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .result-block { padding: 14px; background: rgba(34,197,94,0.08); border: 1px solid rgba(34,197,94,0.3); border-radius: 6px; }
    .row { display: flex; align-items: center; gap: 12px; padding: 4px 0; }
    .label { color: #9ca3af; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; min-width: 120px; }
    .template { color: #22c55e; font-family: ui-monospace, monospace; font-size: 15px; font-weight: 600; }
    .muted { color: #6b7280; font-size: 12px; }
    .tier { padding: 2px 10px; border-radius: 10px; font-size: 12px; font-weight: 600; }
    .tier-default                { background: rgba(148,163,184,0.2); color: #cbd5e1; }
    .tier-globalanysubtype       { background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .tier-globalspecificsubtype  { background: rgba(59,130,246,0.3);  color: #3b82f6; }
    .tier-regionanysubtype       { background: rgba(168,85,247,0.2);  color: #a855f7; }
    .tier-regionspecificsubtype  { background: rgba(168,85,247,0.3);  color: #9333ea; }
    .tier-siteanysubtype         { background: rgba(234,179,8,0.2);   color: #eab308; }
    .tier-sitespecificsubtype    { background: rgba(234,179,8,0.3);   color: #ca8a04; }
    .tier-buildinganysubtype     { background: rgba(34,197,94,0.2);   color: #22c55e; }
    .tier-buildingspecificsubtype{ background: rgba(34,197,94,0.3);   color: #16a34a; }
    @media (max-width: 800px) { .form-grid { grid-template-columns: 1fr; } }
  `]
})
export class NetworkNamingResolveComponent implements OnInit {
  entityTypes = ['Device', 'Link', 'Server'];
  regions: RegionRow[] = [];
  sites: SiteRow[] = [];
  buildings: BuildingRow[] = [];

  req: {
    entityType: string;
    subtypeCode: string;
    regionId: string | null;
    siteId: string | null;
    buildingId: string | null;
    defaultTemplate: string;
  } = {
    entityType: 'Device',
    subtypeCode: '',
    regionId: null,
    siteId: null,
    buildingId: null,
    defaultTemplate: '{building_code}-{role_code}{instance}',
  };

  loading = false;
  status = '';
  result: NamingResolveResponse | null = null;

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void {
    // Prime the three hierarchy pickers in parallel — all tiny.
    this.engine.listRegions().subscribe({ next: (r) => { this.regions = r; }, error: () => {} });
    this.engine.listSites().subscribe({ next: (r) => { this.sites = r; }, error: () => {} });
    this.engine.listBuildings().subscribe({ next: (r) => { this.buildings = r; }, error: () => {} });
  }

  run(): void {
    this.loading = true;
    this.status = 'Resolving…';
    this.result = null;
    this.engine.resolveNamingTemplate({
      organizationId:  environment.defaultTenantId,
      entityType:      this.req.entityType,
      subtypeCode:     this.req.subtypeCode.trim() || null,
      regionId:        this.req.regionId,
      siteId:          this.req.siteId,
      buildingId:      this.req.buildingId,
      defaultTemplate: this.req.defaultTemplate.trim() || null,
    }).subscribe({
      next: (r) => {
        this.loading = false;
        this.result = r;
        this.status = '';
      },
      error: (err) => {
        this.loading = false;
        this.status = `Resolve failed: ${err?.error?.detail ?? err?.message ?? err}`;
      },
    });
  }
}
