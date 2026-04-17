import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxToolbarModule, DxSelectBoxModule,
  DxPopupModule, DxFormModule
} from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { confirm } from 'devextreme/ui/dialog';
import { TaskService, Project, Sprint } from '../../../core/services/task.service';
import { TasksSubNavComponent } from './tasks-sub-nav.component';

/**
 * Sprint board — mirrors the WPF Tasks → Sprints panel.
 *
 * Lists all sprints in a project, supports CRUD inline, and lets the user
 * jump to the burndown chart for a selected sprint. Sprint *backlog* (the
 * drag-and-drop assignment of tasks to a sprint) is a separate panel; this
 * one is just sprint definition + status.
 */
@Component({
  selector: 'app-sprint-board',
  standalone: true,
  imports: [
    CommonModule, RouterModule, TasksSubNavComponent,
    DxDataGridModule, DxButtonModule, DxToolbarModule, DxSelectBoxModule,
    DxPopupModule, DxFormModule
  ],
  template: `
    <app-tasks-sub-nav></app-tasks-sub-nav>

    <dx-toolbar class="page-toolbar">
      <dxi-item location="before">
        <dx-select-box [items]="projects" displayExpr="name" valueExpr="id"
                       placeholder="Project" [value]="projectId"
                       width="220" (onValueChanged)="onProjectChanged($event)"></dx-select-box>
      </dxi-item>
      <dxi-item location="before" widget="dxButton"
                [options]="{ text: '+ New sprint', type: 'default', onClick: openNew, disabled: !projectId }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="sprints" [showBorders]="true" [rowAlternationEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  [editing]="{ mode: 'row', allowUpdating: true, allowDeleting: true, useIcons: true }"
                  (onRowUpdating)="onRowUpdate($event)"
                  (onRowRemoving)="onRowRemove($event)"
                  height="calc(100vh - 240px)">
      <dxi-column dataField="id" caption="ID" width="60" [allowEditing]="false" />
      <dxi-column dataField="name" caption="Name" width="200" />
      <dxi-column dataField="status" caption="Status" width="120" editorType="dxSelectBox"
                  [editorOptions]="{ items: ['Planning','Active','Completed','Cancelled'] }"
                  cellTemplate="statusTpl" />
      <dxi-column dataField="start_date" caption="Start" dataType="date" width="120" />
      <dxi-column dataField="end_date"   caption="End"   dataType="date" width="120" />
      <dxi-column dataField="goal" caption="Goal" />
      <dxi-column dataField="velocity_points" caption="Pts done" width="100" [allowEditing]="false" />
      <dxi-column dataField="velocity_hours"  caption="Hrs done" width="100" [allowEditing]="false" />
      <dxi-column caption="Actions" width="160" [allowEditing]="false" cellTemplate="actionsTpl" />

      <div *dxTemplate="let d of 'statusTpl'">
        <span [class]="'pill pill-' + (d.value || 'planning').toLowerCase()">{{ d.value }}</span>
      </div>
      <div *dxTemplate="let d of 'actionsTpl'">
        <dx-button text="Burndown" [stylingMode]="'outlined'" [width]="100"
                   (onClick)="openBurndown(d.data)" />
      </div>
    </dx-data-grid>

    <div class="empty-state" *ngIf="!loading && projectId && sprints.length === 0">
      No sprints yet — use "+ New sprint" to create one.
    </div>
    <div class="empty-state" *ngIf="!projectId">
      Pick a project to see its sprints.
    </div>

    <!-- New sprint dialog -->
    <dx-popup [(visible)]="showNew" title="New sprint" [width]="480" [height]="380" [showCloseButton]="true">
      <dx-form [(formData)]="newSprint" labelLocation="top">
        <dxi-item dataField="name">
          <dxi-validation-rule type="required" />
        </dxi-item>
        <dxi-item dataField="start_date" editorType="dxDateBox" />
        <dxi-item dataField="end_date"   editorType="dxDateBox" />
        <dxi-item dataField="goal" editorType="dxTextArea" [editorOptions]="{ height: 80 }" />
        <dxi-item dataField="status" editorType="dxSelectBox"
                  [editorOptions]="{ items: ['Planning','Active','Completed','Cancelled'], value: 'Planning' }" />
      </dx-form>
      <div class="dialog-actions">
        <dx-button text="Cancel" (onClick)="showNew = false" />
        <dx-button text="Create" type="success" (onClick)="submitNew()" />
      </div>
    </dx-popup>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .dialog-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
    .pill { padding: 2px 10px; border-radius: 12px; font-size: 11px; font-weight: 600; }
    .pill-planning  { background: rgba(107,114,128,0.2); color: #9ca3af; }
    .pill-active    { background: rgba(34,197,94,0.2);   color: #22c55e; }
    .pill-completed { background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .pill-cancelled { background: rgba(239,68,68,0.2);   color: #ef4444; }
  `]
})
export class SprintBoardComponent implements OnInit {
  projects: Project[] = [];
  projectId?: number;
  sprints: Sprint[] = [];
  loading = false;

  showNew = false;
  newSprint: Partial<Sprint> = { status: 'Planning' };

  constructor(private taskService: TaskService, private router: Router) {}

  ngOnInit(): void { this.loadProjects(); }

  loadProjects(): void {
    this.taskService.getProjects().subscribe({
      next: p => {
        this.projects = p;
        // Auto-select first project so the page isn't empty on first load.
        if (!this.projectId && p.length > 0) {
          this.projectId = p[0].id;
          this.refresh();
        }
      },
      error: () => notify('Failed to load projects', 'error', 3000)
    });
  }

  refresh = (): void => {
    if (!this.projectId) return;
    this.loading = true;
    this.taskService.getSprints(this.projectId).subscribe({
      next: s => { this.sprints = s; this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load sprints', 'error', 3000); }
    });
  };

  onProjectChanged(e: any): void {
    this.projectId = e.value;
    this.sprints = [];
    this.refresh();
  }

  openNew = (): void => {
    this.newSprint = { name: '', status: 'Planning', goal: '' };
    this.showNew = true;
  };

  submitNew(): void {
    if (!this.projectId || !this.newSprint.name?.trim()) {
      notify('Name required', 'warning', 2000);
      return;
    }
    this.taskService.createSprint(this.projectId, this.newSprint).subscribe({
      next: () => { notify('Sprint created', 'success', 1500); this.showNew = false; this.refresh(); },
      error: () => notify('Create failed', 'error', 3000)
    });
  }

  onRowUpdate = (e: any): void => {
    if (!this.projectId) return;
    const merged: Sprint = { ...e.oldData, ...e.newData };
    e.cancel = (async () => {
      try {
        await this.taskService.updateSprint(this.projectId!, merged.id, merged).toPromise();
        notify('Sprint updated', 'success', 1500);
      } catch {
        notify('Update failed', 'error', 3000);
        return true;
      }
      return false;
    })();
  };

  onRowRemove = (e: any): void => {
    if (!this.projectId) return;
    e.cancel = (async () => {
      const ok = await confirm(`Delete sprint "${e.data.name}"?`, 'Confirm');
      if (!ok) return true;
      try {
        await this.taskService.deleteSprint(this.projectId!, e.data.id).toPromise();
        notify('Sprint deleted', 'info', 1500);
      } catch {
        notify('Delete failed', 'error', 3000);
        return true;
      }
      return false;
    })();
  };

  openBurndown(s: Sprint): void {
    if (!this.projectId) return;
    this.router.navigate(['/tasks/burndown'], { queryParams: { project: this.projectId, sprint: s.id } });
  }
}
