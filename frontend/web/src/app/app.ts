import {Component, computed, inject, OnInit, signal} from '@angular/core';
import {CommonModule} from '@angular/common';
import {Router, RouterLink, RouterOutlet} from '@angular/router';
import {AuthService} from './core/auth/auth.service';
import {PushNotificationService} from './core/services/push-notification.service';
import {generateCspHeader, SecurityService} from './core/security/security.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink],
  template: `
    <div class="app-container">

      <!-- Navigation Header -->
      <header class="app-header" *ngIf="authService.isAuthenticated()">
        <div class="header-content">

          <!-- Logo and Title -->
          <div class="brand">
            <div class="logo">🏦</div>
            <h1>eBanking</h1>
          </div>

          <!-- Main Navigation -->
          <nav class="main-nav">
            <a routerLink="/dashboard"
               class="nav-link"
               [class.active]="isActiveRoute('/dashboard')">
              📊 Dashboard
            </a>
            <a routerLink="/transfer"
               class="nav-link"
               [class.active]="isActiveRoute('/transfer')">
              💸 Transfer
            </a>
            <a routerLink="/transactions"
               class="nav-link"
               [class.active]="isActiveRoute('/transactions')">
              📋 History
            </a>
            <a routerLink="/download"
               class="nav-link"
               [class.active]="isActiveRoute('/download')">
              📄 Statements
            </a>
            <a routerLink="/notifications"
               class="nav-link"
               [class.active]="isActiveRoute('/notifications')">
              🔔 Notifications
            </a>
          </nav>

          <!-- User Menu -->
          <div class="user-menu">
            <div class="user-info">
              <span class="user-name">User</span>
              <div class="user-status">
                <span class="status-dot online"></span>
                Online
              </div>
            </div>
            <button class="logout-btn" (click)="logout()">
              🚪 Logout
            </button>
          </div>
        </div>
      </header>

      <!-- Login Prompt for Unauthenticated Users -->
      <div class="login-prompt" *ngIf="!authService.isAuthenticated()">
        <div class="login-card">
          <div class="login-icon">🏦</div>
          <h2>Welcome to eBanking</h2>
          <p>Please log in to access your account</p>
          <button class="login-button" (click)="login()">
            🔐 Secure Login
          </button>

          <!-- Security Notice -->
          <div class="security-notice">
            <div class="security-icon">🛡️</div>
            <div class="security-text">
              <strong>Your security is our priority</strong>
              <ul>
                <li>Multi-factor authentication enabled</li>
                <li>End-to-end encryption</li>
                <li>Real-time fraud monitoring</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content Area -->
      <main class="main-content" [class.authenticated]="authService.isAuthenticated()">
        <router-outlet></router-outlet>
      </main>

      <!-- Push Notification Permission Banner -->
      <div class="notification-banner"
           *ngIf="authService.isAuthenticated() && showNotificationBannerValue()">
        <div class="banner-content">
          <div class="banner-icon">🔔</div>
          <div class="banner-text">
            <strong>Enable Notifications</strong>
            <p>Get instant alerts for transactions and security updates</p>
          </div>
          <div class="banner-actions">
            <button class="btn btn-primary" (click)="requestNotificationPermission()">
              Enable
            </button>
            <button class="btn btn-secondary" (click)="dismissNotificationBanner()">
              Maybe Later
            </button>
          </div>
        </div>
      </div>

      <!-- Loading Overlay -->
      <div class="loading-overlay" *ngIf="isLoading()">
        <div class="loading-spinner">
          <div class="spinner"></div>
          <p>{{ loadingMessage() }}</p>
        </div>
      </div>

      <!-- Error Toast -->
      <div class="error-toast" *ngIf="errorMessage()" (click)="clearError()">
        <div class="toast-content">
          <div class="toast-icon">❌</div>
          <div class="toast-message">{{ errorMessage() }}</div>
          <button class="toast-close">✕</button>
        </div>
      </div>

    </div>
  `,
  styles: [`
    .app-container {
      min-height: 100vh;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      display: flex;
      flex-direction: column;
    }

    /* Header Styles */
    .app-header {
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(10px);
      border-bottom: 1px solid rgba(0, 0, 0, 0.1);
      box-shadow: 0 2px 20px rgba(0, 0, 0, 0.1);
      position: sticky;
      top: 0;
      z-index: 1000;
    }

    .header-content {
      max-width: 1200px;
      margin: 0 auto;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 24px;
      gap: 32px;
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-shrink: 0;
    }

    .logo {
      font-size: 2rem;
    }

    .brand h1 {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 700;
      background: linear-gradient(135deg, #667eea, #764ba2);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .main-nav {
      display: flex;
      gap: 8px;
      flex: 1;
      justify-content: center;
    }

    .nav-link {
      padding: 12px 20px;
      border-radius: 8px;
      text-decoration: none;
      color: #495057;
      font-weight: 500;
      transition: all 0.2s ease;
      white-space: nowrap;
    }

    .nav-link:hover {
      background-color: rgba(102, 126, 234, 0.1);
      color: #667eea;
    }

    .nav-link.active {
      background-color: #667eea;
      color: white;
    }

    .user-menu {
      display: flex;
      align-items: center;
      gap: 16px;
      flex-shrink: 0;
    }

    .user-info {
      text-align: right;
    }

    .user-name {
      font-weight: 600;
      color: #2c3e50;
      display: block;
    }

    .user-status {
      font-size: 0.75rem;
      color: #6c757d;
      display: flex;
      align-items: center;
      gap: 4px;
      justify-content: flex-end;
    }

    .status-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background-color: #dc3545;
    }

    .status-dot.online {
      background-color: #28a745;
    }

    .logout-btn {
      padding: 8px 16px;
      background-color: #dc3545;
      color: white;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      font-size: 0.875rem;
      transition: all 0.2s ease;
    }

    .logout-btn:hover {
      background-color: #c82333;
    }

    /* Login Prompt Styles */
    .login-prompt {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 40px 20px;
    }

    .login-card {
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(10px);
      padding: 48px;
      border-radius: 16px;
      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.1);
      text-align: center;
      max-width: 480px;
      width: 100%;
    }

    .login-icon {
      font-size: 4rem;
      margin-bottom: 24px;
    }

    .login-card h2 {
      margin: 0 0 12px 0;
      color: #2c3e50;
      font-size: 2rem;
      font-weight: 700;
    }

    .login-card p {
      color: #6c757d;
      margin-bottom: 32px;
      font-size: 1.125rem;
    }

    .login-button {
      background: linear-gradient(135deg, #667eea, #764ba2);
      color: white;
      border: none;
      padding: 16px 32px;
      border-radius: 8px;
      font-size: 1.125rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
      margin-bottom: 32px;
    }

    .login-button:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 20px rgba(102, 126, 234, 0.4);
    }

    .security-notice {
      display: flex;
      align-items: flex-start;
      gap: 16px;
      text-align: left;
      background-color: #f8f9fa;
      padding: 20px;
      border-radius: 8px;
      border-left: 4px solid #28a745;
    }

    .security-icon {
      font-size: 1.5rem;
      flex-shrink: 0;
    }

    .security-text strong {
      color: #2c3e50;
      display: block;
      margin-bottom: 8px;
    }

    .security-text ul {
      margin: 0;
      padding-left: 20px;
      color: #495057;
    }

    .security-text li {
      margin-bottom: 4px;
    }

    /* Main Content */
    .main-content {
      flex: 1;
      padding: 0;
    }

    .main-content.authenticated {
      padding: 24px;
      max-width: 1200px;
      margin: 0 auto;
      width: 100%;
    }

    /* Notification Banner */
    .notification-banner {
      background: linear-gradient(135deg, #28a745, #20c997);
      color: white;
      padding: 16px 24px;
      position: sticky;
      bottom: 0;
      z-index: 1000;
    }

    .banner-content {
      max-width: 1200px;
      margin: 0 auto;
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .banner-icon {
      font-size: 1.5rem;
      flex-shrink: 0;
    }

    .banner-text {
      flex: 1;
    }

    .banner-text strong {
      display: block;
      margin-bottom: 4px;
    }

    .banner-text p {
      margin: 0;
      opacity: 0.9;
      font-size: 0.875rem;
    }

    .banner-actions {
      display: flex;
      gap: 12px;
    }

    .btn {
      padding: 8px 16px;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      font-size: 0.875rem;
      transition: all 0.2s ease;
    }

    .btn-primary {
      background-color: white;
      color: #28a745;
    }

    .btn-secondary {
      background-color: rgba(255, 255, 255, 0.2);
      color: white;
    }

    .btn:hover {
      transform: translateY(-1px);
    }

    /* Loading Overlay */
    .loading-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(255, 255, 255, 0.9);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 3000;
    }

    .loading-spinner {
      text-align: center;
    }

    .spinner {
      width: 48px;
      height: 48px;
      border: 4px solid #e9ecef;
      border-top: 4px solid #667eea;
      border-radius: 50%;
      animation: spin 1s linear infinite;
      margin: 0 auto 16px auto;
    }

    @keyframes spin {
      0% { transform: rotate(0deg); }
      100% { transform: rotate(360deg); }
    }

    .loading-spinner p {
      color: #495057;
      margin: 0;
    }

    /* Error Toast */
    .error-toast {
      position: fixed;
      top: 24px;
      right: 24px;
      background: #dc3545;
      color: white;
      border-radius: 8px;
      padding: 16px;
      box-shadow: 0 4px 12px rgba(220, 53, 69, 0.3);
      cursor: pointer;
      z-index: 4000;
      animation: slideIn 0.3s ease;
    }

    @keyframes slideIn {
      from {
        transform: translateX(100%);
        opacity: 0;
      }
      to {
        transform: translateX(0);
        opacity: 1;
      }
    }

    .toast-content {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .toast-icon {
      flex-shrink: 0;
    }

    .toast-message {
      flex: 1;
    }

    .toast-close {
      background: none;
      border: none;
      color: white;
      cursor: pointer;
      font-size: 1.125rem;
      padding: 0;
    }

    /* Responsive Design */
    @media (max-width: 768px) {
      .header-content {
        flex-direction: column;
        gap: 16px;
        padding: 16px;
      }

      .main-nav {
        flex-wrap: wrap;
        gap: 4px;
      }

      .nav-link {
        padding: 8px 12px;
        font-size: 0.875rem;
      }

      .user-menu {
        order: -1;
        width: 100%;
        justify-content: space-between;
      }

      .login-card {
        padding: 32px 24px;
      }

      .banner-content {
        flex-direction: column;
        text-align: center;
      }

      .banner-actions {
        justify-content: center;
      }
    }
  `]
})
export class AppComponent implements OnInit {
  protected readonly router = inject(Router);
  protected readonly authService = inject(AuthService);
  protected readonly pushService = inject(PushNotificationService);
  protected readonly securityService = inject(SecurityService);

