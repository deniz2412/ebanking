import {inject, Injectable} from '@angular/core';
import {BehaviorSubject, firstValueFrom, Observable, throwError} from 'rxjs';
import {Router} from '@angular/router';
import {HttpClient} from '@angular/common/http';
import {catchError, map, tap} from 'rxjs/operators';

// Keycloak types
declare const Keycloak: any;

export interface UserProfile {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  mfaEnabled: boolean;
}

export interface MfaChallenge {
  challengeId: string;
  method: 'totp' | 'sms' | 'email';
  maskedTarget?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private keycloak: any;
  private userSubject = new BehaviorSubject<UserProfile | null>(null);
  private mfaChallengeSubject = new BehaviorSubject<MfaChallenge | null>(null);

  public readonly user$ = this.userSubject.asObservable();
  public readonly mfaChallenge$ = this.mfaChallengeSubject.asObservable();

  constructor() {
    // Dynamic import will be handled in init()
  }

  /** Load runtime config from /config.json, falling back to local-dev defaults. */
  private async loadRuntimeConfig(): Promise<{ keycloakUrl: string; realm: string; clientId: string }> {
    const defaults = { keycloakUrl: 'http://localhost:8180', realm: 'ebanking', clientId: 'ebanking-frontend' };
    try {
      const res = await fetch('/config.json', { cache: 'no-store' });
      if (!res.ok) return defaults;
      return { ...defaults, ...(await res.json()) };
    } catch {
      return defaults;
    }
  }

  /**
   * Initialize Keycloak with PKCE flow
   */
  async init(): Promise<boolean> {
    try {
      // Dynamic import of Keycloak
      const KeycloakModule = await import('keycloak-js');
      const KeycloakConstructor = KeycloakModule.default || KeycloakModule;

      // Runtime config (public/config.json) lets the same build run against local dev
      // (localhost:8180) or the compose/k8s stack (keycloak:8180) without a rebuild.
      const cfg = await this.loadRuntimeConfig();
      this.keycloak = new KeycloakConstructor({
        url: cfg.keycloakUrl,
        realm: cfg.realm,
        clientId: cfg.clientId
      });

      // No onLoad/silent-SSO iframe: the realm sets X-Frame-Options SAMEORIGIN, which
      // blocks framing Keycloak cross-origin. init() still exchanges the auth code that
      // Keycloak appends on redirect back to /dashboard, so login works via full redirect.
      // Guard with a timeout so a slow/unreachable IdP can never hang app bootstrap.
      const authenticated = await Promise.race([
        this.keycloak.init({
          pkceMethod: 'S256', // Enable PKCE
          flow: 'standard',
          checkLoginIframe: false,
          enableLogging: true
        }),
        new Promise<boolean>((resolve) => setTimeout(() => resolve(false), 8000))
      ]);

      if (authenticated) {
        // Guard this too: an unguarded hang here (e.g. a slow/unreachable profile
        // endpoint) would leave init() — and the app's loading spinner — stuck forever,
        // even though login itself already succeeded.
        await Promise.race([
          this.loadUserProfile(),
          new Promise<void>((resolve) => setTimeout(resolve, 5000))
        ]);
        this.setupTokenRefresh();
      }

      return authenticated;
    } catch (error) {
      console.error('Keycloak initialization failed:', error);
      return false;
    }
  }

  /**
   * Login with Keycloak
   */
  async login(): Promise<void> {
    try {
      await this.keycloak.login({
        redirectUri: window.location.origin + '/dashboard',
        // Request the API scopes so the issued token carries the service audiences
        // (account-service, transfer-service, notification-service).
        scope: 'openid profile email read:accounts write:transfers read:notifications'
      });
    } catch (error) {
      console.error('Login failed:', error);
      throw error;
    }
  }

  /**
   * Logout from Keycloak
   */
  async logout(): Promise<void> {
    try {
      this.userSubject.next(null);
      this.mfaChallengeSubject.next(null);

      await this.keycloak.logout({
        redirectUri: window.location.origin
      });
    } catch (error) {
      console.error('Logout failed:', error);
      throw error;
    }
  }

