import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxTreeListModule, DxSelectBoxModule, DxToolbarModule } from 'devextreme-angular';
import { DxoSummaryModule, DxiTotalItemModule } from 'devextreme-angular/ui/nested';
import { TaskService, Task, Project } from '../../../core/services/task.service';

@Component({
  selector: 'app-task-tree',
  standalone: true,
  imports: [CommonModule, DxTreeListModule, DxSelectBoxModule, DxToolbarModule, DxoSummaryModule, DxiTotalItemModule],
  template: `
    <dx-toolbar class="task-toolbar">
      <dxi-item location="before">
        <dx-select-box [items]="projects" displayExpr="name" valueExpr="id"
                        placeholder="All Projects" [showClearButton]="true"
                        (onValueChanged)="onProjectChanged($event)">
        </dx-select-box>
      </dxi-item>
      <dxi-item location="before" widget="dxButton"
                [options]="{ text: '+ Task', type: 'default', onClick: addTask }">
      </dxi-item>
    </dx-toolbar>

    <dx-tree-list [dataSource]="tasks" keyExpr="id" parentIdExpr="parent_id"
                   [columnAutoWidth]="true" [showBorders]="true" [showRowLines]="true"
                   [autoExpandAll]="true" [allowColumnReordering]="true"
                   [filterRow]="{ visible: true }" [searchPanel]="{ visible: true }"
                   [headerFilter]="{ visible: true }" [rowAlternationEnabled]="true"
                   [editing]="{ mode: 'cell', allowUpdating: true }"
                   (onRowUpdated)="onRowUpdated($event)" height="calc(100vh - 160px)">

      <dxo-summary>
        <dxi-total-item column="title" summaryType="count" displayFormat="Tasks: {0}" />
        <dxi-total-item column="points" summaryType="sum" displayFormat="Points: {0}" />
      </dxo-summary>

      <dxi-column dataField="id" caption="ID" width="50" [allowEditing]="false" />
      <dxi-column dataField="title" caption="Task" width="280" />
      <dxi-column dataField="status" caption="Status" width="100"
                   [lookup]="{ dataSource: statuses }" />
      <dxi-column dataField="priority" caption="Priority" width="90"
                   [lookup]="{ dataSource: priorities }" />
      <dxi-column dataField="task_type" caption="Type" width="80"
                   [lookup]="{ dataSource: taskTypes }" />
      <dxi-column dataField="points" caption="Points" width="70" dataType="number" />
      <dxi-column dataField="work_remaining" caption="Remaining" width="80" dataType="number" />
      <dxi-column dataField="assigned_name" caption="Assigned" width="120" [allowEditing]="false" />
      <dxi-column dataField="sprint_name" caption="Sprint" width="100" [allowEditing]="false" />
      <dxi-column dataField="category" caption="Category" width="90" />
      <dxi-column dataField="risk" caption="Risk" width="70" />
      <dxi-column dataField="start_date" caption="Start" width="100" dataType="date" />
      <dxi-column dataField="finish_date" caption="Finish" width="100" dataType="date" />
      <dxi-column dataField="due_date" caption="Due" width="100" dataType="date" />
      <dxi-column dataField="building" caption="Building" width="90" />
      <dxi-column dataField="tags" caption="Tags" width="120" />
    </dx-tree-list>
  `,
  styles: [`.task-toolbar { margin-bottom: 8px; }`]
})
export class TaskTreeComponent implements OnInit {
  tasks: Task[] = [];
  projects: Project[] = [];
  selectedProjectId?: number;

  statuses = ['Open', 'InProgress', 'Review', 'Done', 'Blocked'];
  priorities = ['Critical', 'High', 'Medium', 'Low'];
  taskTypes = ['Epic', 'Story', 'Task', 'Bug', 'SubTask', 'Milestone'];

  constructor(private taskService: TaskService) {}

  ngOnInit(): void {
    this.loadProjects();
    this.loadTasks();
  }

  loadProjects(): void {
    this.taskService.getProjects().subscribe(p => this.projects = p);
  }

  loadTasks(): void {
    this.taskService.getTasks(this.selectedProjectId).subscribe(t => this.tasks = t);
  }

  onProjectChanged(e: any): void {
    this.selectedProjectId = e.value;
    this.loadTasks();
  }

  onRowUpdated(e: any): void {
    this.taskService.updateTask(e.key, e.data).subscribe();
  }

  addTask = (): void => {
    this.taskService.createTask({
      title: 'New Task', status: 'Open', priority: 'Medium', task_type: 'Task'
    }).subscribe(() => this.loadTasks());
  };
}
