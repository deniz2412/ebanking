import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { from, switchMap } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the Keycloak access token as a Bearer header on API calls (routed to the
 * gateway via the dev proxy). Refreshes the token first if it is close to expiry.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Only attach the token to our own API calls.
  if (!req.url.startsWith('/api')) {
    return next(req);
  }

  const auth = inject(AuthService);

  return from(auth.ensureFreshToken()).pipe(
    switchMap(token => {
      const authReq = token
        ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
        : req;
      return next(authReq);
    })
  );
};
