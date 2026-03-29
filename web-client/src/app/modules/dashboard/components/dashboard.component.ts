import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxChartModule, DxPieChartModule } from 'devextreme-angular';
import { TaskService, Task } from '../../../core/services/task.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, DxChartModule, DxPieChartModule],
  template: `
    <div class="dashboard-grid">
      <!-- KPI Cards -->
      <div class="kpi-row">
        <div class="kpi-card">
          <div class="kpi-value">{{ totalTasks }}</div>
          <div class="kpi-label">Total Tasks</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-value" style="color:#22c55e">{{ doneTasks }}</div>
          <div class="kpi-label">Completed</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-value" style="color:#3b82f6">{{ inProgressTasks }}</div>
          <div class="kpi-label">In Progress</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-value" style="color:#ef4444">{{ blockedTasks }}</div>
          <div class="kpi-label">Blocked</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-value" style="color:#f59e0b">{{ totalPoints }}</div>
          <div class="kpi-label">Total Points</div>
        </div>
      </div>

      <!-- Charts -->
      <div class="chart-row">
        <dx-pie-chart [dataSource]="statusData" title="Tasks by Status"
                       [palette]="['#9ca3af','#3b82f6','#f59e0b','#22c55e','#ef4444']"
                       class="chart-card">
          <dxi-series argumentField="status" valueField="count">
            <dxo-label [visible]="true" [customizeText]="labelText"></dxo-label>
          </dxi-series>
          <dxo-legend [visible]="true" horizontalAlignment="right" verticalAlignment="top" />
        </dx-pie-chart>

        <dx-chart [dataSource]="typeData" title="Points by Type" class="chart-card">
          <dxi-series argumentField="type" valueField="points" type="bar" color="#3b82f6" />
          <dxo-argument-axis><dxo-label [rotationAngle]="45" /></dxo-argument-axis>
        </dx-chart>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-grid { display: flex; flex-direction: column; gap: 16px; }
    .kpi-row { display: flex; gap: 12px; flex-wrap: wrap; }
    .kpi-card { background: #1e293b; border-radius: 8px; padding: 20px 24px; flex: 1; min-width: 140px; }
    .kpi-value { font-size: 28px; font-weight: 700; color: #f9fafb; }
    .kpi-label { font-size: 12px; color: #9ca3af; margin-top: 4px; }
    .chart-row { display: flex; gap: 16px; flex-wrap: wrap; }
    .chart-card { flex: 1; min-width: 400px; background: #1e293b; border-radius: 8px; padding: 16px; }
  `]
})
export class DashboardComponent implements OnInit {
  tasks: Task[] = [];
  totalTasks = 0;
  doneTasks = 0;
  inProgressTasks = 0;
  blockedTasks = 0;
  totalPoints = 0;
  statusData: any[] = [];
  typeData: any[] = [];

  constructor(private taskService: TaskService) {}

  ngOnInit(): void {
    this.taskService.getTasks(undefined, undefined, 1000).subscribe(tasks => {
      this.tasks = tasks;
      this.totalTasks = tasks.length;
      this.doneTasks = tasks.filter(t => t.status === 'Done').length;
      this.inProgressTasks = tasks.filter(t => t.status === 'InProgress').length;
      this.blockedTasks = tasks.filter(t => t.status === 'Blocked').length;
      this.totalPoints = tasks.reduce((sum, t) => sum + (t.points || 0), 0);

      this.statusData = ['Open', 'InProgress', 'Review', 'Done', 'Blocked']
        .map(s => ({ status: s, count: tasks.filter(t => t.status === s).length }))
        .filter(d => d.count > 0);

      const types = ['Epic', 'Story', 'Task', 'Bug', 'SubTask'];
      this.typeData = types.map(tp => ({
        type: tp,
        points: tasks.filter(t => t.task_type === tp).reduce((s, t) => s + (t.points || 0), 0)
      })).filter(d => d.points > 0);
    });
  }

  labelText = (info: any) => `${info.argumentText}: ${info.valueText}`;
}
