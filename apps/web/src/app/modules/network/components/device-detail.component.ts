import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DxToolbarModule, DxButtonModule, DxFormModule, DxLoadIndicatorModule } from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { NetworkService } from '../../../core/services/network.service';

/**
 * IPAM device detail page — mirrors the WPF Asset Details panel for a row
 * from `switch_guide`. Loads the device, exposes editable fields via
 * dx-form, and persists with Save.
 *
 * Form fields mirror the most-edited columns in the desktop grid; the
 * underlying API accepts arbitrary column names so adding more here is
 * just an editor row, not a backend change.
 */
@Component({
  selector: 'app-device-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, DxToolbarModule, DxButtonModule, DxFormModule, DxLoadIndicatorModule],
  template: `
    <dx-toolbar class="page-toolbar">
      <dxi-item location="before">
        <a routerLink="/network" class="back-link">← Network</a>
      </dxi-item>
      <dxi-item location="before">
        <div class="page-title">
          <strong>{{ device?.switch_name || 'Device' }}</strong>
          <span class="meta">{{ device?.building }} · {{ device?.device_type }}</span>
        </div>
      </dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ text: 'Save', type: 'success', icon: 'save', onClick: save, disabled: !dirty }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ text: 'Delete', type: 'danger', icon: 'trash', onClick: remove }"></dxi-item>
    </dx-toolbar>

    <div class="loader" *ngIf="loading">
      <dx-load-indicator height="32" width="32"></dx-load-indicator>
    </div>

    <dx-form *ngIf="device && !loading" [(formData)]="device" [colCount]="2"
             (onFieldDataChanged)="dirty = true">
      <dxi-item dataField="switch_name"   [label]="{ text: 'Device name' }" />
      <dxi-item dataField="device_type"   [label]="{ text: 'Type' }" />
      <dxi-item dataField="building"      [label]="{ text: 'Building' }" />
      <dxi-item dataField="region"        [label]="{ text: 'Region' }" />
      <dxi-item dataField="primary_ip"    [label]="{ text: 'Primary IP' }" />
      <dxi-item dataField="management_ip" [label]="{ text: 'Management IP' }" />
      <dxi-item dataField="status"        [label]="{ text: 'Status' }" />
      <dxi-item dataField="asn"           [label]="{ text: 'ASN' }" />
      <dxi-item dataField="serial_number" [label]="{ text: 'Serial number' }" />
      <dxi-item dataField="model"         [label]="{ text: 'Model' }" />
      <dxi-item dataField="rack"          [label]="{ text: 'Rack' }" />
      <dxi-item dataField="rack_unit"     [label]="{ text: 'Rack unit' }" />
      <dxi-item dataField="notes"         [colSpan]="2"
                [editorOptions]="{ height: 100 }" editorType="dxTextArea" [label]="{ text: 'Notes' }" />
    </dx-form>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .back-link { color: #60a5fa; text-decoration: none; margin-right: 12px; font-size: 13px; }
    .back-link:hover { text-decoration: underline; }
    .page-title { font-size: 16px; color: #f9fafb; }
    .page-title .meta { color: #9ca3af; font-size: 12px; margin-left: 12px; }
    .loader { display: flex; justify-content: center; padding: 32px; }
  `]
})
export class DeviceDetailComponent implements OnInit {
  device: any = null;
  loading = true;
  dirty = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private network: NetworkService,
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isFinite(id) || id <= 0) {
      notify('Invalid device ID', 'error', 3000);
      this.router.navigate(['/network']);
      return;
    }
    this.network.getDevice(id).subscribe({
      next: d => { this.device = d; this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load device', 'error', 3000); }
    });
  }

  save = (): void => {
    if (!this.device?.id) return;
    // Strip system fields the server controls.
    const { id, created_at, updated_at, is_deleted, ...body } = this.device;
    this.network.saveDevice(id, body).subscribe({
      next: () => { this.dirty = false; notify('Device saved', 'success', 1500); },
      error: () => notify('Save failed', 'error', 3000)
    });
  };

  remove = (): void => {
    if (!this.device?.id) return;
    if (!confirm(`Delete device "${this.device.switch_name}"?`)) return;
    this.network.deleteDevice(this.device.id).subscribe({
      next: () => { notify('Device deleted', 'success', 1500); this.router.navigate(['/network']); },
      error: () => notify('Delete failed', 'error', 3000)
    });
  };
}
