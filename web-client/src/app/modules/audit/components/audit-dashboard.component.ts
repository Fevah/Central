import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxDataGridModule, DxToolbarModule, DxChartModule, DxPieChartModule, DxPopupModule, DxFormModule } from 'devextreme-angular';
import { AuditService, Investigation, Finding, GdprScore, GdprArticle } from '../../../core/services/audit.service';
import notify from 'devextreme/ui/notify';

@Component({
  selector: 'app-audit-dashboard',
  standalone: true,
  imports: [CommonModule, DxDataGridModule, DxToolbarModule, DxChartModule, DxPieChartModule, DxPopupModule, DxFormModule],
  template: `
    <!-- GDPR Score Cards -->
    <div class="score-row">
      <div class="score-card grade" *ngIf="gdprScore">
        <div class="score-value grade-{{gdprScore.overall_grade.toLowerCase()}}">{{gdprScore.overall_grade}}</div>
        <div class="score-label">GDPR Grade</div>
        <div class="score-sub">{{gdprScore.overall_score}}%</div>
      </div>
      <div class="score-card"><div class="score-value ok">{{gdprScore?.compliant_count || 0}}</div><div class="score-label">Compliant</div></div>
      <div class="score-card"><div class="score-value warn">{{gdprScore?.partial_count || 0}}</div><div class="score-label">Partial</div></div>
      <div class="score-card"><div class="score-value err">{{gdprScore?.non_compliant_count || 0}}</div><div class="score-label">Non-Compliant</div></div>
    </div>

    <!-- GDPR Articles Chart — DxDataGrid fallback (avoids DxChart nested element complexity) -->
    <dx-data-grid [dataSource]="gdprArticles" [showBorders]="true" [rowAlternationEnabled]="true"
                   [columnAutoWidth]="true" height="200" style="margin-bottom: 16px;">
      <dxi-column dataField="article" caption="Article" width="80" />
      <dxi-column dataField="title" caption="Title" />
      <dxi-column dataField="score" caption="Score %" width="90" dataType="number" />
      <dxi-column dataField="status" caption="Status" width="120" cellTemplate="gdprStatusTpl" />
      <div *dxTemplate="let d of 'gdprStatusTpl'">
        <span [class]="'badge gdpr-' + d.value">{{d.value}}</span>
      </div>
    </dx-data-grid>

    <!-- Investigations Grid -->
    <dx-toolbar class="section-toolbar">
      <dxi-item location="before"><div class="section-title">Investigations</div></dxi-item>
      <dxi-item location="before" widget="dxButton"
                [options]="{ text: '+ New Investigation', type: 'default', stylingMode: 'contained', onClick: openNewInv }"></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', onClick: refresh }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="investigations" [showBorders]="true" [rowAlternationEnabled]="true"
                   [columnAutoWidth]="true" [filterRow]="{ visible: true }" [searchPanel]="{ visible: true }"
                   (onRowClick)="onInvClick($event)" height="300">
      <dxi-column dataField="title" caption="Investigation" width="250" />
      <dxi-column dataField="target_user" caption="Target User" width="180" />
      <dxi-column dataField="status" caption="Status" width="90" cellTemplate="statusTpl" />
      <dxi-column dataField="created_by" caption="Created By" width="120" />
      <dxi-column dataField="created_at" caption="Created" width="140" dataType="datetime" sortOrder="desc" />

      <div *dxTemplate="let d of 'statusTpl'">
        <span [class]="'badge badge-' + d.value">{{d.value}}</span>
      </div>
    </dx-data-grid>

    <!-- Findings for selected investigation -->
    <div *ngIf="selectedFindings.length > 0" style="margin-top: 16px;">
      <div class="section-title">Findings ({{selectedFindings.length}})</div>
      <dx-data-grid [dataSource]="selectedFindings" [showBorders]="true" [rowAlternationEnabled]="true"
                     [columnAutoWidth]="true" height="250">
        <dxi-column dataField="severity" caption="Severity" width="80" cellTemplate="sevTpl" />
        <dxi-column dataField="title" caption="Finding" width="300" />
        <dxi-column dataField="finding_type" caption="Type" width="140" />
        <dxi-column dataField="detail" caption="Detail" />

        <div *dxTemplate="let d of 'sevTpl'">
          <span [class]="'badge sev-' + d.value">{{d.value}}</span>
        </div>
      </dx-data-grid>
    </div>

    <!-- New Investigation Dialog -->
    <dx-popup [visible]="showNewInv" [dragEnabled]="true" [showCloseButton]="true"
              title="New Investigation" [width]="480" [height]="340" (onHidden)="showNewInv = false">
      <dx-form [(formData)]="newInv" [colCount]="1" labelLocation="top">
        <dxi-item dataField="title" [editorOptions]="{ placeholder: 'Investigation title...' }">
          <dxi-validation-rule type="required" />
        </dxi-item>
        <dxi-item dataField="target_user" [editorOptions]="{ placeholder: 'user@org.com' }" />
        <dxi-item dataField="description" editorType="dxTextArea" [editorOptions]="{ height: 80 }" />
        <dxi-item itemType="button" [buttonOptions]="{ text: 'Start Investigation', type: 'default', width: '100%', onClick: submitInv }" />
      </dx-form>
    </dx-popup>
  `,
  styles: [`
    .score-row { display: flex; gap: 12px; margin-bottom: 16px; }
    .score-card { flex: 1; padding: 16px; border-radius: 8px; text-align: center; background: rgba(255,255,255,0.03); border: 1px solid #1f2937; }
    .score-card.grade { background: rgba(59,130,246,0.05); border-color: rgba(59,130,246,0.3); }
    .score-value { font-size: 32px; font-weight: bold; }
    .score-label { font-size: 11px; color: #9ca3af; text-transform: uppercase; letter-spacing: 1px; }
    .score-sub { font-size: 14px; color: #6b7280; }
    .grade-a { color: #22c55e; } .grade-b { color: #3b82f6; } .grade-c { color: #eab308; } .grade-d { color: #ef4444; }
    .ok { color: #22c55e; } .warn { color: #eab308; } .err { color: #ef4444; }
    .section-toolbar { margin-bottom: 8px; }
    .section-title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-open { background: rgba(59,130,246,0.2); color: #3b82f6; }
    .badge-closed { background: rgba(107,114,128,0.2); color: #6b7280; }
    .sev-critical { background: rgba(239,68,68,0.2); color: #ef4444; }
    .sev-high { background: rgba(249,115,22,0.2); color: #f97316; }
    .sev-medium { background: rgba(234,179,8,0.2); color: #eab308; }
    .sev-low { background: rgba(34,197,94,0.2); color: #22c55e; }
  `]
})
export class AuditDashboardComponent implements OnInit {
  investigations: Investigation[] = [];
  selectedFindings: Finding[] = [];
  gdprScore: GdprScore | null = null;
  gdprArticles: GdprArticle[] = [];
  showNewInv = false;
  newInv: any = {};

