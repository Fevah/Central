import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  DxDataGridModule, DxButtonModule, DxToolbarModule, DxPopupModule,
  DxFormModule, DxTextBoxModule, DxSelectBoxModule
} from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { confirm } from 'devextreme/ui/dialog';
import { AdminService, ApiKey } from '../../../core/services/admin.service';

/**
 * API Keys management — mirrors the desktop Admin → API Keys panel.
 *
 * Lists existing keys with name/role/created/last-used. "+ Generate" opens
 * a dialog; the server returns the **plaintext** key once on creation —
 * we surface it in a one-shot copy box because it's never retrievable again.
 *
 * Revoke = soft (sets is_revoked); Delete = hard. Both are confirmed.
 */
@Component({
  selector: 'app-api-keys-panel',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    DxDataGridModule, DxButtonModule, DxToolbarModule, DxPopupModule,
    DxFormModule, DxTextBoxModule, DxSelectBoxModule
  ],
  template: `
    <dx-toolbar style="margin-bottom: 8px;">
      <dxi-item location="before"><div class="title">API Keys</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ text: '+ Generate', type: 'default', onClick: openGenerate }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: load }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="keys" [showBorders]="true" [rowAlternationEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  height="calc(100vh - 320px)">
      <dxi-column dataField="id" caption="ID" width="60" />
      <dxi-column dataField="name" caption="Name" width="220" />
      <dxi-column dataField="role" caption="Role" width="120" />
      <dxi-column dataField="created_at" caption="Created" width="160" dataType="datetime" />
      <dxi-column dataField="last_used" caption="Last used" width="160" dataType="datetime" />
      <dxi-column dataField="is_revoked" caption="Revoked" width="90" dataType="boolean" />
      <dxi-column caption="Actions" width="200" cellTemplate="actionsTpl" />

      <div *dxTemplate="let d of 'actionsTpl'">
        <dx-button *ngIf="!d.data.is_revoked" text="Revoke" [stylingMode]="'outlined'" type="danger"
                   [width]="90" (onClick)="revoke(d.data)" />
        <dx-button text="Delete" [stylingMode]="'outlined'" [width]="90"
                   (onClick)="remove(d.data)" />
      </div>
    </dx-data-grid>

    <div class="empty-state" *ngIf="!loading && keys.length === 0">
      No API keys yet — use “+ Generate” to issue one.
    </div>

    <!-- Generate dialog -->
    <dx-popup [(visible)]="showGenerate" title="Generate API key"
              [width]="460" [height]="generated ? 380 : 280" [showCloseButton]="true">
      <ng-container *ngIf="!generated">
        <dx-form [(formData)]="form" labelLocation="top">
          <dxi-item dataField="name" [editorOptions]="{ placeholder: 'e.g. CI deployment key' }">
            <dxi-validation-rule type="required" />
          </dxi-item>
          <dxi-item dataField="role" editorType="dxSelectBox"
                    [editorOptions]="{ items: ['Admin','Operator','Viewer'], value: 'Operator' }" />
        </dx-form>
        <div class="dialog-actions">
          <dx-button text="Generate" type="success" (onClick)="submit()" />
        </div>
      </ng-container>

      <ng-container *ngIf="generated">
        <p class="warn">
          ⚠ Copy this key now — it will <strong>not</strong> be shown again.
        </p>
        <dx-text-box [value]="generated.key" [readOnly]="true"></dx-text-box>
        <div class="generated-meta">
          <div><span>Name:</span> {{ generated.name }}</div>
          <div><span>Role:</span> {{ generated.role }}</div>
        </div>
        <div class="dialog-actions">
          <dx-button text="Copy" icon="copy" (onClick)="copyKey()" />
          <dx-button text="Done" type="default" (onClick)="closeGenerate()" />
        </div>
      </ng-container>
    </dx-popup>
  `,
  styles: [`
    .title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .dialog-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
    .warn { color: #f59e0b; font-size: 13px; margin-bottom: 8px; }
    .generated-meta { margin: 12px 0; font-size: 13px; color: #9ca3af; }
    .generated-meta span { color: #6b7280; margin-right: 6px; }
    dx-button + dx-button { margin-left: 6px; }
  `]
})
export class ApiKeysPanelComponent implements OnInit {
  keys: ApiKey[] = [];
  loading = false;

  showGenerate = false;
  form: { name: string; role: string } = { name: '', role: 'Operator' };
  generated: { key: string; name: string; role: string } | null = null;

  constructor(private admin: AdminService) {}

  ngOnInit(): void { this.load(); }

  load = (): void => {
    this.loading = true;
    this.admin.getApiKeys().subscribe({
      next: k => { this.keys = k; this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load API keys', 'error', 3000); }
    });
  };

  openGenerate = (): void => {
    this.form = { name: '', role: 'Operator' };
    this.generated = null;
    this.showGenerate = true;
  };

  submit(): void {
    if (!this.form.name?.trim()) { notify('Name required', 'warning', 2000); return; }
    this.admin.generateApiKey(this.form.name, this.form.role).subscribe({
      next: g => { this.generated = g; this.load(); },
      error: () => notify('Failed to generate key', 'error', 3000)
    });
  }

  copyKey(): void {
    if (!this.generated?.key) return;
    navigator.clipboard?.writeText(this.generated.key).then(
      () => notify('Key copied', 'info', 1500),
      () => notify('Copy failed — select and copy manually', 'warning', 3000)
    );
  }

  closeGenerate(): void {
    this.showGenerate = false;
    this.generated = null;
  }

  revoke(k: ApiKey): void {
    confirm(`Revoke API key "${k.name}"? Existing usage will fail.`, 'Confirm').then(ok => {
      if (!ok) return;
      this.admin.revokeApiKey(k.id).subscribe({
        next: () => { notify('Key revoked', 'success', 1500); this.load(); },
        error: () => notify('Revoke failed', 'error', 3000)
      });
    });
  }

  remove(k: ApiKey): void {
    confirm(`Permanently delete API key "${k.name}"?`, 'Confirm').then(ok => {
      if (!ok) return;
      this.admin.deleteApiKey(k.id).subscribe({
        next: () => { notify('Key deleted', 'info', 1500); this.load(); },
        error: () => notify('Delete failed', 'error', 3000)
      });
    });
  }
}
