import {Component, inject, OnInit, signal} from '@angular/core';
import {CommonModule, CurrencyPipe, DatePipe} from '@angular/common';
import {FormBuilder, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {ActivatedRoute, Router} from '@angular/router';
import {debounceTime, distinctUntilChanged, startWith} from 'rxjs/operators';
import {combineLatest} from 'rxjs';

import {AccountService, Transaction} from '../core/services/account.service';

export interface TransactionFilters {
  startDate?: string;
  endDate?: string;
  category?: string;
  type?: 'debit' | 'credit';
  minAmount?: number;
  maxAmount?: number;
  search?: string;
}

@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CurrencyPipe, DatePipe],
  template: `
    <div class="transactions-container">
      <div class="transactions-header">
        <h1>Transaction History</h1>
        <button class="refresh-btn" (click)="refreshTransactions()">
          <span class="icon">🔄</span>
          Refresh
        </button>
      </div>

      <!-- Filters -->
      <div class="filters-card">
        <form [formGroup]="filtersForm" class="filters-form">
          <div class="filter-row">
            <div class="filter-group">
              <label for="search">Search</label>
              <input
                id="search"
                type="text"
                formControlName="search"
                placeholder="Description, recipient, amount..."
                class="search-input"
              >
            </div>
            <div class="filter-group">
              <label for="type">Type</label>
              <select id="type" formControlName="type">
                <option value="">All Transactions</option>
                <option value="credit">Incoming</option>
                <option value="debit">Outgoing</option>
              </select>
            </div>
            <div class="filter-group">
              <label for="category">Category</label>
              <select id="category" formControlName="category">
                <option value="">All Categories</option>
                <option *ngFor="let category of categories()" [value]="category">
                  {{ category }}
                </option>
              </select>
            </div>
          </div>

          <div class="filter-row">
            <div class="filter-group">
              <label for="startDate">From Date</label>
              <input
                id="startDate"
                type="date"
                formControlName="startDate"
                [max]="getMaxDate()"
              >
            </div>
            <div class="filter-group">
              <label for="endDate">To Date</label>
              <input
                id="endDate"
                type="date"
                formControlName="endDate"
                [max]="getMaxDate()"
              >
            </div>
            <div class="filter-group">
              <label for="minAmount">Min Amount (€)</label>
              <input
                id="minAmount"
                type="number"
                formControlName="minAmount"
                placeholder="0.00"
                min="0"
                step="0.01"
              >
            </div>
            <div class="filter-group">
              <label for="maxAmount">Max Amount (€)</label>
              <input
                id="maxAmount"
                type="number"
                formControlName="maxAmount"
                placeholder="10000.00"
                min="0"
                step="0.01"
              >
            </div>
          </div>

          <div class="filter-actions">
            <button type="button" class="btn btn-secondary" (click)="clearFilters()">
              Clear Filters
            </button>
            <div class="active-filters" *ngIf="hasActiveFilters()">
              <span class="active-filters-count">
                {{ getActiveFiltersCount() }} filter(s) active
              </span>
            </div>
          </div>
        </form>
      </div>

      <!-- Results Summary -->
      <div class="results-summary" *ngIf="transactionData() as data">
        <div class="summary-stats">
          <div class="stat-item">
            <span class="stat-label">Total Transactions:</span>
            <span class="stat-value">{{ data.totalElements }}</span>
          </div>
          <div class="stat-item" *ngIf="hasActiveFilters()">
            <span class="stat-label">Filtered Results:</span>
            <span class="stat-value">{{ data.content.length }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">Current Page:</span>
            <span class="stat-value">{{ data.number + 1 }} of {{ data.totalPages }}</span>
          </div>
        </div>
      </div>

      <!-- Transactions List -->
      <div class="transactions-list" *ngIf="transactionData() as data">
        <div
          class="transaction-card"
          *ngFor="let transaction of data.content; trackBy: trackByTransactionId"
          [class.highlighted]="isHighlighted(transaction.id)"
        >
          <div class="transaction-main">
            <div class="transaction-icon">
              <span *ngIf="transaction.type === 'debit'" class="icon-outgoing">📤</span>
              <span *ngIf="transaction.type === 'credit'" class="icon-incoming">📥</span>
            </div>

            <div class="transaction-details">
              <div class="transaction-description">
                {{ transaction.description }}
              </div>
              <div class="transaction-meta">
                <span class="transaction-date">
                  {{ transaction.createdAt | date:'MMM d, y HH:mm' }}
                </span>
                <span class="transaction-category" [class]="'category-' + transaction.category.toLowerCase()">
                  {{ transaction.category }}
                </span>
                <span class="transaction-status" [class]="'status-' + transaction.status">
                  {{ transaction.status | titlecase }}
                </span>
              </div>
              <div class="transaction-counterparty" *ngIf="transaction.counterpartyName">
                <strong>{{ getCounterpartyLabel(transaction.type) }}:</strong>
                {{ transaction.counterpartyName }}
                <span class="iban" *ngIf="transaction.counterpartyIban">
                  ({{ formatIban(transaction.counterpartyIban) }})
                </span>
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
            </div>
          </div>

          <!-- Expandable Details -->
          <div class="transaction-details-toggle" (click)="toggleTransactionDetails(transaction.id)">
            <span>{{ isExpanded(transaction.id) ? 'Hide' : 'Show' }} Details</span>
            <span class="toggle-icon">{{ isExpanded(transaction.id) ? '▲' : '▼' }}</span>
          </div>

          <div class="transaction-expanded" *ngIf="isExpanded(transaction.id)">
            <div class="detail-row">
              <span class="detail-label">Transaction ID:</span>
              <span class="detail-value">{{ transaction.id }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Account ID:</span>
              <span class="detail-value">{{ transaction.accountId }}</span>
            </div>
            <div class="detail-row" *ngIf="transaction.counterpartyIban">
              <span class="detail-label">{{ getCounterpartyLabel(transaction.type) }} IBAN:</span>
              <span class="detail-value iban-full">{{ transaction.counterpartyIban }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Currency:</span>
              <span class="detail-value">{{ transaction.currency }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Created:</span>
              <span class="detail-value">{{ transaction.createdAt | date:'full' }}</span>
            </div>
          </div>
        </div>

        <!-- Empty State -->
        <div class="empty-state" *ngIf="data.content.length === 0">
          <div class="empty-icon">📭</div>
          <h3>No transactions found</h3>
          <p *ngIf="hasActiveFilters()">
            Try adjusting your filters to see more results.
          </p>
          <p *ngIf="!hasActiveFilters()">
            You don't have any transactions yet.
          </p>
        </div>
      </div>

      <!-- Pagination -->
      <div class="pagination" *ngIf="transactionData() as data">
        <ng-container *ngIf="data && data.totalPages > 1">
          <button
            class="pagination-btn"
            [disabled]="data.number === 0"
            (click)="changePage(0)"
          >
            First
          </button>
          <button
            class="pagination-btn"
            [disabled]="data.number === 0"
            (click)="changePage(data.number - 1)"
          >
            Previous
          </button>

          <div class="pagination-info">
            <span>Page {{ data.number + 1 }} of {{ data.totalPages }}</span>
          </div>

          <button
            class="pagination-btn"
            [disabled]="data.number >= data.totalPages - 1"
            (click)="changePage(data.number + 1)"
          >
            Next
          </button>
          <button
            class="pagination-btn"
            [disabled]="data.number >= data.totalPages - 1"
            (click)="changePage(data.totalPages - 1)"
          >
            Last
          </button>
        </ng-container>
      </div>

      <!-- Page Size Selector -->
      <div class="page-size-selector">
        <label for="pageSize">Items per page:</label>
        <select id="pageSize" [value]="pageSize()" (change)="changePageSize($event)">
          <option value="10">10</option>
          <option value="20">20</option>
          <option value="50">50</option>
          <option value="100">100</option>
        </select>
      </div>

      <!-- Loading State -->
      <div class="loading-container" *ngIf="isLoading()">
        <div class="loading-spinner"></div>
        <p>Loading transactions...</p>
      </div>
    </div>
  `,
  styles: [`
    .transactions-container {
      max-width: 1000px;
      margin: 0 auto;
      padding: 20px;
      min-height: 100vh;
      background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
    }

    .transactions-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    .transactions-header h1 {
      margin: 0;
      color: #333;
      font-size: 28px;
    }

    .refresh-btn {
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

    .refresh-btn:hover {
      background: #5a6fd8;
    }

    .filters-card {
      background: white;
      border-radius: 12px;
      padding: 24px;
      margin-bottom: 24px;
      box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
    }

    .filters-form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .filter-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }

    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .filter-group label {
      font-weight: 500;
      color: #333;
      font-size: 14px;
    }

    .filter-group input,
    .filter-group select {
      padding: 8px 12px;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
      font-size: 14px;
    }

    .search-input {
      width: 100%;
    }

    .filter-actions {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 8px;
    }

    .btn {
      padding: 8px 16px;
      border: none;
      border-radius: 6px;
      font-size: 14px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .btn-secondary {
      background: #e9ecef;
      color: #495057;
    }

    .btn-secondary:hover {
      background: #dee2e6;
    }

    .active-filters-count {
      font-size: 12px;
      color: #667eea;
      font-weight: 500;
    }

    .results-summary {
      background: white;
      border-radius: 8px;
      padding: 16px;
      margin-bottom: 16px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
    }

    .summary-stats {
      display: flex;
      gap: 24px;
      flex-wrap: wrap;
    }

    .stat-item {
      display: flex;
      gap: 8px;
      align-items: center;
      font-size: 14px;
    }

    .stat-label {
      color: #666;
    }

    .stat-value {
      font-weight: 500;
      color: #333;
    }

    .transactions-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-bottom: 24px;
    }

    .transaction-card {
      background: white;
      border-radius: 12px;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
      overflow: hidden;
      transition: all 0.2s;
    }

    .transaction-card:hover {
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .transaction-card.highlighted {
      border-left: 4px solid #667eea;
    }

    .transaction-main {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 20px;
    }

    .transaction-icon {
      font-size: 24px;
      width: 40px;
      text-align: center;
    }

    .transaction-details {
      flex: 1;
    }

    .transaction-description {
      font-weight: 500;
      color: #333;
      margin-bottom: 8px;
      font-size: 16px;
    }

    .transaction-meta {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
      margin-bottom: 4px;
    }

    .transaction-date {
      color: #666;
      font-size: 14px;
    }

    .transaction-category {
      font-size: 12px;
      padding: 2px 8px;
      border-radius: 12px;
      font-weight: 500;
      text-transform: uppercase;
    }

    .category-salary { background: #e3f2fd; color: #1976d2; }
    .category-food { background: #fff3e0; color: #f57c00; }
    .category-transport { background: #f3e5f5; color: #7b1fa2; }
    .category-shopping { background: #e8f5e8; color: #388e3c; }
    .category-entertainment { background: #fce4ec; color: #c2185b; }
    .category-bills { background: #fff8e1; color: #f9a825; }
    .category-transfer { background: #e0f2f1; color: #00796b; }

    .transaction-status {
      font-size: 11px;
      padding: 2px 6px;
      border-radius: 10px;
      text-transform: uppercase;
      font-weight: 500;
    }

    .status-completed { background: #d4edda; color: #155724; }
    .status-pending { background: #fff3cd; color: #856404; }
    .status-failed { background: #f8d7da; color: #721c24; }

    .transaction-counterparty {
      font-size: 14px;
      color: #666;
    }

    .iban {
      font-family: 'Courier New', monospace;
      font-size: 12px;
      color: #888;
    }

    .transaction-amount {
      text-align: right;
    }

    .amount {
      font-weight: bold;
      font-size: 18px;
    }

    .amount.negative {
      color: #ff6b6b;
    }

    .amount.positive {
      color: #51cf66;
    }

    .transaction-details-toggle {
      padding: 12px 20px;
      background: #f8f9fa;
      border-top: 1px solid #e0e0e0;
      cursor: pointer;
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 14px;
      color: #667eea;
      transition: background 0.2s;
    }

    .transaction-details-toggle:hover {
      background: #e9ecef;
    }

    .toggle-icon {
      font-size: 12px;
    }

    .transaction-expanded {
      padding: 20px;
      background: #f8f9fa;
      border-top: 1px solid #e0e0e0;
    }

    .detail-row {
      display: flex;
      justify-content: space-between;
      margin-bottom: 8px;
      font-size: 14px;
    }

    .detail-label {
      color: #666;
      font-weight: 500;
    }

    .detail-value {
      color: #333;
    }

    .iban-full {
      font-family: 'Courier New', monospace;
      font-size: 12px;
    }

    .empty-state {
      text-align: center;
      padding: 60px 20px;
      color: #666;
    }

    .empty-icon {
      font-size: 48px;
      margin-bottom: 16px;
    }

    .empty-state h3 {
      margin: 0 0 8px 0;
      color: #333;
    }

    .pagination {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 8px;
      margin-bottom: 24px;
    }

    .pagination-btn {
      padding: 8px 16px;
      border: 1px solid #e0e0e0;
      background: white;
      border-radius: 6px;
      cursor: pointer;
      font-size: 14px;
      transition: all 0.2s;
    }

    .pagination-btn:hover:not(:disabled) {
      background: #f8f9fa;
      border-color: #667eea;
    }

    .pagination-btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .pagination-info {
      margin: 0 16px;
      font-size: 14px;
      color: #666;
    }

    .page-size-selector {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 8px;
      margin-bottom: 24px;
      font-size: 14px;
      color: #666;
    }

    .page-size-selector select {
      padding: 4px 8px;
      border: 1px solid #e0e0e0;
      border-radius: 4px;
    }

    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 60px 20px;
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
export class TransactionsComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  currentPage = signal(0);
  pageSize = signal(20);
  expandedTransactions = signal<Set<string>>(new Set());
  highlightedTransactionId = signal<string | null>(null);
  isLoading = signal(false);

  filtersForm: FormGroup;

  categories = signal([
    'Salary', 'Food', 'Transport', 'Shopping', 'Entertainment',
    'Bills', 'Transfer', 'Investment', 'Healthcare', 'Education'
  ]);

  transactionData = signal<{
    content: Transaction[];
    totalElements: number;
    totalPages: number;
    size: number;
    number: number;
  } | null>(null);

  constructor() {
    this.filtersForm = this.fb.group({
      search: [''],
      type: [''],
      category: [''],
      startDate: [''],
      endDate: [''],
      minAmount: [''],
      maxAmount: ['']
    });
  }

  ngOnInit(): void {
    this.setupFilterSubscription();
    this.checkForHighlightedTransaction();
    this.loadTransactions();
  }

  private setupFilterSubscription(): void {
    // Watch for filter changes and reload data
    combineLatest([
      this.filtersForm.valueChanges.pipe(
        startWith(this.filtersForm.value),
        debounceTime(300),
        distinctUntilChanged()
      ),
      // Reset to first page when filters change
    ]).subscribe(() => {
      this.currentPage.set(0);
      this.loadTransactions();
    });
  }

  private checkForHighlightedTransaction(): void {
    // Check for transferId query param to highlight specific transaction
    this.route.queryParams.subscribe(params => {
      if (params['transferId']) {
        this.highlightedTransactionId.set(params['transferId']);
        // Remove the query param after highlighting
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: {}
        });
      }
    });
  }

  private loadTransactions(): void {
    this.isLoading.set(true);

    const filters = this.getFilterValues();

    this.accountService.getTransactions(
      this.currentPage(),
      this.pageSize(),
      filters
    ).subscribe({
      next: (data) => {
        this.transactionData.set(data);
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error('Failed to load transactions:', error);
        this.isLoading.set(false);
      }
    });
  }

  private getFilterValues(): TransactionFilters {
    const formValue = this.filtersForm.value;
    const filters: TransactionFilters = {};

    if (formValue.search?.trim()) {
      filters.search = formValue.search.trim();
    }
    if (formValue.type && formValue.type !== '') {
      filters.type = formValue.type;
    }
    if (formValue.category) {
      filters.category = formValue.category;
    }
    if (formValue.startDate) {
      filters.startDate = formValue.startDate;
    }
    if (formValue.endDate) {
      filters.endDate = formValue.endDate;
    }
    if (formValue.minAmount && !isNaN(formValue.minAmount)) {
      filters.minAmount = parseFloat(formValue.minAmount);
    }
    if (formValue.maxAmount && !isNaN(formValue.maxAmount)) {
      filters.maxAmount = parseFloat(formValue.maxAmount);
    }

    return filters;
  }

  hasActiveFilters(): boolean {
    const filters = this.getFilterValues();
    return Object.keys(filters).length > 0;
  }

  getActiveFiltersCount(): number {
    return Object.keys(this.getFilterValues()).length;
  }

  clearFilters(): void {
    this.filtersForm.reset();
  }

  changePage(page: number): void {
    this.currentPage.set(page);
    this.loadTransactions();
  }

  changePageSize(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.pageSize.set(parseInt(select.value));
    this.currentPage.set(0);
    this.loadTransactions();
  }

  refreshTransactions(): void {
    this.loadTransactions();
  }

  toggleTransactionDetails(transactionId: string): void {
    const expanded = new Set(this.expandedTransactions());
    if (expanded.has(transactionId)) {
      expanded.delete(transactionId);
    } else {
      expanded.add(transactionId);
    }
    this.expandedTransactions.set(expanded);
  }

  isExpanded(transactionId: string): boolean {
    return this.expandedTransactions().has(transactionId);
  }

  isHighlighted(transactionId: string): boolean {
    return this.highlightedTransactionId() === transactionId;
  }

  getCounterpartyLabel(type: 'debit' | 'credit'): string {
    return type === 'debit' ? 'To' : 'From';
  }

  formatIban(iban: string): string {
    if (!iban) return '';
    // Show first 4 and last 4 characters with *** in between
    if (iban.length <= 8) return iban;
    return `${iban.substring(0, 4)}***${iban.substring(iban.length - 4)}`;
  }

  getMaxDate(): string {
    return new Date().toISOString().split('T')[0];
  }

  trackByTransactionId(index: number, transaction: Transaction): string {
    return transaction.id;
  }
}
