import {inject, Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable, of} from 'rxjs';
import {catchError, shareReplay} from 'rxjs/operators';

export interface TransferRequest {
  recipientIban: string;
  recipientName: string;
  amount: number;
  currency: 'EUR';
  description: string;
  isUrgent?: boolean;
  scheduledDate?: string;
}

export interface TransferResponse {
  transferId: string;
  status: 'pending' | 'completed' | 'failed';
  estimatedCompletion?: string;
  fee?: number;
}

export interface PaymentTemplate {
  id: string;
  name: string;
  recipientName: string;
  recipientIban: string;
  defaultAmount?: number;
  description: string;
  category: string;
  isActive: boolean;
  createdAt: string;
}

export interface ValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

@Injectable({
  providedIn: 'root'
})
export class TransferService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/transfer';

  /**
   * Validate IBAN with backend
   */
  validateIban(iban: string): Observable<ValidationResult> {
    if (!iban || iban.length < 15) {
      return of({
        isValid: false,
        errors: ['IBAN too short'],
        warnings: []
      });
    }

    return this.http.post<ValidationResult>(`${this.baseUrl}/validate-iban`, { iban })
      .pipe(
        catchError(() => of({
          isValid: false,
          errors: ['Unable to validate IBAN'],
          warnings: []
        }))
      );
  }

  /**
   * Validate recipient name
   */
  validateRecipient(name: string, iban: string): Observable<ValidationResult> {
    if (!name || !iban) {
      return of({
        isValid: false,
        errors: ['Name and IBAN required'],
        warnings: []
      });
    }

    return this.http.post<ValidationResult>(`${this.baseUrl}/validate-recipient`, {
      name,
      iban
    }).pipe(
      catchError(() => of({
        isValid: false,
        errors: ['Unable to validate recipient'],
        warnings: []
      }))
    );
  }

  /**
   * Check transfer limits
   */
  checkTransferLimits(amount: number): Observable<ValidationResult> {
    return this.http.post<ValidationResult>(`${this.baseUrl}/check-limits`, {
      amount
    }).pipe(
      catchError(() => of({
        isValid: false,
        errors: ['Unable to check transfer limits'],
        warnings: []
      }))
    );
  }

  /**
   * Create transfer
   */
  createTransfer(transfer: TransferRequest, idempotencyKey: string): Observable<TransferResponse> {
    return this.http.post<TransferResponse>(`${this.baseUrl}/transfers`, transfer, {
      headers: {
        'Idempotency-Key': idempotencyKey
      }
    });
  }

  /**
   * Get payment templates
   */
  getPaymentTemplates(): Observable<PaymentTemplate[]> {
    return this.http.get<PaymentTemplate[]>(`/api/payment/templates`)
      .pipe(shareReplay(1));
  }

  /**
   * Create payment template
   */
  createPaymentTemplate(template: Omit<PaymentTemplate, 'id' | 'createdAt'>): Observable<PaymentTemplate> {
    return this.http.post<PaymentTemplate>(`/api/payment/templates`, template);
  }

  /**
   * Get exchange rates (for future multi-currency support)
   */
  getExchangeRates(): Observable<Record<string, number>> {
    return this.http.get<Record<string, number>>('/api/exchange-rates')
      .pipe(
        catchError(() => of({ EUR: 1 }))
      );
  }
}
