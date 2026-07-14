import {Component, computed, Input, OnChanges, OnInit, signal} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SecurityService} from '../../../core/security/security.service';
import {SafeHtml} from '@angular/platform-browser';

/**
 * Secure content display component
 * Handles safe rendering of user-generated or dynamic content
 */
@Component({
  selector: 'app-secure-content',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="secure-content-container" [class.csp-violation]="hasCspViolation()">

      <!-- Security Warning Banner -->
      <div class="security-warning" *ngIf="hasCspViolation()">
        <div class="warning-icon">⚠️</div>
        <div class="warning-text">
          <strong>Security Notice:</strong>
          Some content has been blocked for your protection.
        </div>
      </div>

      <!-- Safe HTML Content -->
      <div
        class="content-display"
        *ngIf="!hasCspViolation() && sanitizedContent()"
        [innerHTML]="sanitizedContent()">
      </div>

      <!-- Fallback for blocked content -->
      <div class="content-blocked" *ngIf="hasCspViolation()">
        <div class="blocked-icon">🛡️</div>
        <h3>Content Blocked</h3>
        <p>This content contains potentially unsafe elements and has been blocked to protect your account.</p>
        <button class="btn btn-secondary" (click)="showRawContent = !showRawContent">
          {{ showRawContent ? 'Hide' : 'Show' }} Raw Content
        </button>

        <div class="raw-content" *ngIf="showRawContent">
          <h4>Raw Content (Safe View):</h4>
          <pre>{{ rawContent() }}</pre>
        </div>
      </div>

      <!-- Content Security Info -->
      <div class="security-info" *ngIf="showSecurityInfo">
        <details>
          <summary>Security Information</summary>
          <div class="security-details">
            <div class="security-item">
              <strong>Content Type:</strong> {{ contentType }}
            </div>
            <div class="security-item">
              <strong>CSP Compliant:</strong>
              <span [class.compliant]="!hasCspViolation()" [class.violation]="hasCspViolation()">
                {{ hasCspViolation() ? 'No' : 'Yes' }}
              </span>
            </div>
            <div class="security-item">
              <strong>Sanitization Applied:</strong> Yes
            </div>
            <div class="security-item" *ngIf="blockedElements().length > 0">
              <strong>Blocked Elements:</strong> {{ blockedElements().join(', ') }}
            </div>
          </div>
        </details>
      </div>
    </div>
  `,
  styles: [`
    .secure-content-container {
      position: relative;
      border-radius: 8px;
      overflow: hidden;
    }

    .secure-content-container.csp-violation {
      border: 2px solid #dc3545;
      background-color: #f8f9fa;
    }

    .security-warning {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
      background: linear-gradient(135deg, #fff3cd, #ffeaa7);
      border-bottom: 1px solid #ffd700;
      color: #856404;
    }

    .warning-icon {
      font-size: 1.25rem;
      flex-shrink: 0;
    }

    .warning-text {
      flex: 1;
    }

    .content-display {
      padding: 16px;
      line-height: 1.6;
    }

    .content-display :global(h1),
    .content-display :global(h2),
    .content-display :global(h3) {
      color: #2c3e50;
      margin-bottom: 12px;
    }

    .content-display :global(p) {
      margin-bottom: 12px;
    }

    .content-display :global(a) {
      color: #007bff;
      text-decoration: none;
    }

    .content-display :global(a:hover) {
      text-decoration: underline;
    }

    .content-display :global(table) {
      width: 100%;
      border-collapse: collapse;
      margin: 16px 0;
    }

    .content-display :global(th),
    .content-display :global(td) {
      padding: 8px 12px;
      border: 1px solid #dee2e6;
      text-align: left;
    }

    .content-display :global(th) {
      background-color: #f8f9fa;
      font-weight: 600;
    }

    .content-blocked {
      text-align: center;
      padding: 40px 20px;
      color: #6c757d;
    }

    .blocked-icon {
      font-size: 3rem;
      margin-bottom: 16px;
    }

    .content-blocked h3 {
      color: #dc3545;
      margin-bottom: 12px;
    }

    .content-blocked p {
      margin-bottom: 20px;
      max-width: 400px;
      margin-left: auto;
      margin-right: auto;
    }

    .btn {
      padding: 8px 16px;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 0.875rem;
      transition: all 0.2s ease;
    }

    .btn-secondary {
      background-color: #6c757d;
      color: white;
    }

    .btn-secondary:hover {
      background-color: #545b62;
    }

    .raw-content {
      margin-top: 20px;
      text-align: left;
      background-color: #f8f9fa;
      padding: 16px;
      border-radius: 4px;
      border: 1px solid #dee2e6;
    }

    .raw-content h4 {
      margin-bottom: 12px;
      color: #495057;
    }

    .raw-content pre {
      background-color: #e9ecef;
      padding: 12px;
      border-radius: 4px;
      overflow-x: auto;
      font-family: 'Courier New', monospace;
      font-size: 0.875rem;
      line-height: 1.4;
      white-space: pre-wrap;
      word-break: break-word;
    }

    .security-info {
      margin-top: 16px;
      border-top: 1px solid #dee2e6;
      padding-top: 16px;
    }

    .security-info details {
      cursor: pointer;
    }

    .security-info summary {
      font-size: 0.875rem;
      color: #6c757d;
      padding: 8px 0;
      user-select: none;
    }

    .security-info summary:hover {
      color: #495057;
    }

    .security-details {
      padding: 12px 0;
      font-size: 0.875rem;
    }

    .security-item {
      display: flex;
      justify-content: space-between;
      padding: 4px 0;
    }

    .security-item strong {
      color: #495057;
    }

    .compliant {
      color: #28a745;
      font-weight: 600;
    }

    .violation {
      color: #dc3545;
      font-weight: 600;
    }

    @media (max-width: 768px) {
      .security-warning {
        flex-direction: column;
        text-align: center;
        gap: 8px;
      }

      .content-blocked {
        padding: 24px 16px;
      }

      .security-item {
        flex-direction: column;
        align-items: flex-start;
        gap: 4px;
      }
    }
  `]
})
export class SecureContentComponent implements OnInit, OnChanges {
  @Input() content: string = '';
  @Input() contentType: 'html' | 'text' | 'markdown' = 'html';
  @Input() showSecurityInfo: boolean = false;
  @Input() maxLength: number = 10000;

  // Component state
  showRawContent = false;

  // Signals for reactive state
  private rawContentSignal = signal<string>('');
  private sanitizedContentSignal = signal<SafeHtml | null>(null);
  private cspViolationSignal = signal<boolean>(false);
  private blockedElementsSignal = signal<string[]>([]);

  // Computed properties
  rawContent = computed(() => this.rawContentSignal());
  sanitizedContent = computed(() => this.sanitizedContentSignal());
  hasCspViolation = computed(() => this.cspViolationSignal());
  blockedElements = computed(() => this.blockedElementsSignal());

  constructor(private securityService: SecurityService) {}

  ngOnInit(): void {
    this.processContent();
  }

  ngOnChanges(): void {
    this.processContent();
  }

  /**
   * Process and sanitize content
   */
  private processContent(): void {
    if (!this.content) {
      this.resetState();
      return;
    }

    // Truncate content if too long
    const truncatedContent = this.content.substring(0, this.maxLength);
    this.rawContentSignal.set(truncatedContent);

    // Check CSP compliance
    const isCompliant = this.securityService.validateCspCompliance(truncatedContent);
    this.cspViolationSignal.set(!isCompliant);

    if (!isCompliant) {
      this.detectBlockedElements(truncatedContent);
      this.sanitizedContentSignal.set(null);
      return;
    }

    // Sanitize content based on type
    try {
      let sanitized: SafeHtml;

      switch (this.contentType) {
        case 'html':
          sanitized = this.securityService.sanitizeHtml(truncatedContent);
          break;
        case 'markdown':
          // Convert basic markdown to HTML then sanitize
          const htmlFromMarkdown = this.markdownToHtml(truncatedContent);
          sanitized = this.securityService.sanitizeHtml(htmlFromMarkdown);
          break;
        case 'text':
        default:
          // Escape HTML entities and wrap in paragraph
          const escapedText = this.escapeHtml(truncatedContent);
          sanitized = this.securityService.sanitizeHtml(`<p>${escapedText}</p>`);
          break;
      }

      this.sanitizedContentSignal.set(sanitized);
      this.blockedElementsSignal.set([]);
    } catch (error) {
      console.warn('Content sanitization failed:', error);
      this.cspViolationSignal.set(true);
      this.sanitizedContentSignal.set(null);
    }
  }

  /**
   * Reset component state
   */
  private resetState(): void {
    this.rawContentSignal.set('');
    this.sanitizedContentSignal.set(null);
    this.cspViolationSignal.set(false);
    this.blockedElementsSignal.set([]);
    this.showRawContent = false;
  }

  /**
   * Detect blocked elements for security reporting
   */
  private detectBlockedElements(content: string): void {
    const blockedElements: string[] = [];

    // Check for inline scripts
    if (content.includes('<script')) {
      blockedElements.push('inline scripts');
    }

    // Check for event handlers
    if (/\son\w+\s*=/gi.test(content)) {
      blockedElements.push('event handlers');
    }

    // Check for javascript URLs
    if (content.includes('javascript:')) {
      blockedElements.push('javascript URLs');
    }

    // Check for dangerous CSS
    if (content.includes('expression(') || content.includes('@import')) {
      blockedElements.push('dangerous CSS');
    }

    // Check for form elements (if not allowed)
    if (content.includes('<form') || content.includes('<input')) {
      blockedElements.push('form elements');
    }

    this.blockedElementsSignal.set(blockedElements);
  }

  /**
   * Basic markdown to HTML conversion
   */
  private markdownToHtml(markdown: string): string {
    return markdown
      // Headers
      .replace(/^### (.*$)/gim, '<h3>$1</h3>')
      .replace(/^## (.*$)/gim, '<h2>$1</h2>')
      .replace(/^# (.*$)/gim, '<h1>$1</h1>')
      // Bold
      .replace(/\*\*(.*)\*\*/gim, '<strong>$1</strong>')
      // Italic
      .replace(/\*(.*)\*/gim, '<em>$1</em>')
      // Links
      .replace(/\[([^\]]+)\]\(([^)]+)\)/gim, '<a href="$2">$1</a>')
      // Line breaks
      .replace(/\n$/gim, '<br>');
  }

  /**
   * Escape HTML entities
   */
  private escapeHtml(text: string): string {
    const map: Record<string, string> = {
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;'
    };

    return text.replace(/[&<>"']/g, (match) => map[match]);
  }
}
