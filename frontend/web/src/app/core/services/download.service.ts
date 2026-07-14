import {inject, Injectable} from '@angular/core';
import {HttpClient, HttpErrorResponse, HttpEvent, HttpEventType, HttpResponse} from '@angular/common/http';
import {Observable, throwError, timer} from 'rxjs';
import {catchError, filter, finalize, map, retry, tap} from 'rxjs/operators';

export interface DownloadRequest {
  year: number;
  month: number;
  format?: 'pdf' | 'csv' | 'xlsx';
}

export interface DownloadProgress {
  isDownloading: boolean;
  progress: number;
  error?: string;
  retryCount: number;
}

@Injectable({
  providedIn: 'root'
})
export class DownloadService {
  private readonly http = inject(HttpClient);
  private downloadState = new Map<string, DownloadProgress>();

  /**
   * Download account statement with progress tracking and retry logic
   */
  downloadStatement(request: DownloadRequest): Observable<Blob> {
    const downloadKey = `statement-${request.year}-${request.month}-${request.format || 'pdf'}`;
    const maxRetries = 3;

    // Initialize download state
    this.updateDownloadState(downloadKey, {
      isDownloading: true,
      progress: 0,
      retryCount: 0
    });

    const yearMonth = `${request.year}-${request.month.toString().padStart(2, '0')}`;
    const format = request.format || 'pdf';
    const url = `/api/account/me/statements/${yearMonth}.${format}`;

    return this.http.get(url, {
      responseType: 'blob',
      reportProgress: true,
      observe: 'events'
    }).pipe(
      // Track download progress
      tap((event: HttpEvent<Blob>) => {
        if (event.type === HttpEventType.DownloadProgress && event.total) {
          const progress = Math.round(100 * event.loaded / event.total);
          this.updateDownloadState(downloadKey, {
            isDownloading: true,
            progress,
            retryCount: this.getDownloadState(downloadKey)?.retryCount || 0
          });
        }
      }),

      // Extract blob from response
      map((event: HttpEvent<Blob>) => {
        if (event.type === HttpEventType.Response) {
          return (event as HttpResponse<Blob>).body;
        }
        return null;
      }),

      // Filter out non-final events
      filter((blob: Blob | null): blob is Blob => blob !== null),

      // Retry on failure with exponential backoff
      retry({
        count: maxRetries,
        delay: (error, retryCount) => {
          this.updateDownloadState(downloadKey, {
            isDownloading: true,
            progress: 0,
            retryCount,
            error: `Retry ${retryCount}/${maxRetries}: ${this.getErrorMessage(error)}`
          });

          // Exponential backoff: 1s, 2s, 4s
          return timer(Math.pow(2, retryCount - 1) * 1000);
        }
      }),

      // Handle final errors
      catchError((error: HttpErrorResponse) => {
        const errorMessage = this.getErrorMessage(error);
        this.updateDownloadState(downloadKey, {
          isDownloading: false,
          progress: 0,
          retryCount: maxRetries,
          error: errorMessage
        });
        return throwError(() => new Error(errorMessage));
      }),

      // Clean up download state on completion
      finalize(() => {
        setTimeout(() => {
          this.updateDownloadState(downloadKey, {
            isDownloading: false,
            progress: 100,
            retryCount: this.getDownloadState(downloadKey)?.retryCount || 0
          });
        }, 500);
      })
    ) as Observable<Blob>;
  }

  /**
   * Download and save statement file
   */
  downloadAndSaveStatement(request: DownloadRequest): Observable<void> {
    return this.downloadStatement(request).pipe(
      tap(blob => {
        const filename = this.generateFilename(request);
        this.saveFile(blob, filename);
      }),
      map(() => void 0)
    );
  }

  /**
   * Get download state for a specific request
   */
  getDownloadState(key: string): DownloadProgress | undefined {
    return this.downloadState.get(key);
  }

  /**
   * Get download state as observable
   */
  getDownloadState$(request: DownloadRequest): Observable<DownloadProgress> {
    const downloadKey = `statement-${request.year}-${request.month}-${request.format || 'pdf'}`;

    return new Observable(subscriber => {
      const interval = setInterval(() => {
        const state = this.getDownloadState(downloadKey);
        if (state) {
          subscriber.next(state);
          if (!state.isDownloading) {
            clearInterval(interval);
            if (!state.error) {
              subscriber.complete();
            }
          }
        }
      }, 100);

      return () => clearInterval(interval);
    });
  }

  /**
   * Clear download state
   */
  clearDownloadState(request: DownloadRequest): void {
    const downloadKey = `statement-${request.year}-${request.month}-${request.format || 'pdf'}`;
    this.downloadState.delete(downloadKey);
  }

  /**
   * Save blob as file
   */
  private saveFile(blob: Blob, filename: string): void {
    try {
      // Use direct blob download without file-saver dependency
      this.saveFileDirectly(blob, filename);
    } catch (error) {
      console.error('Failed to save file:', error);
      // Fallback for browsers that don't support blob URLs
      this.saveFileFallback(blob, filename);
    }
  }

  /**
   * Direct file save method using blob URLs
   */
  private saveFileDirectly(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.style.display = 'none';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Clean up
    setTimeout(() => window.URL.revokeObjectURL(url), 100);
  }

  /**
   * Fallback file save method
   */
  private saveFileFallback(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.style.display = 'none';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Clean up
    setTimeout(() => window.URL.revokeObjectURL(url), 100);
  }

  /**
   * Generate appropriate filename
   */
  private generateFilename(request: DownloadRequest): string {
    const year = request.year;
    const month = request.month.toString().padStart(2, '0');
    const format = request.format || 'pdf';

    const monthNames = [
      'January', 'February', 'March', 'April', 'May', 'June',
      'July', 'August', 'September', 'October', 'November', 'December'
    ];

    const monthName = monthNames[request.month - 1];

    return `account-statement-${monthName}-${year}.${format}`;
  }

  /**
   * Extract user-friendly error message
   */
  private getErrorMessage(error: any): string {
    if (error.status === 0) {
      return 'Network error. Please check your connection.';
    } else if (error.status === 404) {
      return 'Statement not found for the requested period.';
    } else if (error.status === 403) {
      return 'You do not have permission to download this statement.';
    } else if (error.status === 500) {
      return 'Server error. Please try again later.';
    } else if (error.status >= 400 && error.status < 500) {
      return error.error?.message || 'Client error occurred.';
    } else if (error.status >= 500) {
      return 'Server error occurred. Please try again.';
    } else {
      return error.message || 'An unexpected error occurred.';
    }
  }

  /**
   * Update download state
   */
  private updateDownloadState(key: string, state: DownloadProgress): void {
    this.downloadState.set(key, state);
  }
}
