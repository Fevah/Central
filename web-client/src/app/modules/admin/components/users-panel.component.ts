import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  DxDataGridModule, DxButtonModule, DxToolbarModule, DxPopupModule,
  DxFormModule, DxTextBoxModule, DxSelectBoxModule, DxCheckBoxModule
} from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { confirm } from 'devextreme/ui/dialog';
import { AdminService, TenantUser } from '../../../core/services/admin.service';

/**
 * Tenant Users management — mirrors the WPF Admin → Users panel.
 *
 * Lists app_users with inline editing for username/display/role/email/active,
 * delete with confirmation, and a separate password-reset action that opens
 * a popup (passwords are write-only — no inline edit).
 *
 * The password-reset endpoint resets the **legacy WPF login** password.
 * Web/mobile users authenticate via the Rust auth-service which has its
 * own forgot-password flow — keep them in sync if the same person uses both.
 */
@Component({
  selector: 'app-users-panel',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    DxDataGridModule, DxButtonModule, DxToolbarModule, DxPopupModule,
    DxFormModule, DxTextBoxModule, DxSelectBoxModule, DxCheckBoxModule
  ],
  template: `
    <dx-toolbar style="margin-bottom: 8px;">
      <dxi-item location="before"><div class="title">Tenant Users</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ text: '+ New user', type: 'default', onClick: openNew }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="users" [showBorders]="true" [rowAlternationEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  [searchPanel]="{ visible: true }"
                  [editing]="{ mode: 'row', allowUpdating: true, allowDeleting: false, useIcons: true }"
                  (onRowUpdating)="onRowUpdate($event)"
                  height="calc(100vh - 320px)">
      <dxi-column dataField="id" caption="ID" width="60" [allowEditing]="false" />
      <dxi-column dataField="username" caption="Username" width="160" />
      <dxi-column dataField="display_name" caption="Name" width="200" />
      <dxi-column dataField="email" caption="Email" width="220" />
      <dxi-column dataField="role" caption="Role" width="120" editorType="dxSelectBox"
                  [editorOptions]="{ items: ['Admin','Operator','Viewer'] }" />
      <dxi-column dataField="is_active" caption="Active" width="80" dataType="boolean" />
      <dxi-column dataField="last_login" caption="Last login" dataType="datetime" width="160" [allowEditing]="false" />
      <dxi-column caption="Actions" width="220" cellTemplate="actionsTpl" [allowEditing]="false" />

      <div *dxTemplate="let d of 'actionsTpl'">
        <dx-button text="Reset password" [stylingMode]="'outlined'" [width]="130"
                   (onClick)="openReset(d.data)" />
        <dx-button icon="trash" hint="Delete" [stylingMode]="'text'" type="danger"
                   (onClick)="remove(d.data)" />
      </div>
    </dx-data-grid>

    <div class="empty-state" *ngIf="!loading && users.length === 0">
      No users yet — use “+ New user” to add one.
    </div>

    <!-- New user dialog -->
    <dx-popup [(visible)]="showNew" title="New user" [width]="460" [height]="380" [showCloseButton]="true">
      <dx-form [(formData)]="newUser" labelLocation="top">
        <dxi-item dataField="username">
          <dxi-validation-rule type="required" />
        </dxi-item>
        <dxi-item dataField="display_name" />
        <dxi-item dataField="email" [editorOptions]="{ placeholder: 'user@company.com' }">
          <dxi-validation-rule type="email" />
        </dxi-item>
        <dxi-item dataField="role" editorType="dxSelectBox"
                  [editorOptions]="{ items: ['Admin','Operator','Viewer'], value: 'Viewer' }" />
        <dxi-item dataField="is_active" editorType="dxCheckBox"
                  [editorOptions]="{ value: true, text: 'Active' }" />
      </dx-form>
      <div class="dialog-actions">
        <dx-button text="Cancel" (onClick)="showNew = false" />
        <dx-button text="Create" type="success" (onClick)="submitNew()" />
      </div>
    </dx-popup>

    <!-- Password reset dialog -->
    <dx-popup [(visible)]="showReset" title="Reset password"
              [width]="420" [height]="280" [showCloseButton]="true">
      <p class="muted" *ngIf="resetUser">
        User: <strong>{{ resetUser.username }}</strong>
      </p>
      <dx-text-box [(value)]="resetPwd" mode="password"
                   placeholder="New password (8+ chars)"></dx-text-box>
      <p class="hint">
        This resets the legacy desktop login password. Web/mobile users use the
        auth-service forgot-password flow.
      </p>
      <div class="dialog-actions">
        <dx-button text="Cancel" (onClick)="showReset = false" />
        <dx-button text="Reset" type="success" (onClick)="submitReset()" />
      </div>
    </dx-popup>
  `,
  styles: [`
    .title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .dialog-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
    .muted { color: #9ca3af; font-size: 13px; margin-bottom: 8px; }
    .hint { color: #6b7280; font-size: 12px; margin-top: 8px; }
    dx-button + dx-button { margin-left: 6px; }
  `]
})
export class UsersPanelComponent implements OnInit {
  users: TenantUser[] = [];
  loading = false;

  showNew = false;
  newUser: Partial<TenantUser> = {};

  showReset = false;
  resetUser: TenantUser | null = null;
  resetPwd = '';

  constructor(private admin: AdminService) {}

  ngOnInit(): void { this.refresh(); }

  refresh = (): void => {
    this.loading = true;
    this.admin.getTenantUsers().subscribe({
      next: u => { this.users = u; this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load users', 'error', 3000); }
    });
  };

  openNew = (): void => {
    this.newUser = { role: 'Viewer', is_active: true };
    this.showNew = true;
  };

  submitNew(): void {
    if (!this.newUser.username?.trim()) { notify('Username required', 'warning', 2000); return; }
    this.admin.saveTenantUser(this.newUser).subscribe({
      next: () => { notify('User created', 'success', 1500); this.showNew = false; this.refresh(); },
      error: () => notify('Create failed', 'error', 3000)
    });
  }

  onRowUpdate = (e: any): void => {
    const merged = { ...e.oldData, ...e.newData };
    e.cancel = (async () => {
      try {
        await this.admin.saveTenantUser(merged).toPromise();
        notify('User updated', 'success', 1500);
      } catch {
        notify('Update failed', 'error', 3000);
        return true;
      }
      return false;
    })();
  };

  openReset(user: TenantUser): void {
    this.resetUser = user;
    this.resetPwd = '';
    this.showReset = true;
  }

  submitReset(): void {
    if (!this.resetUser?.id) return;
    if (!this.resetPwd || this.resetPwd.length < 8) {
      notify('Password must be at least 8 characters', 'warning', 2000);
      return;
    }
    this.admin.resetTenantUserPassword(this.resetUser.id, this.resetPwd).subscribe({
      next: () => { notify('Password reset', 'success', 1500); this.showReset = false; this.resetPwd = ''; },
      error: () => notify('Reset failed', 'error', 3000)
    });
  }

  remove(user: TenantUser): void {
    if (!user.id) return;
    confirm(`Delete user "${user.username}"? This cannot be undone.`, 'Confirm').then(ok => {
      if (!ok) return;
      this.admin.deleteTenantUser(user.id!).subscribe({
        next: () => { notify('User deleted', 'info', 1500); this.refresh(); },
        error: () => notify('Delete failed', 'error', 3000)
      });
    });
  }
}
