import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxSortableModule, DxScrollViewModule } from 'devextreme-angular';
import { TaskService, Task } from '../../../core/services/task.service';
import { TasksSubNavComponent } from './tasks-sub-nav.component';

interface KanbanColumn {
  name: string;
  status: string;
  tasks: Task[];
  color: string;
}

@Component({
  selector: 'app-kanban-board',
  standalone: true,
  imports: [CommonModule, TasksSubNavComponent, DxSortableModule, DxScrollViewModule],
  template: `
    <app-tasks-sub-nav></app-tasks-sub-nav>
    <div class="kanban-board">
      <div class="kanban-column" *ngFor="let col of columns">
        <div class="column-header" [style.border-top-color]="col.color">
          <span class="column-title">{{ col.name }}</span>
          <span class="column-count">{{ col.tasks.length }}</span>
        </div>
        <dx-scroll-view class="column-body">
          <dx-sortable group="tasks" [data]="col.tasks"
                        (onReorder)="onReorder($event, col)"
                        (onAdd)="onAdd($event, col)"
                        (onRemove)="onRemove($event, col)">
            <div *ngFor="let task of col.tasks" class="kanban-card">
              <div class="card-color" [style.background]="task.color || '#3b82f6'"></div>
              <div class="card-body">
                <div class="card-title">{{ task.title }}</div>
                <div class="card-meta">
                  <span class="card-type">{{ task.task_type }}</span>
                  <span class="card-points" *ngIf="task.points">{{ task.points }}pts</span>
                </div>
                <div class="card-assignee" *ngIf="task.assigned_name">{{ task.assigned_name }}</div>
              </div>
            </div>
          </dx-sortable>
        </dx-scroll-view>
      </div>
    </div>
  `,
  styles: [`
    .kanban-board { display: flex; gap: 12px; height: calc(100vh - 130px); overflow-x: auto; }
    .kanban-column { min-width: 260px; max-width: 280px; background: #111827; border-radius: 8px; display: flex; flex-direction: column; }
    .column-header { padding: 12px 16px; border-top: 3px solid; display: flex; justify-content: space-between; align-items: center; }
    .column-title { color: #f9fafb; font-weight: 600; font-size: 14px; }
    .column-count { background: #374151; color: #9ca3af; border-radius: 10px; padding: 2px 8px; font-size: 12px; }
    .column-body { flex: 1; padding: 8px; }
    .kanban-card { background: #1e293b; border-radius: 6px; margin-bottom: 8px; display: flex; overflow: hidden; cursor: grab; }
    .kanban-card:hover { background: #263045; }
    .card-color { width: 4px; flex-shrink: 0; }
    .card-body { padding: 10px 12px; flex: 1; }
    .card-title { color: #f9fafb; font-size: 13px; font-weight: 500; margin-bottom: 6px; }
    .card-meta { display: flex; gap: 8px; align-items: center; }
    .card-type { background: #374151; color: #9ca3af; padding: 1px 6px; border-radius: 3px; font-size: 11px; }
    .card-points { color: #60a5fa; font-size: 11px; }
    .card-assignee { color: #6b7280; font-size: 11px; margin-top: 4px; }
  `]
})
export class KanbanBoardComponent implements OnInit {
  columns: KanbanColumn[] = [
    { name: 'Backlog', status: 'Open', tasks: [], color: '#6b7280' },
    { name: 'To Do', status: 'Open', tasks: [], color: '#3b82f6' },
    { name: 'In Progress', status: 'InProgress', tasks: [], color: '#f59e0b' },
    { name: 'Review', status: 'Review', tasks: [], color: '#8b5cf6' },
    { name: 'Done', status: 'Done', tasks: [], color: '#22c55e' },
  ];

  constructor(private taskService: TaskService) {}

  ngOnInit(): void {
    this.taskService.getTasks().subscribe(tasks => {
      for (const col of this.columns) {
        col.tasks = tasks.filter(t =>
          (t.board_column === col.name) ||
          (!t.board_column && t.status === col.status)
        );
      }
    });
  }

  onReorder(e: any, col: KanbanColumn): void {
    const task = col.tasks.splice(e.fromIndex, 1)[0];
    col.tasks.splice(e.toIndex, 0, task);
  }

  onAdd(e: any, col: KanbanColumn): void {
    const task = e.itemData as Task;
    col.tasks.splice(e.toIndex, 0, task);
    // Update task status via API
    this.taskService.updateTask(task.id, { status: col.status } as any).subscribe();
  }

  onRemove(e: any, col: KanbanColumn): void {
    col.tasks.splice(e.fromIndex, 1);
  }
}
