import {inject} from '@angular/core';
import {CanActivateFn, Router} from '@angular/router';
import {map, take} from 'rxjs/operators';
import {AuthService} from './auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Check if user is authenticated using the service method
  if (authService.isAuthenticated()) {
    return true;
  } else {
    // Redirect to home where login prompt will be shown
    router.navigate(['/']);
    return false;
  }
};

export const mfaGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.mfaChallenge$.pipe(
    take(1),
    map(challenge => {
      if (challenge) {
        // MFA challenge is active, redirect to MFA page
        router.navigate(['/mfa']);
        return false;
      } else {
        // No MFA challenge, proceed
        return true;
      }
    })
  );
};

export const guestGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.user$.pipe(
    take(1),
    map(user => {
      if (!user) {
        return true;
      } else {
        router.navigate(['/dashboard']);
        return false;
      }
    })
  );
};
