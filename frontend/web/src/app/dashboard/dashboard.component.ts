import {Component, inject, OnInit} from '@angular/core';
import {CommonModule, CurrencyPipe, DatePipe} from '@angular/common';
import {Router} from '@angular/router';
import {Observable, timer} from 'rxjs';
import {startWith, switchMap} from 'rxjs/operators';

import {AccountService, DashboardData, MonthlySpending, SpendingByCategory} from '../core/services/account.service';
import {AuthService} from '../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe],
  template: `
    <div class="dashboard-container">
      <!-- Header -->
      <header class="dashboard-header">
        <div class="header-content">
          <div class="user-info">
            <h1>Welcome back, {{ (user$ | async)?.firstName || 'User' }}!</h1>
            <p class="last-login">Last login: {{ getCurrentTime() | date:'medium' }}</p>
          </div>
          <div class="quick-actions">
            <button class="quick-action-btn" (click)="navigateToTransfer()">
              <span class="icon">💸</span>
              Transfer
            </button>
            <button class="quick-action-btn" (click)="navigateToPayments()">
              <span class="icon">💳</span>
              Pay Bills
            </button>
            <button class="quick-action-btn" (click)="refreshData()">
              <span class="icon">🔄</span>
              Refresh
            </button>
          </div>
        </div>
      </header>

      <!-- Dashboard Content -->
      <main class="dashboard-main" *ngIf="dashboardData$ | async as data">
        <!-- Balance Overview -->
        <section class="balance-section">
          <div class="balance-card">
            <h2>Account Balance</h2>
            <div class="balance-amount">
              {{ data.balance.balance | currency:'EUR':'symbol':'1.2-2' }}
            </div>
            <div class="balance-details">
              <span class="available">
                Available: {{ data.balance.availableBalance | currency:'EUR':'symbol':'1.2-2' }}
              </span>
              <span class="last-updated">
                Updated: {{ data.balance.lastUpdated | date:'short' }}
              </span>
            </div>
          </div>
        </section>

        <!-- Charts Section -->
        <section class="charts-section">
          <!-- Spending by Category Chart -->
          <div class="chart-card">
            <h3>Spending by Category (Last 3 Months)</h3>
            <div class="pie-chart-container">
              <div class="pie-chart" [style.background]="getPieChartBackground(data.spendingByCategory)">
                <div class="pie-chart-center">
                  <span class="total-spent">
                    {{ getTotalSpent(data.spendingByCategory) | currency:'EUR':'symbol':'1.0-0' }}
                  </span>
                  <span class="total-label">Total Spent</span>
                </div>
              </div>
              <div class="pie-chart-legend">
                <div
                  class="legend-item"
                  *ngFor="let category of data.spendingByCategory; let i = index"
                >
                  <span
                    class="legend-color"
                    [style.background-color]="getCategoryColor(i)"
                  ></span>
                  <span class="legend-label">{{ category.category }}</span>
                  <span class="legend-amount">{{ category.amount | currency:'EUR':'symbol':'1.0-0' }}</span>
                  <span class="legend-percentage">({{ category.percentage }}%)</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Monthly Trends Chart -->
          <div class="chart-card">
            <h3>Monthly Trends (Last 6 Months)</h3>
            <div class="bar-chart-container">
              <div class="bar-chart">
                <div
                  class="bar-group"
                  *ngFor="let month of data.monthlyTrends; let i = index"
                >
                  <div class="bar-label">{{ getMonthName(month.month) }}</div>
                  <div class="bars">
                    <div
                      class="bar spent-bar"
                      [style.height.%]="getBarHeight(month.totalSpent, data.monthlyTrends)"
                      [title]="'Spent: ' + (month.totalSpent | currency:'EUR')"
                    ></div>
                    <div
                      class="bar received-bar"
                      [style.height.%]="getBarHeight(month.totalReceived, data.monthlyTrends)"
                      [title]="'Received: ' + (month.totalReceived | currency:'EUR')"
                    ></div>
                  </div>
                  <div class="bar-values">
                    <span class="spent-value">{{ month.totalSpent | currency:'EUR':'symbol':'1.0-0' }}</span>
                    <span class="received-value">{{ month.totalReceived | currency:'EUR':'symbol':'1.0-0' }}</span>
                  </div>
                </div>
              </div>
              <div class="chart-legend">
                <div class="legend-item">
                  <span class="legend-color spent"></span>
                  <span>Spent</span>
                </div>
                <div class="legend-item">
                  <span class="legend-color received"></span>
                  <span>Received</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- Recent Transactions -->
        <section class="transactions-section">
          <div class="transactions-card">
            <div class="transactions-header">
              <h3>Recent Transactions</h3>
              <button class="view-all-btn" (click)="navigateToTransactions()">
                View All
              </button>
            </div>
            <div class="transactions-list">
              <div
                class="transaction-item"
                *ngFor="let transaction of data.recentTransactions"
                [class.debit]="transaction.type === 'debit'"
                [class.credit]="transaction.type === 'credit'"
              >
                <div class="transaction-icon">
                  <span *ngIf="transaction.type === 'debit'">📤</span>
                  <span *ngIf="transaction.type === 'credit'">📥</span>
                </div>
                <div class="transaction-details">
                  <div class="transaction-description">{{ transaction.description }}</div>
                  <div class="transaction-meta">
                    <span class="category">{{ transaction.category }}</span>
                    <span class="date">{{ transaction.createdAt | date:'short' }}</span>
                  </div>
                  <div class="counterparty" *ngIf="transaction.counterpartyName">
                    {{ transaction.counterpartyName }}
                  </div>
                </div>
                <div class="transaction-amount">
                  <span
                    class="amount"
                    [class.negative]="transaction.type === 'debit'"
                    [class.positive]="transaction.type === 'credit'"
                  >
                    {{ transaction.type === 'debit' ? '-' : '+' }}{{ transaction.amount | currency:'EUR':'symbol':'1.2-2' }}
                  </span>
                  <span class="status" [class]="transaction.status">
                    {{ transaction.status }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>

      <!-- Loading State -->
      <div class="loading-container" *ngIf="!(dashboardData$ | async)">
        <div class="loading-spinner"></div>
        <p>Loading your dashboard...</p>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 20px;
      min-height: 100vh;
      background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
    }

    .dashboard-header {
      background: white;
      border-radius: 12px;
      padding: 24px;
      margin-bottom: 24px;
      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
    }

    .header-content {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 16px;
    }

    .user-info h1 {
      margin: 0 0 8px 0;
      color: #333;
      font-size: 28px;
    }

    .last-login {
      margin: 0;
      color: #666;
      font-size: 14px;
    }

    .quick-actions {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
    }

    .quick-action-btn {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      background: #667eea;
      color: white;
      border: none;
      border-radius: 8px;
      cursor: pointer;
      font-size: 14px;
      transition: all 0.2s;
    }

    .quick-action-btn:hover {
      background: #5a6fd8;
      transform: translateY(-1px);
    }

    .quick-action-btn .icon {
      font-size: 16px;
    }

    .dashboard-main {
      display: grid;
      gap: 24px;
    }

    .balance-section {
      display: grid;
    }

    .balance-card {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      padding: 32px;
      border-radius: 16px;
      text-align: center;
      box-shadow: 0 4px 20px rgba(102, 126, 234, 0.3);
    }

    .balance-card h2 {
      margin: 0 0 16px 0;
      font-size: 18px;
      opacity: 0.9;
    }

    .balance-amount {
      font-size: 48px;
      font-weight: bold;
      margin-bottom: 16px;
    }

    .balance-details {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px;
      font-size: 14px;
      opacity: 0.9;
    }

    .charts-section {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
    }

    @media (max-width: 768px) {
      .charts-section {
        grid-template-columns: 1fr;
      }
    }

    .chart-card {
      background: white;
      padding: 24px;
      border-radius: 12px;
      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
    }

    .chart-card h3 {
      margin: 0 0 24px 0;
      color: #333;
      font-size: 18px;
    }

    /* Pie Chart Styles */
    .pie-chart-container {
      display: flex;
      gap: 24px;
      align-items: center;
    }

    .pie-chart {
      width: 120px;
      height: 120px;
      border-radius: 50%;
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .pie-chart-center {
      background: white;
      width: 80px;
      height: 80px;
      border-radius: 50%;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
    }

    .total-spent {
      font-size: 16px;
      font-weight: bold;
      color: #333;
    }

    .total-label {
      font-size: 10px;
      color: #666;
    }

    .pie-chart-legend {
      flex: 1;
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 8px;
      font-size: 12px;
    }

    .legend-color {
      width: 12px;
      height: 12px;
      border-radius: 2px;
    }

    .legend-label {
      flex: 1;
      color: #333;
    }

    .legend-amount {
      font-weight: bold;
      color: #333;
    }

    .legend-percentage {
      color: #666;
      font-size: 11px;
    }

    /* Bar Chart Styles */
    .bar-chart-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .bar-chart {
      display: flex;
      gap: 12px;
      height: 200px;
      align-items: flex-end;
      padding: 0 8px;
    }

    .bar-group {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
    }

    .bar-label {
      font-size: 12px;
      color: #666;
      writing-mode: horizontal-tb;
      text-align: center;
    }

    .bars {
      display: flex;
      gap: 2px;
      height: 150px;
      align-items: flex-end;
    }

    .bar {
      width: 12px;
      min-height: 4px;
      border-radius: 2px;
      transition: all 0.3s;
    }

    .spent-bar {
      background: #ff6b6b;
    }

    .received-bar {
      background: #51cf66;
    }

    .bar-values {
      display: flex;
      flex-direction: column;
      gap: 2px;
      font-size: 10px;
      text-align: center;
    }

    .spent-value {
      color: #ff6b6b;
    }

    .received-value {
      color: #51cf66;
    }

    .chart-legend {
      display: flex;
      justify-content: center;
      gap: 16px;
    }

    .chart-legend .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
    }

    .chart-legend .legend-color {
      width: 12px;
      height: 12px;
      border-radius: 2px;
    }

    .chart-legend .legend-color.spent {
      background: #ff6b6b;
    }

    .chart-legend .legend-color.received {
      background: #51cf66;
    }

    /* Transactions Styles */
    .transactions-card {
      background: white;
      border-radius: 12px;
      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
      overflow: hidden;
    }

    .transactions-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 24px 24px 16px 24px;
    }

    .transactions-header h3 {
      margin: 0;
      color: #333;
      font-size: 18px;
    }

    .view-all-btn {
      background: #667eea;
      color: white;
      border: none;
      padding: 8px 16px;
      border-radius: 6px;
      cursor: pointer;
      font-size: 14px;
      transition: background 0.2s;
    }

    .view-all-btn:hover {
      background: #5a6fd8;
    }

    .transactions-list {
      max-height: 400px;
      overflow-y: auto;
    }

    .transaction-item {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px 24px;
      border-bottom: 1px solid #f0f0f0;
      transition: background 0.2s;
    }

    .transaction-item:hover {
      background: #f8f9fa;
    }

    .transaction-item:last-child {
      border-bottom: none;
    }

    .transaction-icon {
      font-size: 20px;
      width: 32px;
      text-align: center;
    }

    .transaction-details {
      flex: 1;
    }

    .transaction-description {
      font-weight: 500;
      color: #333;
      margin-bottom: 4px;
    }

    .transaction-meta {
      display: flex;
      gap: 12px;
      font-size: 12px;
      color: #666;
      margin-bottom: 2px;
    }

    .counterparty {
      font-size: 12px;
      color: #888;
    }

    .transaction-amount {
      text-align: right;
    }

    .amount {
      display: block;
      font-weight: bold;
      font-size: 16px;
    }

    .amount.negative {
      color: #ff6b6b;
    }

    .amount.positive {
      color: #51cf66;
    }

    .status {
      display: block;
      font-size: 11px;
      margin-top: 2px;
      padding: 2px 6px;
      border-radius: 10px;
      text-transform: uppercase;
    }

    .status.completed {
      background: #d4edda;
      color: #155724;
    }

    .status.pending {
      background: #fff3cd;
      color: #856404;
    }

    .status.failed {
      background: #f8d7da;
      color: #721c24;
    }

    /* Loading Styles */
    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 200px;
      color: #666;
    }

    .loading-spinner {
      width: 40px;
      height: 40px;
      border: 4px solid #f3f3f3;
      border-top: 4px solid #667eea;
      border-radius: 50%;
      animation: spin 1s linear infinite;
      margin-bottom: 16px;
    }

    @keyframes spin {
      0% { transform: rotate(0deg); }
      100% { transform: rotate(360deg); }
    }
  `]
})
export class DashboardComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  user$ = this.authService.user$;

  dashboardData$: Observable<DashboardData> = timer(0, 30000).pipe(
    startWith(0),
    switchMap(() => this.accountService.getDashboardData())
  );

  private categoryColors = [
    '#ff6b6b', '#4ecdc4', '#45b7d1', '#96ceb4', '#feca57',
    '#ff9ff3', '#54a0ff', '#5f27cd', '#00d2d3', '#ff9f43'
  ];

  ngOnInit(): void {
    // Auto-refresh every 30 seconds
  }

  getCurrentTime(): Date {
    return new Date();
  }

  navigateToTransfer(): void {
    this.router.navigate(['/transfer']);
  }

  navigateToPayments(): void {
    this.router.navigate(['/payments']);
  }

  navigateToTransactions(): void {
    this.router.navigate(['/transactions']);
  }

  refreshData(): void {
    this.accountService.refresh();
    // Trigger manual refresh
    this.dashboardData$ = this.accountService.getDashboardData();
  }

  getPieChartBackground(categories: SpendingByCategory[]): string {
    if (!categories.length) return '#f0f0f0';

    let angle = 0;
    const segments = categories.map((category, index) => {
      const startAngle = angle;
      const endAngle = angle + (category.percentage / 100) * 360;
      angle = endAngle;

      return `${this.getCategoryColor(index)} ${startAngle}deg ${endAngle}deg`;
    });

    return `conic-gradient(${segments.join(', ')})`;
  }

  getCategoryColor(index: number): string {
    return this.categoryColors[index % this.categoryColors.length];
  }

  getTotalSpent(categories: SpendingByCategory[]): number {
    return categories.reduce((total, category) => total + category.amount, 0);
  }

  getBarHeight(value: number, data: MonthlySpending[]): number {
    const maxValue = Math.max(
      ...data.map(d => Math.max(d.totalSpent, d.totalReceived))
    );
    return maxValue > 0 ? (value / maxValue) * 100 : 0;
  }

  getMonthName(monthStr: string): string {
    const [year, month] = monthStr.split('-');
    const date = new Date(parseInt(year), parseInt(month) - 1);
    return date.toLocaleDateString('en', { month: 'short' });
  }
}
