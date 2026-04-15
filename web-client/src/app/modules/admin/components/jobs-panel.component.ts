import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  DxDataGridModule, DxButtonModule, DxToolbarModule, DxTabPanelModule,
  DxNumberBoxModule, DxPopupModule
} from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { AdminService, JobSchedule, JobHistoryEntry } from '../../../core/services/admin.service';

/**
 * Background-jobs management — mirrors the WPF Admin → Jobs panel.
 *
 * Tab 1 (Schedules): per-job state, enable/disable, run-now, change interval.
 * Tab 2 (History):   last 50 executions across all jobs with status + duration.
 *
 * "Change interval" uses an inline edit popup because we need validation
 * (positive integer, sensible upper bound). The grid does not allow direct
 * inline editing of intervals to avoid accidental zero/negative values.
 */
@Component({
  selector: 'app-jobs-panel',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    DxDataGridModule, DxButtonModule, DxToolbarModule, DxTabPanelModule,
    DxNumberBoxModule, DxPopupModule
  ],
  template: `
    <dx-toolbar style="margin-bottom: 8px;">
      <dxi-item location="before"><div class="title">Background Jobs</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }"></dxi-item>
    </dx-toolbar>

    <dx-tab-panel [items]="tabs" [selectedIndex]="0" [animationEnabled]="false">
      <div *dxTemplate="let tab of 'item'">
        <ng-container [ngSwitch]="tab.key">

          <!-- Schedules -->
          <div *ngSwitchCase="'schedules'">
            <dx-data-grid [dataSource]="jobs" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                          height="calc(100vh - 380px)">
              <dxi-column dataField="id" caption="ID" width="60" />
              <dxi-column dataField="name" caption="Name" width="200" />
              <dxi-column dataField="job_type" caption="Type" width="140" />
              <dxi-column dataField="is_enabled" caption="Enabled" width="80" dataType="boolean" />
              <dxi-column dataField="interval_minutes" caption="Every (min)" width="100" />
              <dxi-column dataField="last_run_at" caption="Last run" dataType="datetime" width="160" />
              <dxi-column dataField="next_run_at" caption="Next run" dataType="datetime" width="160" />
              <dxi-column dataField="last_status" caption="Status" width="100" />
              <dxi-column caption="Actions" width="280" cellTemplate="actionsTpl" />

              <div *dxTemplate="let d of 'actionsTpl'">
                <dx-button text="Run now" [stylingMode]="'outlined'" type="success"
                           [width]="80" (onClick)="run(d.data)" />
                <dx-button *ngIf="d.data.is_enabled" text="Disable" [stylingMode]="'outlined'"
                           [width]="80" (onClick)="disable(d.data)" />
                <dx-button *ngIf="!d.data.is_enabled" text="Enable" [stylingMode]="'outlined'" type="default"
                           [width]="80" (onClick)="enable(d.data)" />
                <dx-button icon="edit" hint="Change interval" [stylingMode]="'text'"
                           (onClick)="openInterval(d.data)" />
              </div>
            </dx-data-grid>
            <div class="empty-state" *ngIf="!loadingJobs && jobs.length === 0">
              No scheduled jobs.
            </div>
          </div>

          <!-- History -->
          <div *ngSwitchCase="'history'">
            <dx-data-grid [dataSource]="history" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                          height="calc(100vh - 380px)">
              <dxi-column dataField="id" caption="#" width="60" />
              <dxi-column dataField="job_type" caption="Type" width="140" />
              <dxi-column dataField="status" caption="Status" width="100" cellTemplate="statusTpl" />
              <dxi-column dataField="started_at" caption="Started" dataType="datetime" width="160" />
              <dxi-column dataField="duration_ms" caption="Duration (ms)" width="120" dataType="number" />
              <dxi-column dataField="summary" caption="Summary" />
              <dxi-column dataField="error" caption="Error" />

              <div *dxTemplate="let d of 'statusTpl'">
                <span [class]="'status status-' + (d.value || 'unknown').toLowerCase()">{{ d.value }}</span>
              </div>
            </dx-data-grid>
            <div class="empty-state" *ngIf="!loadingHistory && history.length === 0">
              No job runs recorded yet.
            </div>
          </div>

        </ng-container>
      </div>
    </dx-tab-panel>

    <!-- Interval editor -->
    <dx-popup [(visible)]="showIntervalEdit" title="Change interval"
              [width]="380" [height]="220" [showCloseButton]="true">
      <p class="muted">Job: <strong>{{ editingJob?.name }}</strong></p>
      <dx-number-box [(value)]="editIntervalMin" [min]="1" [max]="10080"
                     placeholder="Minutes between runs"></dx-number-box>
      <p class="hint">1 = every minute, 60 = hourly, 1440 = daily, 10080 = weekly.</p>
      <div class="dialog-actions">
        <dx-button text="Cancel" (onClick)="showIntervalEdit = false" />
        <dx-button text="Save" type="success" (onClick)="saveInterval()" />
      </div>
    </dx-popup>
  `,
  styles: [`
    .title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .muted { color: #9ca3af; font-size: 13px; }
    .hint { color: #6b7280; font-size: 12px; margin-top: 8px; }
    .dialog-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
    .status { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .status-success, .status-completed { background: rgba(34,197,94,0.2); color: #22c55e; }
    .status-failed, .status-error      { background: rgba(239,68,68,0.2); color: #ef4444; }
    .status-running, .status-pending   { background: rgba(234,179,8,0.2); color: #eab308; }
    dx-button + dx-button { margin-left: 6px; }
  `]
})
export class JobsPanelComponent implements OnInit {
  readonly tabs = [
    { title: 'Schedules', key: 'schedules' },
    { title: 'History',   key: 'history' },
  ];