  // Reactive state
  private loadingSignal = signal(false);
  private loadingMessageSignal = signal('');
  private errorMessageSignal = signal('');
  private showNotificationBannerSignal = signal(false);

  // Computed properties
  isLoading = computed(() => this.loadingSignal());
  loadingMessage = computed(() => this.loadingMessageSignal());
  errorMessage = computed(() => this.errorMessageSignal());

  ngOnInit(): void {
    this.initializeApp();
  }

  private async initializeApp(): Promise<void> {
    try {
      this.setLoading(true, 'Initializing application...');

      // Initialize Keycloak authentication
      console.log('Initializing Keycloak...');
      await this.authService.init();
      console.log('Keycloak initialized');

      // Initialize security service with CSP
      this.initializeSecurity();

      // Check notification banner visibility
      this.updateNotificationBannerVisibility();

    } catch (error) {
      console.error('App initialization failed:', error);
      this.setError('Failed to initialize application. Please refresh the page.');
    } finally {
      this.setLoading(false);
    }
  }

  private initializeSecurity(): void {
    // Set CSP header if not already set by server
    if (!document.querySelector('meta[http-equiv="Content-Security-Policy"]')) {
      const cspMeta = document.createElement('meta');
      cspMeta.setAttribute('http-equiv', 'Content-Security-Policy');
      cspMeta.setAttribute('content', generateCspHeader());
      document.head.appendChild(cspMeta);
    }
  }

