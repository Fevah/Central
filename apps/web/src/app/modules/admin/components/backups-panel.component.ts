import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxDataGridModule, DxButtonModule, DxToolbarModule, DxTabPanelModule } from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { confirm } from 'devextreme/ui/dialog';
import { AdminService, BackupHistoryEntry } from '../../../core/services/admin.service';

/**
 * Backups + soft-delete purge — mirrors the WPF Admin → Backups panel.
 *
 * Tab 1 (History): list of pg_dump runs with size/duration/status.
 * Tab 2 (Purge):   tables with soft-deleted rows; click "Purge" to hard-delete.
 *
 * Run-now triggers a backup synchronously (server returns when dump completes).
 * The user is warned before purge — purge is irreversible.
 */
@Component({
  selector: 'app-backups-panel',
  standalone: true,
  imports: [CommonModule, DxDataGridModule, DxButtonModule, DxToolbarModule, DxTabPanelModule],
  template: `
    <dx-toolbar style="margin-bottom: 8px;">
      <dxi-item location="before"><div class="title">Backups & Purge</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ text: 'Run backup now', type: 'success', icon: 'save', onClick: runBackup }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }"></dxi-item>
    </dx-toolbar>

    <dx-tab-panel [items]="tabs" [selectedIndex]="0" [animationEnabled]="false">
      <div *dxTemplate="let tab of 'item'">
        <ng-container [ngSwitch]="tab.key">

          <!-- History -->
          <div *ngSwitchCase="'history'">
            <dx-data-grid [dataSource]="history" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                          height="calc(100vh - 380px)">
              <dxi-column dataField="id" caption="#" width="60" />
              <dxi-column dataField="started_at" caption="Started" dataType="datetime" width="160" />
              <dxi-column dataField="status" caption="Status" width="100" cellTemplate="statusTpl" />
              <dxi-column dataField="duration_ms" caption="Duration (ms)" width="120" dataType="number" />
              <dxi-column dataField="file_size_bytes" caption="Size" width="100" cellTemplate="sizeTpl" />
              <dxi-column dataField="file_path" caption="File" />
              <dxi-column dataField="error" caption="Error" />

              <div *dxTemplate="let d of 'statusTpl'">
                <span [class]="'status status-' + (d.value || 'unknown').toLowerCase()">{{ d.value }}</span>
              </div>
              <div *dxTemplate="let d of 'sizeTpl'">
                <span>{{ formatSize(d.value) }}</span>
              </div>
            </dx-data-grid>
            <div class="empty-state" *ngIf="!loadingHistory && history.length === 0">
              No backups yet — click “Run backup now” to create one.
            </div>
          </div>

          <!-- Purge -->
          <div *ngSwitchCase="'purge'">
            <p class="warn">
              ⚠ Purging permanently deletes soft-deleted rows. This cannot be undone.
            </p>
            <dx-data-grid [dataSource]="purgeRows" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" height="calc(100vh - 420px)">
              <dxi-column dataField="table" caption="Table" width="280" />
              <dxi-column dataField="count" caption="Soft-deleted rows" width="180" dataType="number" />
              <dxi-column caption="Action" cellTemplate="purgeBtn" />

              <div *dxTemplate="let d of 'purgeBtn'">
                <dx-button text="Purge {{ d.data.count }}" type="danger"
                           [stylingMode]="'outlined'" [disabled]="d.data.count === 0"
                           (onClick)="purge(d.data)" />
              </div>
            </dx-data-grid>
          </div>

        </ng-container>
      </div>
    </dx-tab-panel>
  `,
  styles: [`
    .title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .warn { color: #f59e0b; font-size: 13px; padding: 8px 12px; background: rgba(245,158,11,0.08);
            border: 1px solid rgba(245,158,11,0.3); border-radius: 6px; margin-bottom: 12px; }
    .status { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .status-success, .status-completed { background: rgba(34,197,94,0.2); color: #22c55e; }
    .status-failed, .status-error      { background: rgba(239,68,68,0.2); color: #ef4444; }
    .status-running, .status-pending   { background: rgba(234,179,8,0.2); color: #eab308; }
  `]
})
export class BackupsPanelComponent implements OnInit {
  readonly tabs = [
    { title: 'History', key: 'history' },
    { title: 'Purge',   key: 'purge' },
  ];

  history: BackupHistoryEntry[] = [];
  purgeRows: { table: string; count: number }[] = [];
  loadingHistory = false;
  loadingPurge = false;

  constructor(private admin: AdminService) {}

  ngOnInit(): void { this.refresh(); }

  refresh = (): void => {
    this.loadingHistory = true;
    this.admin.getBackupHistory().subscribe({
      next: h => { this.history = h; this.loadingHistory = false; },
      error: () => { this.loadingHistory = false; notify('Failed to load backup history', 'error', 3000); }
    });

    this.loadingPurge = true;
    this.admin.getPurgeCounts().subscribe({
      next: counts => {
        this.purgeRows = Object.entries(counts).map(([table, count]) => ({ table, count }));
        this.loadingPurge = false;
      },
      error: () => { this.loadingPurge = false; /* purge counts are optional */ }
    });
  };

  runBackup = (): void => {
    notify('Backup started…', 'info', 2000);
    this.admin.runBackup().subscribe({
      next: r => {
        notify(`Backup ${r.status} (${this.formatSize(r.file_size_bytes)})`, 'success', 3000);
        this.refresh();
      },
      error: () => notify('Backup failed', 'error', 3000)
    });
  };

  purge(row: { table: string; count: number }): void {
    if (row.count === 0) return;
    confirm(`Permanently delete ${row.count} rows from <b>${row.table}</b>? This cannot be undone.`,
            'Confirm purge').then(ok => {
      if (!ok) return;
      this.admin.purgeTable(row.table).subscribe({
        next: r => { notify(`Purged ${r.purged} rows`, 'success', 2000); this.refresh(); },
        error: () => notify('Purge failed', 'error', 3000)
      });
    });
  }

  formatSize(bytes?: number | null): string {
    if (bytes == null || bytes < 0) return '–';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  }
}
