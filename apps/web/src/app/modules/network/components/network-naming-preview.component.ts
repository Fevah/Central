import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxSelectBoxModule, DxTextBoxModule, DxButtonModule, DxNumberBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService,
  LinkNamingContext, DeviceNamingContext, ServerNamingContext,
} from '../../../core/services/networking-engine.service';

/// Pure-token-substitution preview for naming templates. One page,
/// three modes (Link / Device / Server) — operators type a template
/// + fill in the context fields + see the rendered hostname before
/// committing the template to `net.{link_type,device_role,
/// server_profile}.naming_template`.
///
/// Mirrors the WPF Naming preview dialog. The engine endpoints are
/// stateless + tenant-agnostic (pure string expansion), so this page
/// is safe to load without an org context.
@Component({
  selector: 'app-network-naming-preview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxSelectBoxModule, DxTextBoxModule, DxButtonModule, DxNumberBoxModule],
  template: `
    <div class="page-header">
      <h2>Naming preview</h2>
      <small class="subtitle">Dry-run token substitution for link / device / server naming templates. Read-only; writes go through the template editor on each entity's profile row.</small>
    </div>

    <div class="mode-row">
      <label>Mode</label>
      <dx-select-box class="md" [items]="modes" [(value)]="mode"
                     (onValueChanged)="onModeChanged()" />
      <label>Template</label>
      <dx-text-box class="lg" [(value)]="template"
                   [placeholder]="templatePlaceholder()" />
      <dx-button text="Preview" type="default" (onClick)="run()"
                 [disabled]="loading || !template.trim()" />
    </div>

    <!-- Link context -->
    <div class="form-grid" *ngIf="mode === 'Link'">
      <div class="form-row"><label>Site A</label>     <dx-text-box [(value)]="link.siteA!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Site B</label>     <dx-text-box [(value)]="link.siteB!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Device A</label>   <dx-text-box [(value)]="link.deviceA!"   [showClearButton]="true" /></div>
      <div class="form-row"><label>Device B</label>   <dx-text-box [(value)]="link.deviceB!"   [showClearButton]="true" /></div>
      <div class="form-row"><label>Port A</label>     <dx-text-box [(value)]="link.portA!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Port B</label>     <dx-text-box [(value)]="link.portB!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Role A</label>     <dx-text-box [(value)]="link.roleA!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Role B</label>     <dx-text-box [(value)]="link.roleB!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>VLAN</label>       <dx-number-box [(value)]="link.vlanId!"  [showClearButton]="true" [showSpinButtons]="false" /></div>
      <div class="form-row"><label>Subnet</label>     <dx-text-box [(value)]="link.subnet!"    [showClearButton]="true" /></div>
      <div class="form-row"><label>Description</label><dx-text-box [(value)]="link.description!" [showClearButton]="true" /></div>
      <div class="form-row"><label>Link code</label>  <dx-text-box [(value)]="link.linkCode!"  [showClearButton]="true" /></div>
    </div>

    <!-- Device context -->
    <div class="form-grid" *ngIf="mode === 'Device'">
      <div class="form-row"><label>Region code</label>   <dx-text-box [(value)]="device.regionCode!"   [showClearButton]="true" /></div>
      <div class="form-row"><label>Site code</label>     <dx-text-box [(value)]="device.siteCode!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Building code</label> <dx-text-box [(value)]="device.buildingCode!" [showClearButton]="true" /></div>
      <div class="form-row"><label>Rack code</label>     <dx-text-box [(value)]="device.rackCode!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Role code</label>     <dx-text-box [(value)]="device.roleCode!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Instance</label>      <dx-number-box [(value)]="device.instance!"   [showClearButton]="true" [showSpinButtons]="false" /></div>
      <div class="form-row"><label>Instance padding</label><dx-number-box [(value)]="device.instancePadding!" [min]="1" [max]="5" [showSpinButtons]="true" /></div>
    </div>

    <!-- Server context -->
    <div class="form-grid" *ngIf="mode === 'Server'">
      <div class="form-row"><label>Region code</label>   <dx-text-box [(value)]="server.regionCode!"   [showClearButton]="true" /></div>
      <div class="form-row"><label>Site code</label>     <dx-text-box [(value)]="server.siteCode!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Building code</label> <dx-text-box [(value)]="server.buildingCode!" [showClearButton]="true" /></div>
      <div class="form-row"><label>Rack code</label>     <dx-text-box [(value)]="server.rackCode!"     [showClearButton]="true" /></div>
      <div class="form-row"><label>Profile code</label>  <dx-text-box [(value)]="server.profileCode!"  [showClearButton]="true" /></div>
      <div class="form-row"><label>Instance</label>      <dx-number-box [(value)]="server.instance!"   [showClearButton]="true" [showSpinButtons]="false" /></div>
      <div class="form-row"><label>Instance padding</label><dx-number-box [(value)]="server.instancePadding!" [min]="1" [max]="5" [showSpinButtons]="true" /></div>
    </div>

    <div class="result-block" *ngIf="expanded !== null">
      <div class="result-label">Expanded</div>
      <code class="result-value">{{ expanded || '(empty)' }}</code>
    </div>
    <div *ngIf="error" class="error-block">{{ error }}</div>

    <div class="hint-block">
      <strong>Available tokens</strong>
      <ul *ngIf="mode === 'Link'">
        <li><code>&#123;site_a&#125; &#123;site_b&#125; &#123;device_a&#125; &#123;device_b&#125; &#123;port_a&#125; &#123;port_b&#125; &#123;role_a&#125; &#123;role_b&#125;</code></li>
        <li><code>&#123;vlan&#125; &#123;subnet&#125; &#123;description&#125; &#123;link_code&#125;</code></li>
      </ul>
      <ul *ngIf="mode === 'Device'">
        <li><code>&#123;region_code&#125; &#123;site_code&#125; &#123;building_code&#125; &#123;rack_code&#125; &#123;role_code&#125; &#123;instance&#125;</code></li>
      </ul>
      <ul *ngIf="mode === 'Server'">
        <li><code>&#123;region_code&#125; &#123;site_code&#125; &#123;building_code&#125; &#123;rack_code&#125; &#123;profile_code&#125; &#123;instance&#125;</code></li>
      </ul>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .mode-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 16px; }
    .mode-row label { color: #888; font-size: 12px; }
    .mode-row .md { width: 140px; }
    .mode-row .lg { flex: 1; min-width: 300px; }
    .form-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; margin-bottom: 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .form-row { display: flex; flex-direction: column; gap: 2px; }
    .form-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .result-block { margin-top: 12px; padding: 14px; background: rgba(34,197,94,0.08); border: 1px solid rgba(34,197,94,0.3); border-radius: 6px; }
    .result-label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }
    .result-value { color: #22c55e; font-family: ui-monospace, monospace; font-size: 15px; font-weight: 600; word-break: break-all; }
    .error-block { margin-top: 12px; padding: 10px; color: #ef4444; background: rgba(239,68,68,0.08); border-radius: 4px; font-size: 12px; }
    .hint-block { margin-top: 20px; padding: 10px 14px; background: #0f172a; border-radius: 4px; font-size: 12px; color: #94a3b8; }
    .hint-block strong { display: block; color: #cbd5e1; margin-bottom: 4px; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .hint-block ul { margin: 0; padding-left: 16px; }
    .hint-block li { margin: 2px 0; }
    .hint-block code { background: rgba(148,163,184,0.1); padding: 1px 4px; border-radius: 2px; color: #e2e8f0; }
    @media (max-width: 900px) { .form-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkNamingPreviewComponent {
  modes = ['Link', 'Device', 'Server'];
  mode: 'Link' | 'Device' | 'Server' = 'Device';
  template = '{building_code}-{role_code}{instance}';

  link:   LinkNamingContext   = {};
  device: DeviceNamingContext = { instancePadding: 2 };
  server: ServerNamingContext = { instancePadding: 2 };

  loading = false;
  expanded: string | null = null;
  error = '';

  constructor(private engine: NetworkingEngineService) {}

  /// Reset the template to a sensible default per mode so operators
  /// don't have to remember token syntax the first time they switch.
  onModeChanged(): void {
    if (this.mode === 'Link')   this.template = '{device_a}__{port_a}__{device_b}__{port_b}';
    if (this.mode === 'Device') this.template = '{building_code}-{role_code}{instance}';
    if (this.mode === 'Server') this.template = '{building_code}-SRV{instance}';
    this.expanded = null;
    this.error = '';
  }

  templatePlaceholder(): string {
    if (this.mode === 'Link')   return 'e.g. {device_a}__{port_a}__{device_b}__{port_b}';
    if (this.mode === 'Device') return 'e.g. {building_code}-{role_code}{instance}';
    return 'e.g. {building_code}-SRV{instance}';
  }

  run(): void {
    this.loading = true;
    this.expanded = null;
    this.error = '';
    const obs =
      this.mode === 'Link'   ? this.engine.previewLinkName(this.template, this.link)
    : this.mode === 'Device' ? this.engine.previewDeviceName(this.template, this.device)
    :                          this.engine.previewServerName(this.template, this.server);

    obs.subscribe({
      next: (r) => {
        this.loading = false;
        this.expanded = r.expanded;
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.detail ?? err?.message ?? 'Preview failed.';
      },
    });
  }
}
