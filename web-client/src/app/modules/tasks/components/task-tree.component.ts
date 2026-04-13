import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  DxTreeListModule, DxSelectBoxModule, DxToolbarModule,
  DxPopupModule, DxFormModule, DxTreeListComponent
} from 'devextreme-angular';
import { DxoSummaryModule, DxiTotalItemModule } from 'devextreme-angular/ui/nested';
import { TaskService, Task, Project } from '../../../core/services/task.service';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';

@Component({
  selector: 'app-task-tree',
  standalone: true,
  imports: [
    CommonModule, DxTreeListModule, DxSelectBoxModule, DxToolbarModule,
    DxPopupModule, DxFormModule, DxoSummaryModule, DxiTotalItemModule
  ],
  template: `
    <dx-toolbar class="task-toolbar">
      <dxi-item location="before">
        <dx-select-box [items]="projects" displayExpr="name" valueExpr="id"
                        placeholder="All Projects" [showClearButton]="true"
                        (onValueChanged)="onProjectChanged($event)" width="200">
        </dx-select-box>
      </dxi-item>
      <dxi-item location="before" widget="dxButton"
                [options]="{ text: '+ New Task', type: 'default', stylingMode: 'contained', onClick: openNewTask }">
      </dxi-item>
      <dxi-item location="before" widget="dxButton"
                [options]="{ text: 'Delete', type: 'danger', stylingMode: 'outlined', onClick: deleteSelected }">
      </dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }">
      </dxi-item>
    </dx-toolbar>

    <dx-tree-list #treeList [dataSource]="tasks" keyExpr="id" parentIdExpr="parent_id"
                   [columnAutoWidth]="true" [showBorders]="true" [showRowLines]="true"
                   [autoExpandAll]="true" [allowColumnReordering]="true"
                   [filterRow]="{ visible: true }" [searchPanel]="{ visible: true }"
                   [headerFilter]="{ visible: true }" [rowAlternationEnabled]="true"
                   [selection]="{ mode: 'single' }"
                   [editing]="{ mode: 'cell', allowUpdating: true }"
                   (onRowUpdated)="onRowUpdated($event)"
                   height="calc(100vh - 160px)">
      <dxo-summary>
        <dxi-total-item column="title" summaryType="count" displayFormat="Tasks: {0}" />
        <dxi-total-item column="points" summaryType="sum" displayFormat="Points: {0}" />
      </dxo-summary>
      <dxi-column dataField="id" caption="ID" width="60" [allowEditing]="false" sortOrder="asc" />
      <dxi-column dataField="title" caption="Task" width="280" />
      <dxi-column dataField="status" caption="Status" width="100" [lookup]="{ dataSource: statuses }" />
      <dxi-column dataField="priority" caption="Priority" width="90" [lookup]="{ dataSource: priorities }" />
      <dxi-column dataField="task_type" caption="Type" width="90" [lookup]="{ dataSource: taskTypes }" />
      <dxi-column dataField="points" caption="Pts" width="60" dataType="number" />
      <dxi-column dataField="work_remaining" caption="Rem" width="60" dataType="number" />
      <dxi-column dataField="assigned_name" caption="Assigned" width="120" [allowEditing]="false" />
      <dxi-column dataField="sprint_name" caption="Sprint" width="100" [allowEditing]="false" />
      <dxi-column dataField="category" caption="Category" width="90" />
      <dxi-column dataField="severity" caption="Severity" width="80" [lookup]="{ dataSource: severities }" />
      <dxi-column dataField="risk" caption="Risk" width="70" [lookup]="{ dataSource: risks }" />
      <dxi-column dataField="start_date" caption="Start" width="100" dataType="date" />
      <dxi-column dataField="finish_date" caption="Finish" width="100" dataType="date" />
      <dxi-column dataField="due_date" caption="Due" width="100" dataType="date" />
      <dxi-column dataField="building" caption="Building" width="90" />
      <dxi-column dataField="tags" caption="Tags" width="120" />
    </dx-tree-list>

    <!-- New Task Dialog -->
    <dx-popup [visible]="showNewTask" [dragEnabled]="true" [showCloseButton]="true"
              title="New Task" [width]="480" [height]="540" (onHidden)="showNewTask = false">
      <dx-form [(formData)]="newTask" [colCount]="1" labelLocation="top">
        <dxi-item dataField="title" [editorOptions]="{ placeholder: 'Enter task title...' }">
          <dxi-validation-rule type="required" message="Title is required" />
        </dxi-item>
        <dxi-item dataField="status" editorType="dxSelectBox"
                  [editorOptions]="{ items: statuses, value: 'Open' }" />
        <dxi-item dataField="priority" editorType="dxSelectBox"
                  [editorOptions]="{ items: priorities, value: 'Medium' }" />
        <dxi-item dataField="task_type" editorType="dxSelectBox"
                  [editorOptions]="{ items: taskTypes, value: 'Task' }" />
        <dxi-item dataField="parent_id" editorType="dxSelectBox"
                  [editorOptions]="{ items: tasks, displayExpr: 'title', valueExpr: 'id',
                                     placeholder: 'No parent (top-level)', showClearButton: true }" />
        <dxi-item dataField="points" editorType="dxNumberBox"
                  [editorOptions]="{ min: 0, placeholder: '0' }" />
        <dxi-item dataField="due_date" editorType="dxDateBox"
                  [editorOptions]="{ type: 'date', placeholder: 'Due date' }" />
        <dxi-item dataField="category" [editorOptions]="{ placeholder: 'Category' }" />
        <dxi-item dataField="tags" [editorOptions]="{ placeholder: 'Tags (comma separated)' }" />
        <dxi-item itemType="button" [buttonOptions]="{ text: 'Create Task', type: 'default',
                   width: '100%', useSubmitBehavior: false, onClick: submitNewTask }" />
      </dx-form>
    </dx-popup>
  `,
  styles: [`.task-toolbar { margin-bottom: 8px; }`]
})
export class TaskTreeComponent implements OnInit {
  @ViewChild('treeList') treeList!: DxTreeListComponent;
  tasks: Task[] = [];
  projects: Project[] = [];
  selectedProjectId?: number;
  showNewTask = false;
  newTask: Partial<Task> = {};