  constructor(private auditService: AuditService) {}

  ngOnInit(): void { this.refresh(); this.loadGdpr(); }

  refresh = (): void => {
    this.auditService.getInvestigations().subscribe({
      next: inv => this.investigations = inv,
      error: () => notify('Failed to load investigations', 'error', 3000)
    });
  };

  loadGdpr(): void {
    this.auditService.getGdprScore().subscribe({ next: s => this.gdprScore = s });
    this.auditService.getGdprArticles().subscribe({ next: a => this.gdprArticles = a });
  }

  onInvClick(e: any): void {
    const inv = e.data as Investigation;
    this.auditService.getInvestigation(inv.id).subscribe({
      next: res => this.selectedFindings = res.findings,
      error: () => notify('Failed to load findings', 'error', 3000)
    });
  }

  openNewInv = (): void => { this.newInv = { title: '', target_user: '', description: '' }; this.showNewInv = true; };

  submitInv = (): void => {
    if (!this.newInv.title?.trim()) { notify('Title required', 'warning', 2000); return; }
    this.auditService.createInvestigation(this.newInv).subscribe({
      next: res => { notify(`Investigation created — ${res.findings} findings detected`, 'success', 4000); this.showNewInv = false; this.refresh(); },
      error: () => notify('Failed to create investigation', 'error', 3000)
    });
  };
}
