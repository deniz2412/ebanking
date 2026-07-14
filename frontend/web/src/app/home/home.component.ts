import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

/**
 * Golden-path home screen for the M1 demo: view balance (UC-02), make an internal
 * transfer (UC-04), and see the resulting notification (UC-12). All calls go through
 * the Ocelot gateway (via the dev proxy) with the Keycloak bearer token attached by
 * the auth interceptor.
 */
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="grid">
      <!-- Balance (UC-02) -->
      <section class="card balance-card">
        <h2>Balance</h2>
        <div *ngIf="balance() as b; else loadingBal">
          <div class="amount">{{ b.balance | number:'1.2-2' }} {{ b.currency }}</div>
          <div class="muted">Account {{ account()?.accountNumber }} · {{ account()?.iban }}</div>
        </div>
        <ng-template #loadingBal><div class="muted">Loading…</div></ng-template>
        <button class="link" (click)="loadAll()">↻ Refresh</button>
      </section>

      <!-- Transfer (UC-04) -->
      <section class="card">
        <h2>New transfer</h2>
        <form (ngSubmit)="submitTransfer()" #f="ngForm">
          <label>To IBAN
            <input name="toIban" [(ngModel)]="toIban" required placeholder="BA39..." />
          </label>
          <label>Recipient name
            <input name="toName" [(ngModel)]="toName" required placeholder="Jane Doe" />
          </label>
          <label>Amount (EUR)
            <input name="amount" type="number" min="0.01" step="0.01" [(ngModel)]="amount" required />
          </label>
          <label>Description
            <input name="desc" [(ngModel)]="description" placeholder="Rent" />
          </label>
          <button type="submit" [disabled]="submitting() || f.invalid">
            {{ submitting() ? 'Sending…' : 'Send transfer' }}
          </button>
        </form>
        <div class="ok" *ngIf="transferMsg()">{{ transferMsg() }}</div>
        <div class="err" *ngIf="errorMsg()">{{ errorMsg() }}</div>
      </section>

      <!-- Notifications (UC-12) -->
      <section class="card">
        <div class="row">
          <h2>Notifications</h2>
          <button class="link" (click)="loadNotifications()">↻</button>
        </div>
        <div *ngIf="notifications().length === 0" class="muted">No notifications yet.</div>
        <ul class="notif-list">
          <li *ngFor="let n of notifications()">
            <strong>{{ n.title }}</strong>
            <div class="muted">{{ n.body }}</div>
            <div class="tiny">{{ n.createdAt | date:'short' }} · {{ n.status }}</div>
          </li>
        </ul>
      </section>

      <!-- Recent transactions -->
      <section class="card wide">
        <h2>Recent transactions</h2>
        <table *ngIf="transactions().length; else noTx">
          <tr *ngFor="let t of transactions()">
            <td>{{ t.transactionDate | date:'shortDate' }}</td>
            <td>{{ t.description }}</td>
            <td>{{ t.counterpartyName }}</td>
            <td class="amt" [class.debit]="t.type==='DEBIT'">
              {{ t.type === 'DEBIT' ? '-' : '+' }}{{ t.amount | number:'1.2-2' }}
            </td>
          </tr>
        </table>
        <ng-template #noTx><div class="muted">No transactions.</div></ng-template>
      </section>
    </div>
  `,
  styles: [`
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
    .card { background:#fff; border-radius:12px; padding:20px; box-shadow:0 6px 20px rgba(0,0,0,.08); }
    .card.wide { grid-column: 1 / -1; }
    h2 { margin:0 0 12px; font-size:1.1rem; color:#2c3e50; }
    .amount { font-size:2rem; font-weight:700; color:#2c3e50; }
    .muted { color:#6c757d; font-size:.85rem; }
    .tiny { color:#adb5bd; font-size:.72rem; }
    label { display:block; margin:8px 0; font-size:.8rem; color:#495057; }
    input { width:100%; padding:8px; border:1px solid #ced4da; border-radius:6px; box-sizing:border-box; }
    button[type=submit] { margin-top:12px; padding:10px 18px; border:0; border-radius:6px; background:#667eea; color:#fff; cursor:pointer; }
    button[disabled] { opacity:.6; cursor:not-allowed; }
    .link { background:none;border:0;color:#667eea;cursor:pointer;padding:6px 0;font-size:.8rem; }
    .row { display:flex; justify-content:space-between; align-items:center; }
    .ok { color:#198754; margin-top:10px; }
    .err { color:#dc3545; margin-top:10px; }
    .notif-list { list-style:none; padding:0; margin:0; }
    .notif-list li { padding:8px 0; border-bottom:1px solid #f1f3f5; }
    table { width:100%; border-collapse:collapse; }
    td { padding:6px 8px; border-bottom:1px solid #f1f3f5; font-size:.85rem; }
    .amt { text-align:right; color:#198754; font-variant-numeric:tabular-nums; }
    .amt.debit { color:#dc3545; }
    @media (max-width:768px){ .grid{ grid-template-columns:1fr; } }
  `]
})
export class HomeComponent implements OnInit {
  private readonly http = inject(HttpClient);

  balance = signal<any | null>(null);
  account = signal<any | null>(null);
  transactions = signal<any[]>([]);
  notifications = signal<any[]>([]);
  submitting = signal(false);
  transferMsg = signal('');
  errorMsg = signal('');

  toIban = '';
  toName = '';
  amount: number | null = null;
  description = '';

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.http.get<any>('/api/accounts/me/balance').subscribe({
      next: b => this.balance.set(b),
      error: e => this.errorMsg.set('Failed to load balance: ' + (e.status || e.message))
    });
    this.http.get<any>('/api/accounts/me').subscribe({ next: a => this.account.set(a) });
    this.http.get<any>('/api/accounts/me/transactions?page=1&pageSize=8').subscribe({
      next: r => this.transactions.set(r.transactions ?? [])
    });
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.http.get<any>('/api/notifications?page=1&pageSize=8').subscribe({
      next: r => this.notifications.set(r.items ?? []),
      error: () => {}
    });
  }

  submitTransfer(): void {
    this.transferMsg.set('');
    this.errorMsg.set('');
    const acct = this.account();
    if (!acct) { this.errorMsg.set('Account not loaded yet.'); return; }

    this.submitting.set(true);
    const idempotencyKey = 'ui-' + Date.now();
    const payload = {
      fromAccountNumber: acct.accountNumber,
      toAccountNumber: this.toIban,
      toAccountName: this.toName,
      amount: this.amount,
      currency: 'EUR',
      description: this.description,
      type: 1 // Internal — settles immediately and emits transfer.completed
    };

    this.http.post<any>('/api/transfers', payload, {
      headers: { 'Idempotency-Key': idempotencyKey }
    }).subscribe({
      next: r => {
        this.submitting.set(false);
        this.transferMsg.set(`Transfer #${r.id} ${statusLabel(r.status)} — ${r.amount} ${r.currency} to ${r.toAccountName}.`);
        this.toIban = this.toName = this.description = ''; this.amount = null;
        // The notification/audit consumers process the event asynchronously.
        setTimeout(() => { this.loadNotifications(); this.loadAll(); }, 1500);
      },
      error: e => {
        this.submitting.set(false);
        this.errorMsg.set('Transfer failed: ' + (e.error?.error || e.status || e.message));
      }
    });
  }
}

function statusLabel(status: any): string {
  // Backend TransferStatus enum: 3 = Completed, 1 = Pending
  if (status === 3 || status === 'Completed') return 'completed';
  if (status === 1 || status === 'Pending') return 'pending';
  return String(status);
}
