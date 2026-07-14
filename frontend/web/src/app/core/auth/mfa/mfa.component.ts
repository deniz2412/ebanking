import {Component, DestroyRef, inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormBuilder, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatCardModule} from '@angular/material/card';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatButtonModule} from '@angular/material/button';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatIconModule} from '@angular/material/icon';
import {MatSelectModule} from '@angular/material/select';
import {Router} from '@angular/router';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {timer, firstValueFrom} from 'rxjs';
import {map, takeWhile} from 'rxjs/operators';

import {AuthService} from '../auth.service';

@Component({
  selector: 'app-mfa',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatSelectModule
  ],
  template: `
    <div class="mfa-container">
      <mat-card class="mfa-card">
        <mat-card-header>
          <mat-card-title>
            <mat-icon>security</mat-icon>
            Multi-Factor Authentication
          </mat-card-title>
          <mat-card-subtitle>
            Please verify your identity to continue
          </mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <div *ngIf="challenge$ | async as challenge">
            <div class="challenge-info">
              <p class="challenge-method">
                <strong>Verification Method:</strong>
                {{ getMethodDisplayName(challenge.method) }}
              </p>
              <p *ngIf="challenge.maskedTarget" class="masked-target">
                <strong>Sent to:</strong> {{ challenge.maskedTarget }}
              </p>
            </div>

            <form [formGroup]="mfaForm" (ngSubmit)="onSubmit()">
              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Verification Code</mat-label>
                <input
                  matInput
                  type="text"
                  formControlName="code"
                  placeholder="Enter 6-digit code"
                  maxlength="6"
                  autocomplete="one-time-code"
                  inputmode="numeric"
                  pattern="[0-9]*"
                  (input)="onCodeInput($event)"
                >
                <mat-icon matSuffix>vpn_key</mat-icon>
                <mat-error *ngIf="mfaForm.get('code')?.hasError('required')">
                  Verification code is required
                </mat-error>
                <mat-error *ngIf="mfaForm.get('code')?.hasError('pattern')">
                  Please enter a valid 6-digit code
                </mat-error>
              </mat-form-field>

              <div class="countdown" *ngIf="countdown$ | async as countdown">
                <p class="countdown-text">
                  Code expires in: <strong>{{ countdown }}s</strong>
                </p>
              </div>

              <div class="form-actions">
                <button
                  mat-raised-button
                  color="primary"
                  type="submit"
                  [disabled]="mfaForm.invalid || isVerifying"
                  class="full-width"
                >
                  <mat-spinner diameter="20" *ngIf="isVerifying"></mat-spinner>
                  <span *ngIf="!isVerifying">Verify Code</span>
                  <span *ngIf="isVerifying">Verifying...</span>
                </button>

                <button
                  mat-button
                  type="button"
                  (click)="resendCode()"
                  [disabled]="isResending || canResend"
                  class="full-width resend-button"
                >
                  <mat-spinner diameter="20" *ngIf="isResending"></mat-spinner>
                  <span *ngIf="!isResending">Resend Code</span>
                  <span *ngIf="isResending">Sending...</span>
                </button>
              </div>
            </form>
          </div>

          <div *ngIf="errorMessage" class="error-message">
            <mat-icon>error</mat-icon>
            {{ errorMessage }}
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .mfa-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 20px;
    }

    .mfa-card {
      width: 100%;
      max-width: 400px;
      padding: 24px;
      border-radius: 16px;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
    }

    .mfa-card mat-card-title {
      display: flex;
      align-items: center;
      gap: 8px;
      color: #333;
    }

    .challenge-info {
      background: #f5f5f5;
      padding: 16px;
      border-radius: 8px;
      margin-bottom: 24px;
    }

    .challenge-method, .masked-target {
      margin: 0 0 8px 0;
      font-size: 14px;
    }

    .full-width {
      width: 100%;
    }

    .form-actions {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-top: 24px;
    }

    .resend-button {
      color: #666;
    }

    .countdown {
      text-align: center;
      margin: 16px 0;
    }

    .countdown-text {
      font-size: 14px;
      color: #666;
      margin: 0;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px;
      background: #ffebee;
      color: #c62828;
      border-radius: 8px;
      margin-top: 16px;
    }

    mat-form-field {
      margin-bottom: 16px;
    }

    /* Auto-advance styling */
    input[inputmode="numeric"] {
      text-align: center;
      font-size: 18px;
      letter-spacing: 2px;
    }
  `]
})
export class MfaComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  mfaForm: FormGroup;
  challenge$ = this.authService.mfaChallenge$;
  countdown$ = timer(0, 1000).pipe(
    map(tick => 300 - tick), // 5 minutes countdown
    takeWhile(time => time >= 0)
  );

  isVerifying = false;
  isResending = false;
  canResend = false;
  errorMessage = '';

  constructor() {
    this.mfaForm = this.fb.group({
      code: ['', [
        Validators.required,
        Validators.pattern(/^\d{6}$/)
      ]]
    });
  }

  ngOnInit(): void {
    // Auto-focus on the code input
    setTimeout(() => {
      const codeInput = document.querySelector('input[formControlName="code"]') as HTMLInputElement;
      if (codeInput) {
        codeInput.focus();
      }
    }, 100);

    // Enable resend after 30 seconds
    timer(30000).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.canResend = true;
    });
  }

  onCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, ''); // Remove non-digits

    if (value.length > 6) {
      value = value.substring(0, 6);
    }

    this.mfaForm.patchValue({ code: value });

    // Auto-submit when 6 digits are entered
    if (value.length === 6) {
      setTimeout(() => this.onSubmit(), 100);
    }
  }

  async onSubmit(): Promise<void> {
    if (this.mfaForm.invalid || this.isVerifying) {
      return;
    }

    // Get current challenge value
    const challenge = await firstValueFrom(this.challenge$.pipe(takeUntilDestroyed(this.destroyRef)));
    if (!challenge) {
      this.errorMessage = 'No active MFA challenge found.';
      return;
    }

    this.isVerifying = true;
    this.errorMessage = '';

    const { code } = this.mfaForm.value;

    this.authService.verifyMfaChallenge(challenge.challengeId, code)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (verified) => {
          this.isVerifying = false;
          if (verified) {
            this.router.navigate(['/dashboard']);
          } else {
            this.errorMessage = 'Invalid verification code. Please try again.';
            this.mfaForm.patchValue({ code: '' });
          }
        },
        error: (error) => {
          this.isVerifying = false;
          this.errorMessage = error.error?.message || 'Verification failed. Please try again.';
          this.mfaForm.patchValue({ code: '' });
        }
      });
  }

  async resendCode(): Promise<void> {
    if (this.isResending || this.canResend) {
      return;
    }

    this.isResending = true;
    this.errorMessage = '';

    try {
      await this.authService.initiateMfaChallenge();
      this.canResend = false;

      // Re-enable resend after 30 seconds
      timer(30000).pipe(
        takeUntilDestroyed(this.destroyRef)
      ).subscribe(() => {
        this.canResend = true;
      });

    } catch (error: any) {
      this.errorMessage = error.error?.message || 'Failed to resend code. Please try again.';
    } finally {
      this.isResending = false;
    }
  }

  getMethodDisplayName(method: string): string {
    switch (method) {
      case 'totp': return 'Authenticator App (TOTP)';
      case 'sms': return 'SMS Message';
      case 'email': return 'Email';
      default: return 'Unknown Method';
    }
  }
}