  async login(): Promise<void> {
    try {
      this.setLoading(true, 'Redirecting to secure login...');
      await this.authService.login();
    } catch (error) {
      console.error('Login failed:', error);
      this.setError('Login failed. Please try again.');
    } finally {
      this.setLoading(false);
    }
  }

  async logout(): Promise<void> {
    try {
      this.setLoading(true, 'Logging out...');
      await this.authService.logout();
      this.router.navigate(['/']);
    } catch (error) {
      console.error('Logout failed:', error);
      this.setError('Logout failed. Please try again.');
    } finally {
      this.setLoading(false);
    }
  }

  async requestNotificationPermission(): Promise<void> {
    try {
      await this.pushService.requestPermission();
      this.showNotificationBannerSignal.set(false);
    } catch (error) {
      console.error('Failed to request notification permission:', error);
      this.setError('Failed to enable notifications.');
    }
  }

  dismissNotificationBanner(): void {
    this.showNotificationBannerSignal.set(false);
    localStorage.setItem('notificationBannerDismissed', 'true');
  }

  showNotificationBannerValue(): boolean {
    return this.showNotificationBannerSignal();
  }

  isActiveRoute(route: string): boolean {
    return this.router.url.startsWith(route);
  }

  clearError(): void {
    this.errorMessageSignal.set('');
  }

  private setLoading(loading: boolean, message: string = ''): void {
    this.loadingSignal.set(loading);
    this.loadingMessageSignal.set(message);
  }

  private setError(message: string): void {
    this.errorMessageSignal.set(message);
    // Auto-clear error after 5 seconds
    setTimeout(() => this.clearError(), 5000);
  }

  private updateNotificationBannerVisibility(): void {
    const dismissed = localStorage.getItem('notificationBannerDismissed') === 'true';
    const shouldShow = this.authService.isAuthenticated() && !dismissed;
    this.showNotificationBannerSignal.set(shouldShow);
  }
}
