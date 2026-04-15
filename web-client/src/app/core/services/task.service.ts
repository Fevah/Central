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
  project_id?: number;
  name: string;
  start_date: string | null;
  end_date: string | null;
  goal?: string | null;
  status: string;
  velocity_points?: number | null;
  velocity_hours?: number | null;
}

export interface BurndownPoint {
  snapshot_date: string;
  points_remaining: number;
  hours_remaining: number;
  points_completed: number;
  hours_completed: number;
}

export interface TimeEntry {
  id: number;
  entry_date: string;
  hours: number;
  activity_type: string;
  notes: string;
  user: string;
}

export interface NewTimeEntry {
  hours: number;
  entry_date?: string;
  activity_type?: string;
  notes?: string;
  user_id?: number;
}

export interface TaskDependency {
  id: number;
  predecessor_id: number;
  successor_id: number;
  dep_type?: string;
  lag_days?: number;
  predecessor_title?: string;
  successor_title?: string;
}

export interface TaskActivity {
  id: number;
  timestamp: string;
  action: string;
  summary: string;
  user: string;
}

@Injectable({ providedIn: 'root' })
export class TaskService {
  /** task-service base — used for the high-frequency CRUD + SSE stream. */
  private baseUrl = environment.taskServiceUrl;
  /** Central.Api base — used for sprints, burndown, time entries, dependencies. */
  private centralBase = environment.centralApiUrl;

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
    // Sprints are owned by Central.Api (richer DTO with velocity etc.)
    return this.http.get<Sprint[]>(`${this.centralBase}/api/projects/${projectId}/sprints`);
  }

  createSprint(projectId: number, body: Partial<Sprint>): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(
      `${this.centralBase}/api/projects/${projectId}/sprints`, body);
  }

  updateSprint(projectId: number, id: number, body: Partial<Sprint>): Observable<{ id: number }> {
    return this.http.put<{ id: number }>(
      `${this.centralBase}/api/projects/${projectId}/sprints/${id}`, body);
  }

  deleteSprint(projectId: number, id: number): Observable<void> {
    return this.http.delete<void>(`${this.centralBase}/api/projects/${projectId}/sprints/${id}`);
  }

  // ── Burndown ─────────────────────────────────────────────────────────

  getBurndown(projectId: number, sprintId: number): Observable<BurndownPoint[]> {
    return this.http.get<BurndownPoint[]>(
      `${this.centralBase}/api/projects/${projectId}/sprints/${sprintId}/burndown`);
  }

  /** Force-snapshot today's burndown numbers (idempotent — overwrites today). */
  snapshotBurndown(projectId: number, sprintId: number): Observable<{ snapshotted: boolean }> {
    return this.http.post<{ snapshotted: boolean }>(
      `${this.centralBase}/api/projects/${projectId}/sprints/${sprintId}/burndown/snapshot`, null);
  }

  // ── Time entries ─────────────────────────────────────────────────────

  getTimeEntries(taskId: number): Observable<TimeEntry[]> {
    return this.http.get<TimeEntry[]>(`${this.centralBase}/api/tasks/${taskId}/time`);
  }

  addTimeEntry(taskId: number, entry: NewTimeEntry): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(`${this.centralBase}/api/tasks/${taskId}/time`, entry);
  }

  deleteTimeEntry(taskId: number, entryId: number): Observable<void> {
    return this.http.delete<void>(`${this.centralBase}/api/tasks/${taskId}/time/${entryId}`);
  }

  // ── Dependencies (Gantt) ─────────────────────────────────────────────

  getDependencies(taskId: number): Observable<TaskDependency[]> {
    return this.http.get<TaskDependency[]>(`${this.centralBase}/api/tasks/${taskId}/dependencies`);
  }

  // ── Activity feed ────────────────────────────────────────────────────

  getTaskActivity(taskId: number, limit = 50): Observable<TaskActivity[]> {
    return this.http.get<TaskActivity[]>(
      `${this.centralBase}/api/tasks/${taskId}/activity?limit=${limit}`);
  }

  /** Global recent activity across all tasks (uses /api/activity). */
  getGlobalActivity(limit = 100): Observable<any[]> {
    return this.http.get<any[]>(`${this.centralBase}/api/activity/global?limit=${limit}`);
  }
}
