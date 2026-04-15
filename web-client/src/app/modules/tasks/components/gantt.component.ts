import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  DxGanttModule, DxToolbarModule, DxSelectBoxModule, DxButtonModule
} from 'devextreme-angular';
import { DxoToolbarModule, DxiToolbarItemModule, DxiColumnModule } from 'devextreme-angular/ui/nested';
import { forkJoin } from 'rxjs';
import notify from 'devextreme/ui/notify';
import { TaskService, Project, Task, TaskDependency } from '../../../core/services/task.service';
import { TasksSubNavComponent } from './tasks-sub-nav.component';

interface GanttTask {
  id:       number;
  parentId: number | null;
  title:    string;
  start:    Date;
  end:      Date;
  progress: number;  // 0..100
}

interface GanttDep {
  id:        number;
  predecessorId: number;
  successorId:   number;
  type:      number;  // 0=FS, 1=SS, 2=FF, 3=SF (DevExtreme Gantt enum)
}

/**
 * Gantt view — DxGantt over the project's task hierarchy + dependencies.
 *
 * Tasks without start/finish dates fall back to created_at and a default
 * 1-day duration so the bar still renders. Progress is derived from
 * status (Done = 100, Review = 75, InProgress = 50, Open = 0).
 *
 * Dependencies are loaded on demand for the visible tasks (one HTTP call
 * per task today — fine for small projects, will need a bulk endpoint
 * for projects with thousands of tasks).
 */
@Component({
  selector: 'app-gantt',
  standalone: true,
  imports: [
    CommonModule, TasksSubNavComponent,
    DxGanttModule, DxToolbarModule, DxSelectBoxModule, DxButtonModule,
    DxoToolbarModule, DxiToolbarItemModule, DxiColumnModule,
  ],
  template: `
    <app-tasks-sub-nav></app-tasks-sub-nav>

    <dx-toolbar class="page-toolbar">
      <dxi-item location="before">
        <dx-select-box [items]="projects" displayExpr="name" valueExpr="id"
                       [value]="projectId" placeholder="Project" width="220"
                       (onValueChanged)="onProjectChanged($event)"></dx-select-box>
      </dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: reload }"></dxi-item>
    </dx-toolbar>

    <dx-gantt *ngIf="ganttTasks.length > 0"
              [tasks]="{ dataSource: ganttTasks }"
              [dependencies]="{ dataSource: ganttDeps }"
              [taskListWidth]="420"
              [scaleType]="'weeks'"
              height="calc(100vh - 220px)">
      <dxo-toolbar>
        <dxi-item name="undo" />
        <dxi-item name="redo" />
        <dxi-item name="separator" />
        <dxi-item name="zoomIn" />
        <dxi-item name="zoomOut" />
      </dxo-toolbar>
      <dxi-column dataField="title" caption="Task" width="280" />
      <dxi-column dataField="start" caption="Start" dataType="date" width="80" />
      <dxi-column dataField="end"   caption="End"   dataType="date" width="80" />
    </dx-gantt>

    <div class="empty-state" *ngIf="!loading && ganttTasks.length === 0">
      No tasks in this project yet.
    </div>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .empty-state { text-align: center; color: #6b7280; padding: 48px; font-size: 13px; }
    dx-gantt { display: block; }
  `]
})
export class GanttComponent implements OnInit {
  projects: Project[] = [];
  projectId?: number;
  ganttTasks: GanttTask[] = [];
  ganttDeps:  GanttDep[]  = [];
  loading = false;

  constructor(private taskService: TaskService) {}

  ngOnInit(): void {
    this.taskService.getProjects().subscribe(p => {
      this.projects = p;
      if (p.length > 0) {
        this.projectId = p[0].id;
        this.reload();
      }
    });
  }

  onProjectChanged(e: any): void {
    this.projectId = e.value;
    this.reload();
  }

  reload = (): void => {
    if (!this.projectId) return;
    this.loading = true;
    this.taskService.getTasks(this.projectId, undefined, 500).subscribe({
      next: tasks => {
        this.ganttTasks = tasks.map(t => this.toGantt(t));
        this.loadDependencies(tasks);
        this.loading = false;
      },
      error: () => { this.loading = false; notify('Failed to load tasks', 'error', 3000); }
    });
  };

  /**
   * Pull dependencies for every task in parallel. For projects with
   * 1000s of tasks this is wasteful — a future server-side bulk endpoint
   * (`GET /api/projects/{id}/dependencies`) would be a one-shot win.
   */
  private loadDependencies(tasks: Task[]): void {
    if (tasks.length === 0) { this.ganttDeps = []; return; }
    if (tasks.length > 100) {
      // Skip dep load for large projects — would issue 1k+ HTTP calls.
      this.ganttDeps = [];
      return;
    }
    forkJoin(tasks.map(t => this.taskService.getDependencies(t.id))).subscribe({
      next: results => this.ganttDeps = this.flattenDeps(results),
      error: () => this.ganttDeps = []  // dependencies are non-essential
    });
  }

  private flattenDeps(results: TaskDependency[][]): GanttDep[] {
    const flat: GanttDep[] = [];
    const seen = new Set<number>();
    for (const arr of results) {
      for (const d of arr) {
        if (seen.has(d.id)) continue;  // Each link returned twice (pred+succ side)
        seen.add(d.id);
        flat.push({
          id:            d.id,
          predecessorId: d.predecessor_id,
          successorId:   d.successor_id,
          type:          this.depTypeCode(d.dep_type),
        });
      }
    }
    return flat;
  }

  /**
   * Maps DB dep_type strings to DevExtreme Gantt's numeric enum.
   * 0=FS (finish-start, default), 1=SS, 2=FF, 3=SF.
   */
  private depTypeCode(type?: string): number {
    switch ((type ?? '').toUpperCase()) {
      case 'SS': return 1;
      case 'FF': return 2;
      case 'SF': return 3;
      default:   return 0;
    }
  }

  /**
   * Convert a task to DxGantt's expected shape. We always need start + end
   * dates — fall back to today for missing data so the bar still renders.
   */
  private toGantt(t: Task): GanttTask {
    const today = new Date();
    const start = t.start_date  ? new Date(t.start_date)  : (t.due_date ? new Date(t.due_date) : today);
    const end   = t.finish_date ? new Date(t.finish_date) : (t.due_date ? new Date(t.due_date) : new Date(start.getTime() + 86_400_000));
    return {
      id:       t.id,
      parentId: t.parent_id,
      title:    t.title,
      start, end,
      progress: this.statusToProgress(t.status),
    };
  }

  private statusToProgress(status: string): number {
    switch (status) {
      case 'Done':       return 100;
      case 'Review':     return 75;
      case 'InProgress': return 50;
      case 'Blocked':    return 25;
      default:           return 0;
    }
  }
}
