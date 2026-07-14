import {inject, Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {BehaviorSubject, Observable} from 'rxjs';
import {catchError, tap} from 'rxjs/operators';

export interface PushSubscriptionData {
  endpoint: string;
  keys: {
    p256dh: string;
    auth: string;
  };
}

export interface NotificationSettings {
  transactionAlerts: boolean;
  securityAlerts: boolean;
  marketingNotifications: boolean;
  maintenanceAlerts: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class PushNotificationService {
  private readonly http = inject(HttpClient);

  private isSupported = 'serviceWorker' in navigator && 'PushManager' in window;
  private swRegistration: ServiceWorkerRegistration | null = null;

  private subscriptionSubject = new BehaviorSubject<PushSubscription | null>(null);
  private permissionSubject = new BehaviorSubject<NotificationPermission>('default');
  private settingsSubject = new BehaviorSubject<NotificationSettings>({
    transactionAlerts: true,
    securityAlerts: true,
    marketingNotifications: false,
    maintenanceAlerts: true
  });

  public readonly subscription$ = this.subscriptionSubject.asObservable();
  public readonly permission$ = this.permissionSubject.asObservable();
  public readonly settings$ = this.settingsSubject.asObservable();

  constructor() {
    if (this.isSupported) {
      this.initializeServiceWorker();
      this.checkExistingSubscription();
      this.loadNotificationSettings();
    }
  }

  /**
   * Check if push notifications are supported
   */
  isSupported$(): boolean {
    return this.isSupported;
  }

  /**
   * Initialize service worker
   */
  private async initializeServiceWorker(): Promise<void> {
    try {
      this.swRegistration = await navigator.serviceWorker.register('/sw.js', {
        scope: '/'
      });

      console.log('Service Worker registered successfully');

      // Update permission status
      this.permissionSubject.next(Notification.permission);

    } catch (error) {
      console.error('Service Worker registration failed:', error);
    }
  }

  /**
   * Check for existing push subscription
   */
  private async checkExistingSubscription(): Promise<void> {
    if (!this.swRegistration) return;

    try {
      const subscription = await this.swRegistration.pushManager.getSubscription();
      this.subscriptionSubject.next(subscription);
    } catch (error) {
      console.error('Failed to get existing subscription:', error);
    }
  }

  /**
   * Request notification permission
   */
  async requestPermission(): Promise<NotificationPermission> {
    if (!this.isSupported) {
      throw new Error('Push notifications are not supported');
    }

    try {
      const permission = await Notification.requestPermission();
      this.permissionSubject.next(permission);
      return permission;
    } catch (error) {
      console.error('Failed to request notification permission:', error);
      throw error;
    }
  }

  /**
   * Subscribe to push notifications
   */
  async subscribe(): Promise<PushSubscription> {
    if (!this.swRegistration) {
      throw new Error('Service Worker not registered');
    }

    if (Notification.permission !== 'granted') {
      const permission = await this.requestPermission();
      if (permission !== 'granted') {
        throw new Error('Notification permission denied');
      }
    }

    try {
      // Get VAPID public key from server
      const vapidPublicKey = await this.getVapidPublicKey();

      const subscription = await this.swRegistration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: this.urlBase64ToUint8Array(vapidPublicKey) as BufferSource
      });

      // Send subscription to server
      await this.sendSubscriptionToServer(subscription);

      this.subscriptionSubject.next(subscription);
      return subscription;

    } catch (error) {
      console.error('Failed to subscribe to push notifications:', error);
      throw error;
    }
  }

  /**
   * Unsubscribe from push notifications
   */
  async unsubscribe(): Promise<void> {
    const subscription = this.subscriptionSubject.value;
    if (!subscription) {
      return;
    }

    try {
      await subscription.unsubscribe();
      await this.removeSubscriptionFromServer(subscription);
      this.subscriptionSubject.next(null);
    } catch (error) {
      console.error('Failed to unsubscribe from push notifications:', error);
      throw error;
    }
  }

  /**
   * Update notification settings
   */
  updateSettings(settings: NotificationSettings): Observable<void> {
    return this.http.put<void>('/api/notification/settings', settings)
      .pipe(
        tap(() => {
          this.settingsSubject.next(settings);
          this.saveNotificationSettings(settings);
        }),
        catchError(error => {
          console.error('Failed to update notification settings:', error);
          throw error;
        })
      );
  }

  /**
   * Test push notification
   */
  testNotification(): Observable<void> {
    return this.http.post<void>('/api/notification/test', {})
      .pipe(
        catchError(error => {
          console.error('Failed to send test notification:', error);
          throw error;
        })
      );
  }

  /**
   * Get VAPID public key from server
   */
  private async getVapidPublicKey(): Promise<string> {
    try {
      const response = await this.http.get<{publicKey: string}>('/api/notification/vapid-key').toPromise();
      return response!.publicKey;
    } catch (error) {
      console.error('Failed to get VAPID public key:', error);
      throw error;
    }
  }

  /**
   * Send subscription to server
   */
  private async sendSubscriptionToServer(subscription: PushSubscription): Promise<void> {
    const subscriptionData: PushSubscriptionData = {
      endpoint: subscription.endpoint,
      keys: {
        p256dh: this.arrayBufferToBase64(subscription.getKey('p256dh')!),
        auth: this.arrayBufferToBase64(subscription.getKey('auth')!)
      }
    };

    try {
      await this.http.post('/api/notification/subscribe', subscriptionData).toPromise();
    } catch (error) {
      console.error('Failed to send subscription to server:', error);
      throw error;
    }
  }

  /**
   * Remove subscription from server
   */
  private async removeSubscriptionFromServer(subscription: PushSubscription): Promise<void> {
    try {
      await this.http.delete('/api/notification/unsubscribe', {
        body: { endpoint: subscription.endpoint }
      }).toPromise();
    } catch (error) {
      console.error('Failed to remove subscription from server:', error);
      throw error;
    }
  }

  /**
   * Load notification settings from localStorage
   */
  private loadNotificationSettings(): void {
    try {
      const saved = localStorage.getItem('notification-settings');
      if (saved) {
        const settings = JSON.parse(saved);
        this.settingsSubject.next(settings);
      }
    } catch (error) {
      console.error('Failed to load notification settings:', error);
    }
  }

  /**
   * Save notification settings to localStorage
   */
  private saveNotificationSettings(settings: NotificationSettings): void {
    try {
      localStorage.setItem('notification-settings', JSON.stringify(settings));
    } catch (error) {
      console.error('Failed to save notification settings:', error);
    }
  }

  /**
   * Convert URL-safe base64 to Uint8Array
   */
  private urlBase64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding)
      .replace(/-/g, '+')
      .replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
  }

  /**
   * Convert ArrayBuffer to base64
   */
  private arrayBufferToBase64(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return window.btoa(binary);
  }
}
