import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DxTextBoxModule, DxButtonModule, DxValidationGroupModule } from 'devextreme-angular';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, DxTextBoxModule, DxButtonModule, DxValidationGroupModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <h1>Central</h1>
        <p class="subtitle">Enterprise Platform</p>

        <dx-validation-group>
          <dx-text-box [(value)]="email" placeholder="Email" mode="email" [stylingMode]="'outlined'">
          </dx-text-box>

          <dx-text-box [(value)]="password" placeholder="Password" mode="password" [stylingMode]="'outlined'"
                       (onEnterKey)="login()" class="mt-3">
          </dx-text-box>

          <div *ngIf="mfaRequired" class="mt-3">
            <dx-text-box [(value)]="mfaCode" placeholder="MFA Code" [stylingMode]="'outlined'">
            </dx-text-box>
          </div>

          <div *ngIf="error" class="error-text">{{ error }}</div>

          <dx-button [text]="mfaRequired ? 'Verify MFA' : 'Sign In'" type="default"
                     [stylingMode]="'contained'" width="100%" class="mt-4"
                     (onClick)="mfaRequired ? verifyMfa() : login()">
          </dx-button>
        </dx-validation-group>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex; justify-content: center; align-items: center;
      min-height: 100vh; background: #0f172a;
    }
    .login-card {
      background: #1e293b; border-radius: 12px; padding: 48px;
      width: 400px; box-shadow: 0 8px 32px rgba(0,0,0,0.4);
    }
    h1 { color: #fff; font-size: 28px; margin: 0 0 4px 0; text-align: center; }
    .subtitle { color: #9ca3af; text-align: center; margin: 0 0 32px 0; }
    .mt-3 { margin-top: 12px; }
    .mt-4 { margin-top: 16px; }
    .error-text { color: #ef4444; font-size: 13px; margin-top: 8px; }
  `]
})
export class LoginComponent {
  email = '';
  password = '';
  mfaCode = '';
  mfaRequired = false;
  sessionId = '';
  error = '';

  constructor(private auth: AuthService, private router: Router) {}

  login(): void {
    this.error = '';
    this.auth.login(this.email, this.password).subscribe({
      next: resp => {
        if (resp.mfa_required) {
          this.mfaRequired = true;
          this.sessionId = resp.session_id;
        } else {
          this.router.navigate(['/']);
        }
      },
      error: err => {
        this.error = err.error?.error || 'Login failed';
      }
    });
  }

  verifyMfa(): void {
    this.error = '';
    this.auth.verifyMfa(this.sessionId, this.mfaCode).subscribe({
      next: () => this.router.navigate(['/']),
      error: err => this.error = err.error?.error || 'MFA verification failed'
    });
  }
}