  statuses = ['Open', 'InProgress', 'Review', 'Done', 'Blocked', 'Cancelled'];
  priorities = ['Critical', 'High', 'Medium', 'Low'];
  taskTypes = ['Epic', 'Story', 'Task', 'Bug', 'SubTask', 'Milestone'];
  severities = ['Critical', 'Major', 'Minor', 'Trivial'];
  risks = ['High', 'Medium', 'Low'];

  constructor(private taskService: TaskService) {}

  ngOnInit(): void { this.loadProjects(); this.loadTasks(); }

  loadProjects(): void {
    this.taskService.getProjects().subscribe({
      next: p => this.projects = p,
      error: () => notify('Failed to load projects', 'error', 3000)
    });
  }

  loadTasks(): void {
    this.taskService.getTasks(this.selectedProjectId).subscribe({
      next: t => this.tasks = t,
      error: () => notify('Failed to load tasks', 'error', 3000)
    });
  }

  onProjectChanged(e: any): void { this.selectedProjectId = e.value; this.loadTasks(); }

  onRowUpdated(e: any): void {
    this.taskService.updateTask(e.key, e.data).subscribe({
      next: () => notify('Task updated', 'success', 2000),
      error: () => notify('Update failed', 'error', 3000)
    });
  }

  openNewTask = (): void => {
    this.newTask = { title: '', status: 'Open', priority: 'Medium', task_type: 'Task',
                     parent_id: null, points: null, due_date: null, category: null, tags: null };
    this.showNewTask = true;
  };

  submitNewTask = (): void => {
    if (!this.newTask.title?.trim()) { notify('Title is required', 'warning', 2000); return; }
    this.taskService.createTask(this.newTask).subscribe({
      next: (res) => { notify('Task created', 'success', 2000); this.showNewTask = false; this.loadTasks(); },
      error: (err) => notify('Create failed: ' + (err.error?.error || err.message), 'error', 4000)
    });
  };

  deleteSelected = (): void => {
    const keys = this.treeList?.instance?.getSelectedRowKeys();
    if (!keys?.length) { notify('Select a task first', 'warning', 2000); return; }
    const task = this.tasks.find(t => t.id === keys[0]);
    confirm('Delete this task?', 'Confirm Delete').then(ok => {
      if (ok) {
        this.taskService.deleteTask(keys[0]).subscribe({
          next: () => { notify('Deleted', 'success', 2000); this.loadTasks(); },
          error: () => notify('Delete failed', 'error', 3000)
        });
      }
    });
  };

  refresh = (): void => { this.loadTasks(); notify('Refreshed', 'info', 1000); };
}
