import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Task {
  id: number;
  parent_id: number | null;
  title: string;
  status: string;
  priority: string;
  task_type: string;
  points: number | null;
  work_remaining: number | null;
  start_date: string | null;
  finish_date: string | null;
  due_date: string | null;
  assigned_name: string;
  project_name: string;
  sprint_name: string;
  category: string | null;
  severity: string | null;
  risk: string | null;
  color: string | null;
  is_epic: boolean;
  is_milestone: boolean;
  board_column: string | null;
  building: string | null;
  tags: string | null;
  created_at: string;
}

export interface Project {
  id: number;
  name: string;
  description: string | null;
  scheduling_method: string;
  default_mode: string;
  archived: boolean;
}

export interface Sprint {
  id: number;
  project_id: number;
  name: string;
  start_date: string | null;
  end_date: string | null;
  status: string;
}

@Injectable({ providedIn: 'root' })
export class TaskService {
  private baseUrl = environment.taskServiceUrl;

  constructor(private http: HttpClient) {}

  getTasks(projectId?: number, cursor?: number, limit = 200, search?: string): Observable<Task[]> {
    let params = new HttpParams().set('limit', limit.toString());
    if (projectId) params = params.set('project_id', projectId.toString());
    if (cursor) params = params.set('cursor', cursor.toString());
    if (search) params = params.set('search', search);
    return this.http.get<Task[]>(`${this.baseUrl}/api/v1/tasks`, { params });
  }

  getTask(id: number): Observable<Task> {
    return this.http.get<Task>(`${this.baseUrl}/api/v1/tasks/${id}`);
  }

  createTask(task: Partial<Task>): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.baseUrl}/api/v1/tasks`, task);
  }

  updateTask(id: number, task: Partial<Task>): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/api/v1/tasks/${id}`, task);
  }

  deleteTask(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/v1/tasks/${id}`);
  }

  batchCreate(tasks: Partial<Task>[]): Observable<{ count: number }> {
    return this.http.post<{ count: number }>(`${this.baseUrl}/api/v1/tasks/batch`, { tasks });
  }

  getProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/api/v1/tasks/projects`);
  }

  getSprints(projectId: number): Observable<Sprint[]> {
    return this.http.get<Sprint[]>(`${this.baseUrl}/api/v1/tasks/projects/${projectId}/sprints`);
  }
}
