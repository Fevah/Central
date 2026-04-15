import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

/**
 * Shared sub-nav for the Tasks module so the user can jump between the
 * task tree, kanban, sprints, burndown, gantt, timesheet, and activity
 * without going back to the sidebar.
 *
 * Mirrors the WPF Tasks ribbon panel grouping. The sidebar still lists
 * "Tasks" + "Kanban" as headline entries; everything else is reachable
 * from any of the task pages via this strip.
 */
@Component({
  selector: 'app-tasks-sub-nav',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="sub-nav">
      <a routerLink="/tasks"             routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Tree</a>
      <a routerLink="/kanban"            routerLinkActive="active">Kanban</a>
      <a routerLink="/tasks/sprints"     routerLinkActive="active">Sprints</a>
      <a routerLink="/tasks/burndown"    routerLinkActive="active">Burndown</a>
      <a routerLink="/tasks/gantt"       routerLinkActive="active">Gantt</a>
      <a routerLink="/tasks/timesheet"   routerLinkActive="active">Timesheet</a>
      <a routerLink="/tasks/activity"    routerLinkActive="active">Activity</a>
    </div>
  `,
  styles: [`
    .sub-nav { display: flex; gap: 4px; margin-bottom: 12px; border-bottom: 1px solid #1f2937; padding-bottom: 8px; flex-wrap: wrap; }
    .sub-nav a { color: #9ca3af; text-decoration: none; padding: 6px 12px; border-radius: 6px; font-size: 13px; }
    .sub-nav a:hover { background: rgba(59,130,246,0.1); color: #d1d5db; }
    .sub-nav a.active { background: rgba(59,130,246,0.2); color: #60a5fa; }
  `]
})
export class TasksSubNavComponent {}
