import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxButtonModule } from 'devextreme-angular';

/**
 * Reusable empty/error state for grids and lists.
 *
 * Three modes (driven by `kind`):
 *   - `empty`   — nothing to show (no error)
 *   - `error`   — load failed; surface the message + a Retry button
 *   - `loading` — show a subtle loader instead of empty UI
 *
 * Place inside any container that hosts a list when its data array is empty:
 *   <app-empty-state *ngIf="!loading && rows.length === 0"
 *                    icon="inbox" title="No links yet"
 *                    message="Use + to create your first one." />
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, DxButtonModule],
  template: `
    <div class="empty-state" [class.error]="kind === 'error'">
      <i *ngIf="icon" [class]="'dx-icon-' + icon"></i>
      <div class="title">{{ title }}</div>
      <div class="message" *ngIf="message">{{ message }}</div>
      <dx-button *ngIf="kind === 'error' && retry"
                 text="Retry" icon="refresh" type="default"
                 (onClick)="retry!()" />
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      padding: 48px 24px; gap: 8px; color: #6b7280; text-align: center;
    }
    .empty-state.error { color: #f87171; }
    .empty-state i { font-size: 40px; opacity: 0.5; margin-bottom: 8px; }
    .title { font-size: 14px; font-weight: 600; color: #d1d5db; }
    .empty-state.error .title { color: #fca5a5; }
    .message { font-size: 12px; max-width: 360px; line-height: 1.5; }
    dx-button { margin-top: 12px; }
  `]
})
export class EmptyStateComponent {
  @Input() kind: 'empty' | 'error' | 'loading' = 'empty';
  @Input() icon  = 'inbox';
  @Input() title = 'Nothing here yet';
  @Input() message?: string;
  /** If provided, a Retry button is rendered (only in error mode). */
  @Input() retry?: () => void;
}
