import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxDataGridModule, DxToolbarModule, DxPieChartModule } from 'devextreme-angular';
import { ServiceDeskService, SdRequest } from '../../../core/services/servicedesk.service';
import notify from 'devextreme/ui/notify';

@Component({
  selector: 'app-servicedesk',
  standalone: true,
  imports: [CommonModule, DxDataGridModule, DxToolbarModule, DxPieChartModule],
  template: `
    <!-- Summary -->
    <div class="stat-cards">
      <div class="stat-card open"><div class="stat-value">{{ openCount }}</div><div class="stat-label">Open</div></div>
      <div class="stat-card progress"><div class="stat-value">{{ progressCount }}</div><div class="stat-label">In Progress</div></div>
      <div class="stat-card resolved"><div class="stat-value">{{ resolvedCount }}</div><div class="stat-label">Resolved</div></div>
      <div class="stat-card total"><div class="stat-value">{{ requests.length }}</div><div class="stat-label">Total</div></div>
    </div>

    <div class="grid-chart-row">
      <!-- Request Grid -->
      <div class="grid-section">
        <dx-toolbar class="section-toolbar">
          <dxi-item location="before"><div class="section-title">Requests</div></dxi-item>
          <dxi-item location="after" widget="dxButton"
                    [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }"></dxi-item>
        </dx-toolbar>

        <dx-data-grid [dataSource]="requests" [showBorders]="true" [rowAlternationEnabled]="true"
                       [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                       [searchPanel]="{ visible: true }" [headerFilter]="{ visible: true }"
                       [groupPanel]="{ visible: true }" height="calc(100vh - 280px)">
          <dxo-summary>
            <dxi-total-item column="subject" summaryType="count" displayFormat="Tickets: {0}" />
          </dxo-summary>
          <dxi-column dataField="display_id" caption="ID" width="80" sortOrder="desc" />
          <dxi-column dataField="subject" caption="Subject" width="280" />
          <dxi-column dataField="status" caption="Status" width="100" cellTemplate="statusTemplate" />
          <dxi-column dataField="priority" caption="Priority" width="90" cellTemplate="priorityTemplate" />
          <dxi-column dataField="requester_name" caption="Requester" width="120" />
          <dxi-column dataField="technician_name" caption="Technician" width="120" />
          <dxi-column dataField="group_name" caption="Group" width="100" />
          <dxi-column dataField="category" caption="Category" width="100" />
          <dxi-column dataField="created_at" caption="Created" width="140" dataType="datetime" />
          <dxi-column dataField="resolved_at" caption="Resolved" width="140" dataType="datetime" />

          <div *dxTemplate="let d of 'statusTemplate'">
            <span [class]="'badge badge-' + (d.value || '').toLowerCase().replace(' ', '-')">{{ d.value }}</span>
          </div>
          <div *dxTemplate="let d of 'priorityTemplate'">
            <span [class]="'badge priority-' + (d.value || '').toLowerCase()">{{ d.value }}</span>
          </div>
        </dx-data-grid>
      </div>

      <!-- Priority chart -->
      <div class="chart-section">
        <div class="section-title" style="margin-bottom: 8px;">By Priority</div>
        <dx-pie-chart [dataSource]="priorityData" [palette]="['#ef4444','#f97316','#3b82f6','#22c55e']"
                       [size]="{ height: 200 }">
          <dxi-series argumentField="priority" valueField="count">
            <dxo-label [visible]="true" [connector]="{ visible: true }" format="fixedPoint" />
          </dxi-series>
          <dxo-legend [visible]="true" horizontalAlignment="center" verticalAlignment="bottom" />
        </dx-pie-chart>
      </div>
    </div>
  `,
  styles: [`
    .stat-cards { display: flex; gap: 12px; margin-bottom: 16px; }
    .stat-card { flex: 1; padding: 16px; border-radius: 8px; text-align: center; }
    .stat-card.open { background: rgba(59,130,246,0.1); border: 1px solid rgba(59,130,246,0.3); }
    .stat-card.progress { background: rgba(249,115,22,0.1); border: 1px solid rgba(249,115,22,0.3); }
    .stat-card.resolved { background: rgba(34,197,94,0.1); border: 1px solid rgba(34,197,94,0.3); }
    .stat-card.total { background: rgba(139,92,246,0.1); border: 1px solid rgba(139,92,246,0.3); }
    .stat-value { font-size: 28px; font-weight: bold; }
    .open .stat-value { color: #3b82f6; } .progress .stat-value { color: #f97316; }
    .resolved .stat-value { color: #22c55e; } .total .stat-value { color: #8b5cf6; }
    .stat-label { font-size: 11px; color: #9ca3af; text-transform: uppercase; letter-spacing: 1px; }
    .grid-chart-row { display: flex; gap: 16px; }
    .grid-section { flex: 3; } .chart-section { flex: 1; }
    .section-toolbar { margin-bottom: 8px; }
    .section-title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-open { background: rgba(59,130,246,0.2); color: #3b82f6; }
    .badge-in-progress { background: rgba(249,115,22,0.2); color: #f97316; }
    .badge-resolved { background: rgba(34,197,94,0.2); color: #22c55e; }
    .badge-closed { background: rgba(107,114,128,0.2); color: #6b7280; }
    .priority-critical, .priority-urgent { background: rgba(239,68,68,0.2); color: #ef4444; }
    .priority-high { background: rgba(249,115,22,0.2); color: #f97316; }
    .priority-medium, .priority-normal { background: rgba(59,130,246,0.2); color: #3b82f6; }
    .priority-low { background: rgba(34,197,94,0.2); color: #22c55e; }
  `]
})
export class ServiceDeskComponent implements OnInit {
  requests: SdRequest[] = [];

  get openCount(): number { return this.requests.filter(r => r.status?.toLowerCase() === 'open').length; }
  get progressCount(): number { return this.requests.filter(r => r.status?.toLowerCase().includes('progress')).length; }
  get resolvedCount(): number { return this.requests.filter(r => r.status?.toLowerCase() === 'resolved').length; }

  get priorityData(): { priority: string; count: number }[] {
    const counts: Record<string, number> = {};
    this.requests.forEach(r => { counts[r.priority] = (counts[r.priority] || 0) + 1; });
    return Object.entries(counts).map(([priority, count]) => ({ priority, count }));
  }

  constructor(private sdService: ServiceDeskService) {}

  ngOnInit(): void { this.loadRequests(); }

  loadRequests(): void {
    this.sdService.getRequests().subscribe({
      next: r => this.requests = r,
      error: () => notify('Failed to load tickets', 'error', 3000)
    });
  }

  refresh = (): void => { this.loadRequests(); notify('Refreshed', 'info', 1000); };
}