  /**
   * Get current access token
   */
  getToken(): string | undefined {
    return this.keycloak?.token;
  }

  /**
   * Return a valid access token, refreshing it first if it expires within 30s.
   * Used by the HTTP interceptor before each API call.
   */
  async ensureFreshToken(): Promise<string | undefined> {
    if (!this.keycloak?.authenticated) {
      return undefined;
    }
    try {
      await this.keycloak.updateToken(30);
    } catch {
      // Refresh failed (e.g. session expired) — fall through with whatever we have.
    }
    return this.keycloak.token;
  }

  /**
   * Check if user is authenticated
   */
  isAuthenticated(): boolean {
    return this.keycloak?.authenticated || false;
  }

  /**
   * Check if user has specific role
   */
  hasRole(role: string): boolean {
    return this.keycloak?.hasRealmRole(role) || false;
  }

  /**
   * Load user profile from Keycloak
   */
  private async loadUserProfile(): Promise<void> {
    try {
      const profile = await this.keycloak.loadUserProfile();
      const tokenParsed = this.keycloak.tokenParsed;

      const userProfile: UserProfile = {
        id: profile.id!,
        username: profile.username!,
        email: profile.email!,
        firstName: profile.firstName || '',
        lastName: profile.lastName || '',
        roles: tokenParsed?.realm_access?.roles || [],
        mfaEnabled: tokenParsed?.acr === '2' // ACR level 2 indicates MFA
      };

      this.userSubject.next(userProfile);

      // Check if MFA challenge is required
      if (this.shouldChallengeMfa(userProfile)) {
        await this.initiateMfaChallenge();
      }
    } catch (error) {
      console.error('Failed to load user profile:', error);
      throw error;
    }
  }

  /**
   * Check if MFA challenge should be initiated
   */
  private shouldChallengeMfa(user: UserProfile): boolean {
    // Challenge MFA for high-value operations or sensitive roles
    return user.roles.includes('premium_user') && !user.mfaEnabled;
  }

  /**
   * Initiate MFA challenge
   */
  async initiateMfaChallenge(): Promise<MfaChallenge> {
    try {
      const challenge = await firstValueFrom(
        this.http.post<MfaChallenge>('/api/auth/mfa/challenge', {})
          .pipe(
            tap(challenge => this.mfaChallengeSubject.next(challenge)),
            catchError(error => {
              console.error('MFA challenge failed:', error);
              return throwError(() => error);
            })
          )
      );
      return challenge;
    } catch (error) {
      console.error('MFA challenge failed:', error);
      throw error;
    }
  }

  /**
   * Verify MFA challenge
   */
  verifyMfaChallenge(challengeId: string, code: string): Observable<boolean> {
    return this.http.post<{verified: boolean}>('/api/auth/mfa/verify', {
      challengeId,
      code
    }).pipe(
      map(response => response.verified),
      tap(verified => {
        if (verified) {
          this.mfaChallengeSubject.next(null);
          // Reload user profile to update MFA status
          this.loadUserProfile();
        }
      }),
      catchError(error => {
        console.error('MFA verification failed:', error);
        return throwError(() => error);
      })
    );
  }

  /**
   * Setup automatic token refresh
   */
  private setupTokenRefresh(): void {
    // Refresh token when it's 30 seconds before expiry
    this.keycloak.onTokenExpired = () => {
      this.keycloak.updateToken(30)
        .then((refreshed: boolean) => {
          if (refreshed) {
            console.log('Token refreshed');
          }
        })
        .catch((error: any) => {
          console.error('Failed to refresh token:', error);
          this.logout();
        });
    };
  }

  /**
   * Update account security settings
   */
  updateSecuritySettings(settings: {mfaEnabled: boolean}): Observable<void> {
    return this.http.put<void>('/api/auth/security', settings)
      .pipe(
        tap(() => this.loadUserProfile()),
        catchError(error => {
          console.error('Failed to update security settings:', error);
          return throwError(() => error);
        })
      );
  }
}
