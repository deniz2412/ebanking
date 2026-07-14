import {Component, inject, OnInit, signal} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormBuilder, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {Router} from '@angular/router';
import {debounceTime, distinctUntilChanged, filter} from 'rxjs/operators';

import {PaymentTemplate, TransferRequest, TransferService} from '../core/services/transfer.service';
import {
  amountAsyncValidator,
  amountRangeValidator,
  ibanAsyncValidator,
  ibanFormatValidator,
  recipientAsyncValidator
} from '../core/validators/transfer.validators';

@Component({
  selector: 'app-transfer',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="transfer-container">
      <div class="transfer-card">
        <header class="transfer-header">
          <h1>New Transfer</h1>
          <p>Send money to another account</p>
        </header>

        <!-- Payment Templates -->
        <section class="templates-section" *ngIf="paymentTemplates().length > 0">
          <h3>Quick Templates</h3>
          <div class="templates-grid">
            <button
              class="template-btn"
              *ngFor="let template of paymentTemplates()"
              (click)="applyTemplate(template)"
              type="button"
            >
              <div class="template-name">{{ template.name }}</div>
              <div class="template-recipient">{{ template.recipientName }}</div>
              <div class="template-amount" *ngIf="template.defaultAmount">
                €{{ template.defaultAmount | number:'1.2-2' }}
              </div>
            </button>
          </div>
        </section>

        <!-- Transfer Form -->
        <form [formGroup]="transferForm" (ngSubmit)="onSubmit()" novalidate>
          <!-- Recipient IBAN -->
          <div class="form-group">
            <label for="recipientIban">Recipient IBAN *</label>
            <input
              id="recipientIban"
              type="text"
              formControlName="recipientIban"
              placeholder="DE89 3704 0044 0532 0130 00"
              [class.invalid]="isFieldInvalid('recipientIban')"
              [class.valid]="isFieldValid('recipientIban')"
              [class.validating]="isFieldValidating('recipientIban')"
              (input)="onIbanInput($event)"
              autocomplete="off"
              maxlength="34"
            >
            <div class="field-status">
              <span class="validating-indicator" *ngIf="isFieldValidating('recipientIban')">
                Validating IBAN...
              </span>
              <span class="valid-indicator" *ngIf="isFieldValid('recipientIban')">
                ✓ Valid IBAN
              </span>
            </div>
            <div class="form-errors" *ngIf="getFieldErrors('recipientIban') as errors">
              <div class="error" *ngFor="let error of errors">{{ error }}</div>
            </div>
          </div>

          <!-- Recipient Name -->
          <div class="form-group">
            <label for="recipientName">Recipient Name *</label>
            <input
              id="recipientName"
              type="text"
              formControlName="recipientName"
              placeholder="John Doe"
              [class.invalid]="isFieldInvalid('recipientName')"
              [class.valid]="isFieldValid('recipientName')"
              [class.validating]="isFieldValidating('recipientName')"
              autocomplete="name"
              maxlength="70"
            >
            <div class="field-status">
              <span class="validating-indicator" *ngIf="isFieldValidating('recipientName')">
                Validating recipient...
              </span>
              <span class="valid-indicator" *ngIf="isFieldValid('recipientName')">
                ✓ Valid recipient
              </span>
            </div>
            <div class="form-errors" *ngIf="getFieldErrors('recipientName') as errors">
              <div class="error" *ngFor="let error of errors">{{ error }}</div>
            </div>
          </div>

          <!-- Amount -->
          <div class="form-group">
            <label for="amount">Amount (EUR) *</label>
            <div class="amount-input-container">
              <span class="currency-symbol">€</span>
              <input
                id="amount"
                type="text"
                formControlName="amount"
                placeholder="0.00"
                [class.invalid]="isFieldInvalid('amount')"
                [class.valid]="isFieldValid('amount')"
                [class.validating]="isFieldValidating('amount')"
                (input)="onAmountInput($event)"
                autocomplete="off"
              >
            </div>
            <div class="field-status">
              <span class="validating-indicator" *ngIf="isFieldValidating('amount')">
                Checking limits...
              </span>
              <span class="valid-indicator" *ngIf="isFieldValid('amount')">
                ✓ Amount within limits
              </span>
            </div>
            <div class="form-errors" *ngIf="getFieldErrors('amount') as errors">
              <div class="error" *ngFor="let error of errors">{{ error }}</div>
            </div>
            <div class="form-warnings" *ngIf="getFieldWarnings('amount') as warnings">
              <div class="warning" *ngFor="let warning of warnings">⚠ {{ warning }}</div>
            </div>
          </div>

          <!-- Description -->
          <div class="form-group">
            <label for="description">Description *</label>
            <input
              id="description"
              type="text"
              formControlName="description"
              placeholder="Payment description"
              [class.invalid]="isFieldInvalid('description')"
              maxlength="140"
            >
            <div class="character-count">
              {{ transferForm.get('description')?.value?.length || 0 }}/140
            </div>
            <div class="form-errors" *ngIf="getFieldErrors('description') as errors">
              <div class="error" *ngFor="let error of errors">{{ error }}</div>
            </div>
          </div>

          <!-- Transfer Options -->
          <div class="form-group">
            <div class="checkbox-group">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  formControlName="isUrgent"
                >
                <span class="checkmark"></span>
                Urgent transfer (+€2.50 fee)
              </label>
            </div>
          </div>

          <!-- Scheduled Date -->
          <div class="form-group" *ngIf="!transferForm.get('isUrgent')?.value">
            <label for="scheduledDate">Scheduled Date (Optional)</label>
            <input
              id="scheduledDate"
              type="date"
              formControlName="scheduledDate"
              [min]="getMinDate()"
              [max]="getMaxDate()"
            >
            <small class="field-hint">
              Leave empty for immediate transfer
            </small>
          </div>

          <!-- Save as Template -->
          <div class="form-group">
            <div class="checkbox-group">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  formControlName="saveAsTemplate"
                >
                <span class="checkmark"></span>
                Save as payment template
              </label>
            </div>
          </div>

          <!-- Template Name -->
          <div class="form-group" *ngIf="transferForm.get('saveAsTemplate')?.value">
            <label for="templateName">Template Name</label>
            <input
              id="templateName"
              type="text"
              formControlName="templateName"
              placeholder="e.g., Monthly Rent"
              maxlength="50"
            >
          </div>

          <!-- Transfer Summary -->
          <div class="transfer-summary" *ngIf="transferForm.valid">
            <h3>Transfer Summary</h3>
            <div class="summary-item">
              <span>Recipient:</span>
              <span>{{ transferForm.get('recipientName')?.value }}</span>
            </div>
            <div class="summary-item">
              <span>Amount:</span>
              <span>€{{ transferForm.get('amount')?.value | number:'1.2-2' }}</span>
            </div>
            <div class="summary-item" *ngIf="transferForm.get('isUrgent')?.value">
              <span>Urgent fee:</span>
              <span>€2.50</span>
            </div>
            <div class="summary-item total">
              <span>Total:</span>
              <span>€{{ getTotalAmount() | number:'1.2-2' }}</span>
            </div>
          </div>

          <!-- Form Actions -->
          <div class="form-actions">
            <button type="button" class="btn btn-secondary" (click)="onCancel()">
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              [disabled]="!transferForm.valid || isSubmitting()"
            >
              <span *ngIf="!isSubmitting()">Send Transfer</span>
              <span *ngIf="isSubmitting()">Processing...</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .transfer-container {
      max-width: 600px;
      margin: 0 auto;
      padding: 20px;
      min-height: 100vh;
      background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
    }

    .transfer-card {
      background: white;
      border-radius: 16px;
      padding: 32px;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.1);
    }

    .transfer-header {
      text-align: center;
      margin-bottom: 32px;
    }

    .transfer-header h1 {
      margin: 0 0 8px 0;
      color: #333;
      font-size: 28px;
    }

    .transfer-header p {
      margin: 0;
      color: #666;
      font-size: 16px;
    }

    .templates-section {
      margin-bottom: 32px;
      padding-bottom: 24px;
      border-bottom: 1px solid #e0e0e0;
    }

    .templates-section h3 {
      margin: 0 0 16px 0;
      color: #333;
      font-size: 18px;
    }

    .templates-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 12px;
    }

    .template-btn {
      background: #f8f9fa;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      padding: 16px;
      text-align: left;
      cursor: pointer;
      transition: all 0.2s;
    }

    .template-btn:hover {
      background: #e9ecef;
      border-color: #667eea;
    }

    .template-name {
      font-weight: 500;
      color: #333;
      margin-bottom: 4px;
    }

    .template-recipient {
      font-size: 14px;
      color: #666;
      margin-bottom: 4px;
    }

    .template-amount {
      font-size: 14px;
      color: #667eea;
      font-weight: 500;
    }

    .form-group {
      margin-bottom: 24px;
    }

    label {
      display: block;
      margin-bottom: 8px;
      font-weight: 500;
      color: #333;
    }

    input[type="text"], input[type="date"] {
      width: 100%;
      padding: 12px 16px;
      border: 2px solid #e0e0e0;
      border-radius: 8px;
      font-size: 16px;
      transition: all 0.2s;
      background: white;
    }

    input:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
    }

    input.invalid {
      border-color: #ff6b6b;
    }

    input.valid {
      border-color: #51cf66;
    }

    input.validating {
      border-color: #ffd43b;
    }

    .amount-input-container {
      position: relative;
    }

    .currency-symbol {
      position: absolute;
      left: 16px;
      top: 50%;
      transform: translateY(-50%);
      color: #666;
      font-weight: 500;
      z-index: 1;
    }

    .amount-input-container input {
      padding-left: 40px;
    }

    .field-status {
      margin-top: 4px;
      font-size: 14px;
      min-height: 20px;
    }

    .validating-indicator {
      color: #ffd43b;
    }

    .valid-indicator {
      color: #51cf66;
    }

    .form-errors {
      margin-top: 4px;
    }

    .error {
      color: #ff6b6b;
      font-size: 14px;
      margin-bottom: 4px;
    }

    .form-warnings {
      margin-top: 4px;
    }

    .warning {
      color: #ffd43b;
      font-size: 14px;
      margin-bottom: 4px;
    }

    .character-count {
      text-align: right;
      font-size: 12px;
      color: #666;
      margin-top: 4px;
    }

    .checkbox-group {
      display: flex;
      align-items: center;
    }

    .checkbox-label {
      display: flex;
      align-items: center;
      cursor: pointer;
      font-weight: normal;
      margin: 0;
    }

    .checkbox-label input[type="checkbox"] {
      width: auto;
      margin-right: 8px;
    }

    .field-hint {
      display: block;
      font-size: 12px;
      color: #666;
      margin-top: 4px;
    }

    .transfer-summary {
      background: #f8f9fa;
      border-radius: 8px;
      padding: 20px;
      margin: 24px 0;
    }

    .transfer-summary h3 {
      margin: 0 0 16px 0;
      color: #333;
      font-size: 18px;
    }

    .summary-item {
      display: flex;
      justify-content: space-between;
      margin-bottom: 8px;
      font-size: 14px;
    }

    .summary-item.total {
      border-top: 1px solid #e0e0e0;
      padding-top: 8px;
      margin-top: 8px;
      font-weight: 500;
      font-size: 16px;
    }

    .form-actions {
      display: flex;
      gap: 16px;
      justify-content: flex-end;
      margin-top: 32px;
    }

    .btn {
      padding: 12px 24px;
      border: none;
      border-radius: 8px;
      font-size: 16px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-secondary {
      background: #e9ecef;
      color: #495057;
    }

    .btn-secondary:hover:not(:disabled) {
      background: #dee2e6;
    }

    .btn-primary {
      background: #667eea;
      color: white;
    }

    .btn-primary:hover:not(:disabled) {
      background: #5a6fd8;
    }

    /* IBAN formatting styles */
    input[formControlName="recipientIban"] {
      font-family: 'Courier New', monospace;
      letter-spacing: 1px;
    }

    /* Amount formatting styles */
    input[formControlName="amount"] {
      text-align: right;
      font-weight: 500;
    }
  `]
})
export class TransferComponent implements OnInit {
  private readonly transferService = inject(TransferService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  paymentTemplates = signal<PaymentTemplate[]>([]);
  isSubmitting = signal(false);

  transferForm: FormGroup;

  constructor() {
    this.transferForm = this.fb.group({
      recipientIban: ['', {
        validators: [Validators.required, ibanFormatValidator],
        asyncValidators: [ibanAsyncValidator()],
        updateOn: 'blur'
      }],
      recipientName: ['', {
        validators: [Validators.required, Validators.minLength(2), Validators.maxLength(70)],
        updateOn: 'blur'
      }],
      amount: ['', {
        validators: [Validators.required, amountRangeValidator(0.01, 100000)],
        asyncValidators: [amountAsyncValidator()],
        updateOn: 'blur'
      }],
      description: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(140)]],
      isUrgent: [false],
      scheduledDate: [''],
      saveAsTemplate: [false],
      templateName: ['']
    });

    // Add async validator for recipient name after IBAN is valid
    this.transferForm.get('recipientName')?.setAsyncValidators([
      recipientAsyncValidator(this.transferForm.get('recipientIban')!)
    ]);
  }

  ngOnInit(): void {
    this.loadPaymentTemplates();
    this.setupFormValidation();
  }

  private loadPaymentTemplates(): void {
    this.transferService.getPaymentTemplates().subscribe({
      next: (templates) => this.paymentTemplates.set(templates),
      error: (error) => console.error('Failed to load payment templates:', error)
    });
  }

  private setupFormValidation(): void {
    // Trigger recipient validation when IBAN changes
    this.transferForm.get('recipientIban')?.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      filter(iban => iban && this.transferForm.get('recipientIban')?.valid)
    ).subscribe(() => {
      const recipientControl = this.transferForm.get('recipientName');
      if (recipientControl?.value) {
        recipientControl.updateValueAndValidity();
      }
    });

    // Template name required when save as template is checked
    this.transferForm.get('saveAsTemplate')?.valueChanges.subscribe(saveAsTemplate => {
      const templateNameControl = this.transferForm.get('templateName');
      if (saveAsTemplate) {
        templateNameControl?.setValidators([Validators.required, Validators.minLength(2)]);
      } else {
        templateNameControl?.clearValidators();
      }
      templateNameControl?.updateValueAndValidity();
    });

    // Clear scheduled date when urgent is selected
    this.transferForm.get('isUrgent')?.valueChanges.subscribe(isUrgent => {
      if (isUrgent) {
        this.transferForm.patchValue({ scheduledDate: '' });
      }
    });
  }

  onIbanInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\s/g, '').toUpperCase();

    // Add spaces every 4 characters for readability
    const formatted = value.replace(/(.{4})/g, '$1 ').trim();

    // Update the form control without triggering validation immediately
    this.transferForm.get('recipientIban')?.setValue(value, { emitEvent: false });

    // Update the input display value
    input.value = formatted;
  }

  onAmountInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/[^\d.,]/g, '');

    // Replace comma with dot for decimal
    value = value.replace(',', '.');

    // Ensure only one decimal point
    const parts = value.split('.');
    if (parts.length > 2) {
      value = parts[0] + '.' + parts.slice(1).join('');
    }

    // Limit decimal places to 2
    if (parts[1] && parts[1].length > 2) {
      value = parts[0] + '.' + parts[1].substring(0, 2);
    }

    this.transferForm.get('amount')?.setValue(value);
  }

  applyTemplate(template: PaymentTemplate): void {
    this.transferForm.patchValue({
      recipientIban: template.recipientIban,
      recipientName: template.recipientName,
      amount: template.defaultAmount || '',
      description: template.description
    });
  }

  isFieldInvalid(fieldName: string): boolean {
    const field = this.transferForm.get(fieldName);
    return !!(field && field.invalid && (field.dirty || field.touched));
  }

  isFieldValid(fieldName: string): boolean {
    const field = this.transferForm.get(fieldName);
    return !!(field && field.valid && (field.dirty || field.touched) && field.value);
  }

  isFieldValidating(fieldName: string): boolean {
    const field = this.transferForm.get(fieldName);
    return !!(field && field.pending);
  }

  getFieldErrors(fieldName: string): string[] {
    const field = this.transferForm.get(fieldName);
    if (!field || !field.errors || !this.isFieldInvalid(fieldName)) {
      return [];
    }

    const errors: string[] = [];
    const fieldErrors = field.errors;

    if (fieldErrors['required']) {
      errors.push('This field is required');
    }
    if (fieldErrors['minlength']) {
      errors.push(`Minimum length is ${fieldErrors['minlength'].requiredLength} characters`);
    }
    if (fieldErrors['maxlength']) {
      errors.push(`Maximum length is ${fieldErrors['maxlength'].requiredLength} characters`);
    }
    if (fieldErrors['ibanFormat']) {
      errors.push(fieldErrors['ibanFormat'].message);
    }
    if (fieldErrors['ibanLength']) {
      errors.push(fieldErrors['ibanLength'].message);
    }
    if (fieldErrors['ibanInvalid']) {
      errors.push(...fieldErrors['ibanInvalid'].errors);
    }
    if (fieldErrors['recipientInvalid']) {
      errors.push(...fieldErrors['recipientInvalid'].errors);
    }
    if (fieldErrors['amountFormat']) {
      errors.push(fieldErrors['amountFormat'].message);
    }
    if (fieldErrors['amountMin']) {
      errors.push(fieldErrors['amountMin'].message);
    }
    if (fieldErrors['amountMax']) {
      errors.push(fieldErrors['amountMax'].message);
    }
    if (fieldErrors['amountInvalid']) {
      errors.push(...fieldErrors['amountInvalid'].errors);
    }

    return errors;
  }

  getFieldWarnings(fieldName: string): string[] {
    const field = this.transferForm.get(fieldName);
    if (!field || !field.errors) {
      return [];
    }

    const warnings: string[] = [];
    const fieldErrors = field.errors;

    if (fieldErrors['ibanInvalid'] && fieldErrors['ibanInvalid'].warnings) {
      warnings.push(...fieldErrors['ibanInvalid'].warnings);
    }
    if (fieldErrors['amountInvalid'] && fieldErrors['amountInvalid'].warnings) {
      warnings.push(...fieldErrors['amountInvalid'].warnings);
    }

    return warnings;
  }

  getTotalAmount(): number {
    const amount = parseFloat(this.transferForm.get('amount')?.value || '0');
    const urgentFee = this.transferForm.get('isUrgent')?.value ? 2.50 : 0;
    return amount + urgentFee;
  }

  getMinDate(): string {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    return tomorrow.toISOString().split('T')[0];
  }

  getMaxDate(): string {
    const maxDate = new Date();
    maxDate.setDate(maxDate.getDate() + 365); // 1 year from now
    return maxDate.toISOString().split('T')[0];
  }

  async onSubmit(): Promise<void> {
    if (this.transferForm.invalid || this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);

    try {
      const formValue = this.transferForm.value;
      const transferRequest: TransferRequest = {
        recipientIban: formValue.recipientIban.replace(/\s/g, ''),
        recipientName: formValue.recipientName,
        amount: parseFloat(formValue.amount),
        currency: 'EUR',
        description: formValue.description,
        isUrgent: formValue.isUrgent,
        scheduledDate: formValue.scheduledDate || undefined
      };

      // Generate idempotency key
      const idempotencyKey = `transfer-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

      const response = await this.transferService.createTransfer(transferRequest, idempotencyKey).toPromise();

      if (response) {
        // Save as template if requested
        if (formValue.saveAsTemplate && formValue.templateName) {
          try {
            await this.transferService.createPaymentTemplate({
              name: formValue.templateName,
              recipientName: formValue.recipientName,
              recipientIban: formValue.recipientIban.replace(/\s/g, ''),
              defaultAmount: parseFloat(formValue.amount),
              description: formValue.description,
              category: 'transfer',
              isActive: true
            }).toPromise();
          } catch (error) {
            console.error('Failed to save template:', error);
          }
        }

        // Navigate to success page or transactions
        this.router.navigate(['/transactions'], {
          queryParams: { transferId: response.transferId }
        });
      }
    } catch (error: any) {
      console.error('Transfer failed:', error);
      // Show error message to user
      alert(error.error?.message || 'Transfer failed. Please try again.');
    } finally {
      this.isSubmitting.set(false);
    }
  }

  onCancel(): void {
    this.router.navigate(['/dashboard']);
  }
}
