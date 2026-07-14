import {AbstractControl, AsyncValidatorFn, ValidationErrors} from '@angular/forms';
import {Observable, of, timer} from 'rxjs';
import {catchError, map, switchMap} from 'rxjs/operators';
import {inject} from '@angular/core';
import {TransferService} from '../services/transfer.service';

/**
 * Async IBAN validator
 */
export function ibanAsyncValidator(): AsyncValidatorFn {
  return (control: AbstractControl): Observable<ValidationErrors | null> => {
    if (!control.value) {
      return of(null);
    }

    const transferService = inject(TransferService);

    return timer(500).pipe(
      switchMap(() => transferService.validateIban(control.value)),
      map(result => {
        if (result.isValid) {
          return null;
        }
        return {
          ibanInvalid: {
            errors: result.errors,
            warnings: result.warnings
          }
        };
      }),
      catchError(() => of({ ibanInvalid: { errors: ['Validation failed'] } }))
    );
  };
}

/**
 * Async recipient validator
 */
export function recipientAsyncValidator(ibanControl: AbstractControl): AsyncValidatorFn {
  return (control: AbstractControl): Observable<ValidationErrors | null> => {
    if (!control.value || !ibanControl.value) {
      return of(null);
    }

    const transferService = inject(TransferService);

    return timer(500).pipe(
      switchMap(() => transferService.validateRecipient(control.value, ibanControl.value)),
      map(result => {
        if (result.isValid) {
          return null;
        }
        return {
          recipientInvalid: {
            errors: result.errors,
            warnings: result.warnings
          }
        };
      }),
      catchError(() => of({ recipientInvalid: { errors: ['Validation failed'] } }))
    );
  };
}

/**
 * Async amount validator
 */
export function amountAsyncValidator(): AsyncValidatorFn {
  return (control: AbstractControl): Observable<ValidationErrors | null> => {
    if (!control.value || control.value <= 0) {
      return of(null);
    }

    const transferService = inject(TransferService);

    return timer(300).pipe(
      switchMap(() => transferService.checkTransferLimits(control.value)),
      map(result => {
        if (result.isValid) {
          return null;
        }
        return {
          amountInvalid: {
            errors: result.errors,
            warnings: result.warnings
          }
        };
      }),
      catchError(() => of({ amountInvalid: { errors: ['Validation failed'] } }))
    );
  };
}

/**
 * IBAN format validator (client-side)
 */
export function ibanFormatValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value) {
    return null;
  }

  const iban = control.value.replace(/\s/g, '').toUpperCase();

  // Basic format check
  if (!/^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$/.test(iban)) {
    return { ibanFormat: { message: 'Invalid IBAN format' } };
  }

  // Length check for specific countries
  const countryLengths: Record<string, number> = {
    AD: 24, AE: 23, AL: 28, AT: 20, AZ: 28, BA: 20, BE: 16, BG: 22,
    BH: 22, BR: 29, BY: 28, CH: 21, CR: 22, CY: 28, CZ: 24, DE: 22,
    DK: 18, DO: 28, EE: 20, EG: 29, ES: 24, FI: 18, FO: 18, FR: 27,
    GB: 22, GE: 22, GI: 23, GL: 18, GR: 27, GT: 28, HR: 21, HU: 28,
    IE: 22, IL: 23, IS: 26, IT: 27, JO: 30, KW: 30, KZ: 20, LB: 28,
    LC: 32, LI: 21, LT: 20, LU: 20, LV: 21, MC: 27, MD: 24, ME: 22,
    MK: 19, MR: 27, MT: 31, MU: 30, NL: 18, NO: 15, PK: 24, PL: 28,
    PS: 29, PT: 25, QA: 29, RO: 24, RS: 22, SA: 24, SE: 24, SI: 19,
    SK: 24, SM: 27, TN: 24, TR: 26, UA: 29, VG: 24, XK: 20
  };

  const countryCode = iban.substring(0, 2);
  const expectedLength = countryLengths[countryCode];

  if (expectedLength && iban.length !== expectedLength) {
    return {
      ibanLength: {
        message: `IBAN for ${countryCode} should be ${expectedLength} characters long`
      }
    };
  }

  return null;
}

/**
 * Amount range validator
 */
export function amountRangeValidator(min: number = 0.01, max: number = 100000) {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!control.value) {
      return null;
    }

    const amount = parseFloat(control.value);

    if (isNaN(amount)) {
      return { amountFormat: { message: 'Invalid amount format' } };
    }

    if (amount < min) {
      return { amountMin: { message: `Minimum amount is €${min}` } };
    }

    if (amount > max) {
      return { amountMax: { message: `Maximum amount is €${max}` } };
    }

    return null;
  };
}
