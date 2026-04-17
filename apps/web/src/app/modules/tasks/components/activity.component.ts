import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxDataGridModule, DxToolbarModule, DxButtonModule } from 'devextreme-angular';
import { Subject, Subscription } from 'rxjs';
import { auditTime } from 'rxjs/operators';
import notify from 'devextreme/ui/notify';
import { TaskService } from '../../../core/services/task.service';
import { SseService } from '../../../core/services/sse.service';
import { TasksSubNavComponent } from './tasks-sub-nav.component';

interface ActivityRow {
  time:   string | Date;
  source: string;
  action: string;
  entity: string;
  name:   string;
  user:   string;
}

/**
 * Activity feed — recent platform-wide events from /api/activity/global.
 *
 * Auto-refreshes when the SSE task stream emits anything (debounced), so
 * a teammate creating/editing a task surfaces here within a second.
 */
@Component({
  selector: 'app-activity',
  standalone: true,
  imports: [CommonModule, TasksSubNavComponent, DxDataGridModule, DxToolbarModule, DxButtonModule],
  template: `
    <app-tasks-sub-nav></app-tasks-sub-nav>

    <dx-toolbar class="page-toolbar">
      <dxi-item location="before"><div class="title">Recent activity</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: reload }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [rowAlternationEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  [searchPanel]="{ visible: true }"
                  height="calc(100vh - 220px)">
      <dxi-column dataField="time" caption="When" dataType="datetime" width="180" sortOrder="desc" />
      <dxi-column dataField="source" caption="Source" width="100" cellTemplate="sourceTpl" />
      <dxi-column dataField="action" caption="Action" width="140" />
      <dxi-column dataField="entity" caption="Entity" width="120" />
      <dxi-column dataField="name" caption="Name" />
      <dxi-column dataField="user" caption="User" width="180" />

      <div *dxTemplate="let d of 'sourceTpl'">
        <span [class]="'pill pill-' + (d.value || 'unknown').toLowerCase()">{{ d.value }}</span>
      </div>
    </dx-data-grid>

    <div class="empty-state" *ngIf="!loading && rows.length === 0">
      No recent activity recorded.
    </div>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .empty-state { text-align: center; color: #6b7280; padding: 32px; font-size: 13px; }
    .pill { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .pill-audit { background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .pill-auth  { background: rgba(34,197,94,0.2);   color: #22c55e; }
    .pill-task  { background: rgba(168,85,247,0.2);  color: #c084fc; }
  `]
})
export class ActivityComponent implements OnInit, OnDestroy {
  rows: ActivityRow[] = [];
  loading = false;

  private readonly reload$ = new Subject<void>();
  private rtSub?: Subscription;

  constructor(private taskService: TaskService, private sse: SseService) {}

  ngOnInit(): void {
    this.reload();

    // Debounced reload on any task SSE event — keeps the feed fresh
    // without hammering the server during bursts.
    this.rtSub = this.reload$.pipe(auditTime(1500)).subscribe(() => this.reload());
    this.rtSub.add(
      this.sse.events$.subscribe(() => this.reload$.next())
    );
  }

  ngOnDestroy(): void { this.rtSub?.unsubscribe(); }

  reload = (): void => {
    this.loading = true;
    this.taskService.getGlobalActivity(200).subscribe({
      next: r => { this.rows = r; this.loading = false; },
      error: () => { this.loading = false; notify('Failed to load activity', 'error', 3000); }
    });
  };
}