  jobs:    JobSchedule[]      = [];
  history: JobHistoryEntry[] = [];
  loadingJobs    = false;
  loadingHistory = false;

  showIntervalEdit = false;
  editingJob: JobSchedule | null = null;
  editIntervalMin = 60;

  constructor(private admin: AdminService) {}

  ngOnInit(): void { this.refresh(); }

  refresh = (): void => {
    this.loadingJobs = true;
    this.admin.getJobs().subscribe({
      next: j => { this.jobs = j; this.loadingJobs = false; },
      error: () => { this.loadingJobs = false; notify('Failed to load jobs', 'error', 3000); }
    });

    this.loadingHistory = true;
    this.admin.getJobHistory().subscribe({
      next: h => { this.history = h; this.loadingHistory = false; },
      error: () => { this.loadingHistory = false; /* history is optional */ }
    });
  };

  run(j: JobSchedule): void {
    this.admin.runJob(j.id).subscribe({
      next: r => { notify(`Job dispatched (${r.status})`, 'success', 1500); this.refresh(); },
      error: () => notify('Run failed', 'error', 3000)
    });
  }

  enable(j: JobSchedule): void {
    this.admin.enableJob(j.id).subscribe({
      next: () => { notify('Job enabled', 'success', 1500); this.refresh(); },
      error: () => notify('Enable failed', 'error', 3000)
    });
  }

  disable(j: JobSchedule): void {
    this.admin.disableJob(j.id).subscribe({
      next: () => { notify('Job disabled', 'info', 1500); this.refresh(); },
      error: () => notify('Disable failed', 'error', 3000)
    });
  }

  openInterval(j: JobSchedule): void {
    this.editingJob = j;
    this.editIntervalMin = j.interval_minutes || 60;
    this.showIntervalEdit = true;
  }

  saveInterval(): void {
    if (!this.editingJob) return;
    if (!this.editIntervalMin || this.editIntervalMin < 1) {
      notify('Interval must be at least 1 minute', 'warning', 2000);
      return;
    }
    this.admin.setJobInterval(this.editingJob.id, this.editIntervalMin).subscribe({
      next: () => { notify('Interval updated', 'success', 1500); this.showIntervalEdit = false; this.refresh(); },
      error: () => notify('Update failed', 'error', 3000)
    });
  }
}
