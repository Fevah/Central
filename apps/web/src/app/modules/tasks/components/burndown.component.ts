import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  DxChartModule, DxToolbarModule, DxButtonModule, DxSelectBoxModule
} from 'devextreme-angular';
import {
  DxoCommonSeriesSettingsModule, DxiSeriesModule,
  DxoArgumentAxisModule, DxoValueAxisModule, DxoLabelModule, DxoTitleModule,
  DxoLegendModule, DxoTooltipModule
} from 'devextreme-angular/ui/nested';
import notify from 'devextreme/ui/notify';
import { TaskService, Project, Sprint, BurndownPoint } from '../../../core/services/task.service';
import { TasksSubNavComponent } from './tasks-sub-nav.component';

interface ChartPoint {
  date:           string;
  pointsRemaining: number;
  pointsCompleted: number;
  ideal:          number | null;
}

/**
 * Burndown chart — DxChart of remaining points over time, with the
 * canonical "ideal line" (linear from sprint start total → 0 at end).
 *
 * Picks the project + sprint from URL query params if present (so the
 * Sprints page can deep-link), otherwise from the dropdowns.
 *
 * "Snapshot now" forces the server to recompute today's row and reload —
 * useful when you want the chart to reflect changes you just made.
 */
@Component({
  selector: 'app-burndown',
  standalone: true,
  imports: [
    CommonModule, RouterModule, TasksSubNavComponent,
    DxChartModule, DxToolbarModule, DxButtonModule, DxSelectBoxModule,
    DxoCommonSeriesSettingsModule, DxiSeriesModule,
    DxoArgumentAxisModule, DxoValueAxisModule, DxoLabelModule, DxoTitleModule,
    DxoLegendModule, DxoTooltipModule,
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
        <dx-select-box [items]="sprints" displayExpr="name" valueExpr="id"
                       [value]="sprintId" placeholder="Sprint" width="220"
                       [disabled]="!projectId" (onValueChanged)="onSprintChanged($event)"></dx-select-box>
      </dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'photo', text: 'Snapshot now', onClick: snapshot, disabled: !sprintId }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: reload }"></dxi-item>
    </dx-toolbar>

    <div class="sprint-meta" *ngIf="selectedSprint">
      <span><strong>{{ selectedSprint.name }}</strong></span>
      <span>{{ selectedSprint.start_date || '–' }} → {{ selectedSprint.end_date || '–' }}</span>
      <span *ngIf="selectedSprint.goal" class="goal">Goal: {{ selectedSprint.goal }}</span>
    </div>

    <dx-chart *ngIf="chartData.length > 0" [dataSource]="chartData"
              palette="Soft" [animation]="{ enabled: true, duration: 400 }">
      <dxo-common-series-settings argumentField="date" type="line" />
      <dxi-series valueField="pointsRemaining" name="Points remaining"
                  [point]="{ visible: true, size: 8 }"
                  [color]="'#60a5fa'" />
      <dxi-series valueField="ideal" name="Ideal"
                  [dashStyle]="'dash'" [color]="'#9ca3af'"
                  [point]="{ visible: false }" />
      <dxi-series valueField="pointsCompleted" name="Points completed"
                  [color]="'#22c55e'"
                  [point]="{ visible: true, size: 6 }" />
      <dxo-argument-axis>
        <dxo-label format="MMM d" />
      </dxo-argument-axis>
      <dxo-value-axis [tickInterval]="5">
        <dxo-title text="Story points"></dxo-title>
      </dxo-value-axis>
      <dxo-legend verticalAlignment="bottom" horizontalAlignment="center" />
      <dxo-tooltip [enabled]="true" />
    </dx-chart>

    <div class="empty-state" *ngIf="!loading && chartData.length === 0 && sprintId">
      No burndown snapshots for this sprint yet — click <strong>Snapshot now</strong> to create the first one.
    </div>
    <div class="empty-state" *ngIf="!sprintId">
      Pick a project + sprint to view its burndown.
    </div>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .sprint-meta { display: flex; gap: 16px; padding: 8px 12px; background: rgba(59,130,246,0.05);
                   border: 1px solid rgba(59,130,246,0.2); border-radius: 6px; margin-bottom: 12px;
                   color: #9ca3af; font-size: 13px; }
    .sprint-meta strong { color: #f9fafb; }
    .sprint-meta .goal { font-style: italic; }
    dx-chart { height: 420px; display: block; }
    .empty-state { text-align: center; color: #6b7280; padding: 48px; font-size: 13px; }
  `]
})
export class BurndownComponent implements OnInit {
  projects: Project[] = [];
  sprints: Sprint[] = [];
  projectId?: number;
  sprintId?: number;
  selectedSprint: Sprint | null = null;

  chartData: ChartPoint[] = [];
  loading = false;

  constructor(private taskService: TaskService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    // Deep-link from sprint board: /tasks/burndown?project=1&sprint=4
    const qp = this.route.snapshot.queryParamMap;
    this.projectId = Number(qp.get('project')) || undefined;
    this.sprintId  = Number(qp.get('sprint'))  || undefined;

    this.taskService.getProjects().subscribe(p => {
      this.projects = p;
      if (!this.projectId && p.length > 0) this.projectId = p[0].id;
      if (this.projectId) this.loadSprints();
    });
  }

  onProjectChanged(e: any): void {
    this.projectId = e.value;
    this.sprintId = undefined;
    this.selectedSprint = null;
    this.chartData = [];
    this.loadSprints();
  }

  onSprintChanged(e: any): void {
    this.sprintId = e.value;
    this.selectedSprint = this.sprints.find(s => s.id === e.value) ?? null;
    this.reload();
  }

  private loadSprints(): void {
    if (!this.projectId) return;
    this.taskService.getSprints(this.projectId).subscribe({
      next: s => {
        this.sprints = s;
        if (this.sprintId) {
          this.selectedSprint = s.find(x => x.id === this.sprintId) ?? null;
          if (this.selectedSprint) this.reload();
        }
      },
      error: () => notify('Failed to load sprints', 'error', 3000)
    });
  }

  reload = (): void => {
    if (!this.projectId || !this.sprintId) return;
    this.loading = true;
    this.taskService.getBurndown(this.projectId, this.sprintId).subscribe({
      next: pts => { this.chartData = this.buildChart(pts); this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load burndown', 'error', 3000); }
    });
  };

  snapshot = (): void => {
    if (!this.projectId || !this.sprintId) return;
    this.taskService.snapshotBurndown(this.projectId, this.sprintId).subscribe({
      next: () => { notify('Snapshot taken', 'success', 1500); this.reload(); },
      error: () => notify('Snapshot failed', 'error', 3000)
    });
  };

  /**
   * Build the ideal line linearly between snapshot[0] (= total points) on
   * day 1 and 0 on the sprint's end_date. If end_date isn't set we fall
   * back to the last snapshot date.
   */
  private buildChart(pts: BurndownPoint[]): ChartPoint[] {
    if (pts.length === 0) return [];

    const total = Number(pts[0].points_remaining) + Number(pts[0].points_completed);
    const startDate = new Date(pts[0].snapshot_date);
    const endDate   = this.selectedSprint?.end_date
      ? new Date(this.selectedSprint.end_date)
      : new Date(pts[pts.length - 1].snapshot_date);

    const totalDays = Math.max(1, Math.ceil((endDate.getTime() - startDate.getTime()) / 86_400_000));

    return pts.map(p => {
      const d = new Date(p.snapshot_date);
      const dayIdx = Math.min(totalDays, Math.max(0,
        Math.round((d.getTime() - startDate.getTime()) / 86_400_000)));
      const idealRemaining = total - (total * dayIdx / totalDays);
      return {
        date:            p.snapshot_date,
        pointsRemaining: Number(p.points_remaining),
        pointsCompleted: Number(p.points_completed),
        ideal:           Math.max(0, Number(idealRemaining.toFixed(1))),
      };
    });
  }
}
