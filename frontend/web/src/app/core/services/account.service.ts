import {inject, Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {BehaviorSubject, combineLatest, Observable, of} from 'rxjs';
import {map, shareReplay} from 'rxjs/operators';

export interface AccountBalance {
  accountId: string;
  balance: number;
  availableBalance: number;
  currency: 'EUR';
  lastUpdated: string;
}

export interface Transaction {
  id: string;
  accountId: string;
  amount: number;
  currency: 'EUR';
  description: string;
  category: string;
  type: 'debit' | 'credit';
  status: 'completed' | 'pending' | 'failed';
  createdAt: string;
  counterpartyName?: string;
  counterpartyIban?: string;
}

export interface SpendingByCategory {
  category: string;
  amount: number;
  percentage: number;
  transactionCount: number;
}

export interface MonthlySpending {
  month: string;
  year: number;
  totalSpent: number;
  totalReceived: number;
  netFlow: number;
  transactionCount: number;
}

export interface DashboardData {
  balance: AccountBalance;
  recentTransactions: Transaction[];
  spendingByCategory: SpendingByCategory[];
  monthlyTrends: MonthlySpending[];
}

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private readonly http = inject(HttpClient);
  // Routed to the Ocelot gateway via the dev proxy (see proxy.conf.json).
  private readonly baseUrl = '/api/accounts';

  private refreshTrigger = new BehaviorSubject<void>(undefined);

  /**
   * Get current account balance. Maps the backend BalanceResponse
   * ({balance, currency, userId, timestamp}) to the UI shape.
   */
  getBalance(): Observable<AccountBalance> {
    return this.http.get<any>(`${this.baseUrl}/me/balance`).pipe(
      map(r => ({
        accountId: r.userId,
        balance: r.balance,
        availableBalance: r.balance,
        currency: (r.currency ?? 'EUR') as 'EUR',
        lastUpdated: r.timestamp
      } as AccountBalance)),
      shareReplay(1)
    );
  }

  /**
   * Get recent transactions with pagination
   */
  getTransactions(page = 0, size = 20, filters?: {
    startDate?: string;
    endDate?: string;
    category?: string;
    type?: 'debit' | 'credit';
    minAmount?: number;
    maxAmount?: number;
  }): Observable<{
    content: Transaction[];
    totalElements: number;
    totalPages: number;
    size: number;
    number: number;
  }> {
    // Backend pagination is 1-based; the UI is 0-based.
    let params: any = { page: page + 1, pageSize: size };

    if (filters) {
      Object.keys(filters).forEach(key => {
        const value = (filters as any)[key];
        if (value !== undefined && value !== null && value !== '') {
          params[key] = value;
        }
      });
    }

    return this.http.get<any>(`${this.baseUrl}/me/transactions`, { params }).pipe(
      map(r => ({
        content: (r.transactions ?? []).map((t: any) => ({
          id: String(t.id),
          accountId: '',
          amount: t.amount,
          currency: (t.currency ?? 'EUR') as 'EUR',
          description: t.description,
          category: t.type,
          type: (t.type ?? '').toLowerCase() === 'credit' ? 'credit' : 'debit',
          status: 'completed',
          createdAt: t.transactionDate,
          counterpartyName: t.counterpartyName,
          counterpartyIban: t.counterpartyAccount
        })),
        totalElements: r.totalCount ?? 0,
        totalPages: r.totalPages ?? 0,
        size: r.pageSize ?? size,
        number: (r.page ?? 1) - 1
      })),
      shareReplay(1)
    );
  }

  /**
   * Spending analytics endpoints are not implemented on the backend yet (M1).
   * Return empty sets so the dashboard renders without erroring.
   */
  getSpendingByCategory(months = 3): Observable<SpendingByCategory[]> {
    return of([]);
  }

  getMonthlyTrends(months = 12): Observable<MonthlySpending[]> {
    return of([]);
  }

  /**
   * Get comprehensive dashboard data
   */
  getDashboardData(): Observable<DashboardData> {
    return combineLatest([
      this.getBalance(),
      this.getTransactions(0, 10),
      this.getSpendingByCategory(3),
      this.getMonthlyTrends(6)
    ]).pipe(
      map(([balance, transactions, spendingByCategory, monthlyTrends]) => ({
        balance,
        recentTransactions: transactions.content,
        spendingByCategory,
        monthlyTrends
      })),
      shareReplay(1)
    );
  }

  /**
   * Download account statement as PDF
   */
  downloadStatement(year: number, month: number): Observable<Blob> {
    const yearMonth = `${year}-${month.toString().padStart(2, '0')}`;
    return this.http.get(`${this.baseUrl}/me/statements/${yearMonth}.pdf`, {
      responseType: 'blob'
    });
  }

  /**
   * Refresh all cached data
   */
  refresh(): void {
    this.refreshTrigger.next();
  }
}
