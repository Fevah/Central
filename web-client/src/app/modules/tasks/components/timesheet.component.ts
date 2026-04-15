import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  DxDataGridModule, DxToolbarModule, DxButtonModule, DxSelectBoxModule,
  DxPopupModule, DxFormModule
} from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { confirm } from 'devextreme/ui/dialog';
import { TaskService, Project, Task, TimeEntry } from '../../../core/services/task.service';
import { TasksSubNavComponent } from './tasks-sub-nav.component';

/**
 * Timesheet — pick a task, see all logged time entries, add new ones.
 *
 * Mirrors the WPF Tasks → Timesheet panel. We deliberately keep this
 * task-scoped (you log time *against a task*) rather than user-scoped
 * (which would need a "my time across all tasks" rollup that doesn't
 * have a server endpoint yet).
 */
@Component({
  selector: 'app-timesheet',
  standalone: true,
  imports: [
    CommonModule, FormsModule, TasksSubNavComponent,
    DxDataGridModule, DxToolbarModule, DxButtonModule, DxSelectBoxModule,
    DxPopupModule, DxFormModule
  ],
  template: `
    <app-tasks-sub-nav></app-tasks-sub-nav>

    <dx-toolbar class="page-toolbar">
      <dxi-item location="before">
        <dx-select-box [items]="projects" displayExpr="name" valueExpr="id"
                       [value]="projectId" placeholder="Project" width="200"
                       (onValueChanged)="onProjectChanged($event)"></dx-select-box>
      </dxi-item>
      <dxi-item location="before">
        <dx-select-box [items]="tasks" displayExpr="title" valueExpr="id"
                       [value]="taskId" placeholder="Task" width="280"
                       [disabled]="!projectId" [searchEnabled]="true"
                       (onValueChanged)="onTaskChanged($event)"></dx-select-box>
      </dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ text: '+ Log time', type: 'default', onClick: openAdd, disabled: !taskId }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: reload }"></dxi-item>
    </dx-toolbar>

    <div class="totals" *ngIf="entries.length > 0">
      <span class="total"><strong>{{ totalHours.toFixed(1) }}h</strong> across {{ entries.length }} entries</span>
    </div>

    <dx-data-grid [dataSource]="entries" [showBorders]="true" [rowAlternationEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  height="calc(100vh - 280px)">
      <dxi-column dataField="entry_date" caption="Date" dataType="date" width="120" sortOrder="desc" />
      <dxi-column dataField="hours" caption="Hours" width="80" dataType="number" />
      <dxi-column dataField="activity_type" caption="Activity" width="140" />
      <dxi-column dataField="user" caption="By" width="160" />
      <dxi-column dataField="notes" caption="Notes" />
      <dxi-column caption="" width="60" cellTemplate="actionsTpl" />

      <div *dxTemplate="let d of 'actionsTpl'">
        <dx-button icon="trash" hint="Delete" [stylingMode]="'text'" type="danger"
                   (onClick)="remove(d.data)" />
      </div>
    </dx-data-grid>

    <div class="empty-state" *ngIf="!loading && taskId && entries.length === 0">
      No time logged on this task yet.
    </div>
    <div class="empty-state" *ngIf="!taskId">
      Pick a project + task to view its time entries.
    </div>

    <!-- Add entry dialog -->
    <dx-popup [(visible)]="showAdd" title="Log time" [width]="460" [height]="380" [showCloseButton]="true">
      <dx-form [(formData)]="newEntry" labelLocation="top">
        <dxi-item dataField="entry_date" editorType="dxDateBox"
                  [editorOptions]="{ value: today }" />
        <dxi-item dataField="hours" editorType="dxNumberBox"
                  [editorOptions]="{ min: 0.25, max: 24, format: '#0.0', value: 1 }">
          <dxi-validation-rule type="required" />
        </dxi-item>
        <dxi-item dataField="activity_type" editorType="dxSelectBox"
                  [editorOptions]="{ items: ['Development','Review','Testing','Meeting','Research','Bugfix','Other'] }" />
        <dxi-item dataField="notes" editorType="dxTextArea"
                  [editorOptions]="{ height: 90, placeholder: 'Optional context' }" />
      </dx-form>
      <div class="dialog-actions">
        <dx-button text="Cancel" (onClick)="showAdd = false" />
        <dx-button text="Log" type="success" (onClick)="submitAdd()" />
      </div>
    </dx-popup>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .totals { display: flex; gap: 12px; margin-bottom: 8px; padding: 8px 12px;
              background: rgba(34,197,94,0.05); border: 1px solid rgba(34,197,94,0.2);
              border-radius: 6px; color: #9ca3af; font-size: 13px; }
    .total strong { color: #22c55e; font-size: 15px; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .dialog-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
  `]
})
export class TimesheetComponent implements OnInit {
  projects: Project[] = [];
  tasks: Task[] = [];
  entries: TimeEntry[] = [];

  projectId?: number;
  taskId?: number;

  loading = false;
  showAdd = false;
  newEntry: any = {};
  readonly today = new Date();

  get totalHours(): number {
    return this.entries.reduce((acc, e) => acc + Number(e.hours || 0), 0);
  }

  constructor(private taskService: TaskService) {}

  ngOnInit(): void {
    this.taskService.getProjects().subscribe(p => {
      this.projects = p;
      if (p.length > 0) {
        this.projectId = p[0].id;
        this.loadTasks();
      }
    });
  }

  onProjectChanged(e: any): void {
    this.projectId = e.value;
    this.taskId = undefined;
    this.entries = [];
    this.loadTasks();
  }

  onTaskChanged(e: any): void {
    this.taskId = e.value;
    this.reload();
  }

  private loadTasks(): void {
    if (!this.projectId) return;
    this.taskService.getTasks(this.projectId, undefined, 500).subscribe({
      next: t => this.tasks = t,
      error: () => notify('Failed to load tasks', 'error', 3000)
    });
  }

  reload = (): void => {
    if (!this.taskId) return;
    this.loading = true;
    this.taskService.getTimeEntries(this.taskId).subscribe({
      next: e => { this.entries = e; this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load time entries', 'error', 3000); }
    });
  };

  openAdd = (): void => {
    this.newEntry = { entry_date: this.today, hours: 1, activity_type: 'Development', notes: '' };
    this.showAdd = true;
  };

  submitAdd(): void {
    if (!this.taskId) return;
    if (!this.newEntry.hours || this.newEntry.hours <= 0) {
      notify('Hours must be > 0', 'warning', 2000);
      return;
    }
    const body = {
      hours:         Number(this.newEntry.hours),
      entry_date:    this.formatDate(this.newEntry.entry_date),
      activity_type: this.newEntry.activity_type,
      notes:         this.newEntry.notes,
    };
    this.taskService.addTimeEntry(this.taskId, body).subscribe({
      next: () => { notify('Time logged', 'success', 1500); this.showAdd = false; this.reload(); },
      error: () => notify('Log failed', 'error', 3000)
    });
  }

  remove(entry: TimeEntry): void {
    if (!this.taskId) return;
    confirm(`Delete this ${entry.hours}h entry?`, 'Confirm').then(ok => {
      if (!ok) return;
      this.taskService.deleteTimeEntry(this.taskId!, entry.id).subscribe({
        next: () => { notify('Entry deleted', 'info', 1500); this.reload(); },
        error: () => notify('Delete failed', 'error', 3000)
      });
    });
  }

  /** Date → 'YYYY-MM-DD' (server expects ISO date, not full timestamp). */
  private formatDate(d: any): string {
    const date = d instanceof Date ? d : new Date(d);
    return date.toISOString().substring(0, 10);
  }
}
