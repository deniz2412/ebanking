import {Component, inject, OnInit, signal} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormBuilder, FormGroup, ReactiveFormsModule} from '@angular/forms';

import {NotificationSettings, PushNotificationService} from '../core/services/push-notification.service';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="settings-container">
      <div class="settings-card">
        <header class="settings-header">
          <h1>🔔 Notification Settings</h1>
          <p>Manage your push notification preferences</p>
        </header>

        <!-- Browser Support Check -->
        <div class="support-check" *ngIf="!isSupported()">
          <div class="warning-message">
            <span class="icon">⚠️</span>
            <div>
              <strong>Push notifications not supported</strong>
              <p>Your browser doesn't support push notifications. Please use a modern browser like Chrome, Firefox, or Safari.</p>
            </div>
          </div>
        </div>

        <!-- Permission Status -->
        <div class="permission-status" *ngIf="isSupported()">
          <div class="status-card" [class]="getPermissionStatusClass()">
            <div class="status-info">
              <span class="status-icon">{{ getPermissionIcon() }}</span>
              <div>
                <strong>{{ getPermissionText() }}</strong>
                <p>{{ getPermissionDescription() }}</p>
              </div>
            </div>
            <div class="status-actions">
              <button
                class="btn btn-primary"
                *ngIf="permissionStatus() === 'default'"
                (click)="requestPermission()"
                [disabled]="isLoading()"
              >
                Enable Notifications
              </button>
              <button
                class="btn btn-success"
                *ngIf="permissionStatus() === 'granted' && !isSubscribed()"
                (click)="subscribe()"
                [disabled]="isLoading()"
              >
                <span *ngIf="!isLoading()">Subscribe to Notifications</span>
                <span *ngIf="isLoading()">Subscribing...</span>
              </button>
              <button
                class="btn btn-danger"
                *ngIf="isSubscribed()"
                (click)="unsubscribe()"
                [disabled]="isLoading()"
              >
                <span *ngIf="!isLoading()">Unsubscribe</span>
                <span *ngIf="isLoading()">Unsubscribing...</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Notification Settings Form -->
        <div class="settings-form" *ngIf="isSubscribed()">
          <h3>Notification Types</h3>
          <p>Choose which types of notifications you'd like to receive</p>

          <form [formGroup]="settingsForm" (ngSubmit)="saveSettings()">
            <div class="setting-group">
              <div class="setting-item">
                <div class="setting-info">
                  <label for="transactionAlerts">
                    <span class="setting-icon">💳</span>
                    <div>
                      <strong>Transaction Alerts</strong>
                      <p>Get notified about incoming and outgoing transactions</p>
                    </div>
                  </label>
                </div>
                <div class="setting-control">
                  <label class="toggle-switch">
                    <input
                      type="checkbox"
                      id="transactionAlerts"
                      formControlName="transactionAlerts"
                    >
                    <span class="toggle-slider"></span>
                  </label>
                </div>
              </div>

              <div class="setting-item">
                <div class="setting-info">
                  <label for="securityAlerts">
                    <span class="setting-icon">🔐</span>
                    <div>
                      <strong>Security Alerts</strong>
                      <p>Important security notifications and login alerts</p>
                    </div>
                  </label>
                </div>
                <div class="setting-control">
                  <label class="toggle-switch">
                    <input
                      type="checkbox"
                      id="securityAlerts"
                      formControlName="securityAlerts"
                    >
                    <span class="toggle-slider"></span>
                  </label>
                </div>
              </div>

              <div class="setting-item">
                <div class="setting-info">
                  <label for="maintenanceAlerts">
                    <span class="setting-icon">🔧</span>
                    <div>
                      <strong>Maintenance Alerts</strong>
                      <p>System maintenance and service updates</p>
                    </div>
                  </label>
                </div>
                <div class="setting-control">
                  <label class="toggle-switch">
                    <input
                      type="checkbox"
                      id="maintenanceAlerts"
                      formControlName="maintenanceAlerts"
                    >
                    <span class="toggle-slider"></span>
                  </label>
                </div>
              </div>

              <div class="setting-item">
                <div class="setting-info">
                  <label for="marketingNotifications">
                    <span class="setting-icon">📢</span>
                    <div>
                      <strong>Marketing Notifications</strong>
                      <p>Promotional offers and product updates</p>
                    </div>
                  </label>
                </div>
                <div class="setting-control">
                  <label class="toggle-switch">
                    <input
                      type="checkbox"
                      id="marketingNotifications"
                      formControlName="marketingNotifications"
                    >
                    <span class="toggle-slider"></span>
                  </label>
                </div>
              </div>
            </div>

            <div class="form-actions">
              <button
                type="submit"
                class="btn btn-primary"
                [disabled]="!settingsForm.dirty || isSaving()"
              >
                <span *ngIf="!isSaving()">Save Settings</span>
                <span *ngIf="isSaving()">Saving...</span>
              </button>

              <button
                type="button"
                class="btn btn-secondary"
                (click)="testNotification()"
                [disabled]="isTesting()"
              >
                <span *ngIf="!isTesting()">🧪 Test Notification</span>
                <span *ngIf="isTesting()">Sending...</span>
              </button>
            </div>
          </form>
        </div>

        <!-- Browser Instructions -->
        <div class="browser-instructions" *ngIf="permissionStatus() === 'denied'">
          <h4>🛠️ How to Enable Notifications</h4>
          <div class="instruction-tabs">
            <div class="tab-content">
              <div class="instruction-section">
                <h5>Chrome</h5>
                <ol>
                  <li>Click the lock icon next to the URL</li>
                  <li>Set "Notifications" to "Allow"</li>
                  <li>Refresh the page</li>
                </ol>
              </div>
              <div class="instruction-section">
                <h5>Firefox</h5>
                <ol>
                  <li>Click the shield icon in the address bar</li>
                  <li>Click "Enable" next to notifications</li>
                  <li>Refresh the page</li>
                </ol>
              </div>
              <div class="instruction-section">
                <h5>Safari</h5>
                <ol>
                  <li>Go to Safari → Preferences → Websites</li>
                  <li>Select "Notifications" from the left sidebar</li>
                  <li>Set this website to "Allow"</li>
                </ol>
              </div>
            </div>
          </div>
        </div>

        <!-- Success/Error Messages -->
        <div class="message" *ngIf="message()" [class]="messageType()">
          <span class="message-icon">{{ getMessageIcon() }}</span>
          {{ message() }}
        </div>
      </div>
    </div>
  `,
  styles: [`
    .settings-container {
      max-width: 800px;
      margin: 0 auto;
      padding: 20px;
      min-height: 100vh;
      background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
    }

    .settings-card {
      background: white;
      border-radius: 16px;
      padding: 32px;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.1);
    }

    .settings-header {
      text-align: center;
      margin-bottom: 32px;
    }

    .settings-header h1 {
      margin: 0 0 8px 0;
      color: #333;
      font-size: 28px;
    }

    .settings-header p {
      margin: 0;
      color: #666;
      font-size: 16px;
    }

    .warning-message {
      display: flex;
      align-items: flex-start;
      gap: 16px;
      padding: 16px;
      background: #fff3cd;
      border: 1px solid #ffeaa7;
      border-radius: 8px;
      color: #856404;
      margin-bottom: 24px;
    }

    .warning-message .icon {
      font-size: 24px;
    }

    .permission-status {
      margin-bottom: 32px;
    }

    .status-card {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 20px;
      border-radius: 12px;
      border-left: 4px solid;
      gap: 16px;
    }

    .status-card.default {
      background: #f8f9fa;
      border-left-color: #6c757d;
    }

    .status-card.granted {
      background: #d4edda;
      border-left-color: #28a745;
    }

    .status-card.denied {
      background: #f8d7da;
      border-left-color: #dc3545;
    }

    .status-info {
      display: flex;
      align-items: center;
      gap: 12px;
      flex: 1;
    }

    .status-icon {
      font-size: 24px;
    }

    .status-info strong {
      display: block;
      color: #333;
      margin-bottom: 4px;
    }

    .status-info p {
      margin: 0;
      color: #666;
      font-size: 14px;
    }

    .settings-form {
      margin-bottom: 32px;
    }

    .settings-form h3 {
      margin: 0 0 8px 0;
      color: #333;
      font-size: 20px;
    }

    .settings-form > p {
      margin: 0 0 24px 0;
      color: #666;
    }

    .setting-group {
      display: flex;
      flex-direction: column;
      gap: 20px;
      margin-bottom: 32px;
    }

    .setting-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 20px;
      background: #f8f9fa;
      border-radius: 12px;
      gap: 16px;
    }

    .setting-info {
      flex: 1;
    }

    .setting-info label {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      cursor: pointer;
      font-weight: normal;
      margin: 0;
    }

    .setting-icon {
      font-size: 24px;
      margin-top: 2px;
    }

    .setting-info strong {
      display: block;
      color: #333;
      margin-bottom: 4px;
      font-size: 16px;
    }

    .setting-info p {
      margin: 0;
      color: #666;
      font-size: 14px;
      line-height: 1.4;
    }

    .toggle-switch {
      position: relative;
      display: inline-block;
      width: 50px;
      height: 24px;
      cursor: pointer;
    }

    .toggle-switch input {
      opacity: 0;
      width: 0;
      height: 0;
    }

    .toggle-slider {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background-color: #ccc;
      border-radius: 24px;
      transition: 0.3s;
    }

    .toggle-slider:before {
      position: absolute;
      content: "";
      height: 18px;
      width: 18px;
      left: 3px;
      bottom: 3px;
      background-color: white;
      border-radius: 50%;
      transition: 0.3s;
    }

    input:checked + .toggle-slider {
      background-color: #667eea;
    }

    input:checked + .toggle-slider:before {
      transform: translateX(26px);
    }

    .form-actions {
      display: flex;
      gap: 16px;
      justify-content: center;
      flex-wrap: wrap;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 12px 24px;
      border: none;
      border-radius: 8px;
      font-size: 16px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
      text-decoration: none;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-primary {
      background: #667eea;
      color: white;
    }

    .btn-primary:hover:not(:disabled) {
      background: #5a6fd8;
    }

    .btn-secondary {
      background: #e9ecef;
      color: #495057;
    }

    .btn-secondary:hover:not(:disabled) {
      background: #dee2e6;
    }

    .btn-success {
      background: #28a745;
      color: white;
    }

    .btn-success:hover:not(:disabled) {
      background: #218838;
    }

    .btn-danger {
      background: #dc3545;
      color: white;
    }

    .btn-danger:hover:not(:disabled) {
      background: #c82333;
    }

    .browser-instructions {
      background: #f8f9fa;
      border-radius: 12px;
      padding: 24px;
      margin-bottom: 24px;
    }

    .browser-instructions h4 {
      margin: 0 0 16px 0;
      color: #333;
    }

    .instruction-section {
      margin-bottom: 16px;
    }

    .instruction-section h5 {
      margin: 0 0 8px 0;
      color: #667eea;
      font-size: 16px;
    }

    .instruction-section ol {
      margin: 0;
      padding-left: 20px;
      color: #666;
    }

    .instruction-section li {
      margin-bottom: 4px;
      line-height: 1.4;
    }

    .message {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 16px;
      border-radius: 8px;
      font-size: 14px;
      margin-top: 24px;
    }

    .message.success {
      background: #d4edda;
      color: #155724;
      border: 1px solid #c3e6cb;
    }

    .message.error {
      background: #f8d7da;
      color: #721c24;
      border: 1px solid #f5c6cb;
    }

    .message.info {
      background: #d1ecf1;
      color: #0c5460;
      border: 1px solid #bee5eb;
    }

    .message-icon {
      font-size: 18px;
    }

    @media (max-width: 600px) {
      .status-card {
        flex-direction: column;
        align-items: stretch;
      }

      .status-actions {
        text-align: center;
      }

      .setting-item {
        flex-direction: column;
        align-items: stretch;
      }

      .setting-control {
        text-align: center;
        margin-top: 12px;
      }
    }
  `]
})
export class NotificationSettingsComponent implements OnInit {
  private readonly pushService = inject(PushNotificationService);
  private readonly fb = inject(FormBuilder);

  isSupported = signal(false);
  permissionStatus = signal<NotificationPermission>('default');
  isSubscribed = signal(false);
  isLoading = signal(false);
  isSaving = signal(false);
  isTesting = signal(false);
  message = signal('');
  messageType = signal<'success' | 'error' | 'info'>('info');

  settingsForm: FormGroup;

  constructor() {
    this.settingsForm = this.fb.group({
      transactionAlerts: [true],
      securityAlerts: [true],
      marketingNotifications: [false],
      maintenanceAlerts: [true]
    });
  }

  ngOnInit(): void {
    this.isSupported.set(this.pushService.isSupported$());

    if (this.isSupported()) {
      this.setupSubscriptions();
    }
  }

  private setupSubscriptions(): void {
    // Monitor permission status
    this.pushService.permission$.subscribe(permission => {
      this.permissionStatus.set(permission);
    });

    // Monitor subscription status
    this.pushService.subscription$.subscribe(subscription => {
      this.isSubscribed.set(!!subscription);
    });

    // Load settings
    this.pushService.settings$.subscribe(settings => {
      this.settingsForm.patchValue(settings, { emitEvent: false });
    });
  }

  async requestPermission(): Promise<void> {
    this.isLoading.set(true);
    this.clearMessage();

    try {
      const permission = await this.pushService.requestPermission();

      if (permission === 'granted') {
        this.showMessage('Notification permission granted! You can now subscribe to notifications.', 'success');
      } else {
        this.showMessage('Notification permission denied. You can enable it manually in your browser settings.', 'error');
      }
    } catch (error: any) {
      this.showMessage(`Failed to request permission: ${error.message}`, 'error');
    } finally {
      this.isLoading.set(false);
    }
  }

  async subscribe(): Promise<void> {
    this.isLoading.set(true);
    this.clearMessage();

    try {
      await this.pushService.subscribe();
      this.showMessage('Successfully subscribed to push notifications!', 'success');
    } catch (error: any) {
      this.showMessage(`Failed to subscribe: ${error.message}`, 'error');
    } finally {
      this.isLoading.set(false);
    }
  }

  async unsubscribe(): Promise<void> {
    this.isLoading.set(true);
    this.clearMessage();

    try {
      await this.pushService.unsubscribe();
      this.showMessage('Successfully unsubscribed from push notifications.', 'info');
    } catch (error: any) {
      this.showMessage(`Failed to unsubscribe: ${error.message}`, 'error');
    } finally {
      this.isLoading.set(false);
    }
  }

  saveSettings(): void {
    if (this.settingsForm.invalid) {
      return;
    }

    this.isSaving.set(true);
    this.clearMessage();

    const settings: NotificationSettings = this.settingsForm.value;

    this.pushService.updateSettings(settings).subscribe({
      next: () => {
        this.showMessage('Notification settings saved successfully!', 'success');
        this.settingsForm.markAsPristine();
      },
      error: (error) => {
        this.showMessage(`Failed to save settings: ${error.message}`, 'error');
      },
      complete: () => {
        this.isSaving.set(false);
      }
    });
  }

  testNotification(): void {
    this.isTesting.set(true);
    this.clearMessage();

    this.pushService.testNotification().subscribe({
      next: () => {
        this.showMessage('Test notification sent! Check your browser notifications.', 'success');
      },
      error: (error) => {
        this.showMessage(`Failed to send test notification: ${error.message}`, 'error');
      },
      complete: () => {
        this.isTesting.set(false);
      }
    });
  }

  getPermissionStatusClass(): string {
    return this.permissionStatus();
  }

  getPermissionIcon(): string {
    switch (this.permissionStatus()) {
      case 'granted': return '✅';
      case 'denied': return '❌';
      default: return '❓';
    }
  }

  getPermissionText(): string {
    switch (this.permissionStatus()) {
      case 'granted': return 'Notifications Enabled';
      case 'denied': return 'Notifications Blocked';
      default: return 'Permission Required';
    }
  }

  getPermissionDescription(): string {
    switch (this.permissionStatus()) {
      case 'granted': return 'You will receive push notifications when enabled.';
      case 'denied': return 'Push notifications are blocked. Enable them in your browser settings.';
      default: return 'Click the button to enable push notifications.';
    }
  }

  getMessageIcon(): string {
    switch (this.messageType()) {
      case 'success': return '✅';
      case 'error': return '❌';
      default: return 'ℹ️';
    }
  }

  private showMessage(text: string, type: 'success' | 'error' | 'info'): void {
    this.message.set(text);
    this.messageType.set(type);

    // Auto-clear success messages
    if (type === 'success') {
      setTimeout(() => this.clearMessage(), 5000);
    }
  }

  private clearMessage(): void {
    this.message.set('');
  }
}
