import {Injectable} from '@angular/core';
import {DomSanitizer, SafeHtml, SafeResourceUrl, SafeStyle, SafeUrl} from '@angular/platform-browser';

/**
 * Security service for safe content sanitization
 * Implements strict CSP policies and secure content handling
 */
@Injectable({
  providedIn: 'root'
})
export class SecurityService {

  constructor(private sanitizer: DomSanitizer) {}

  /**
   * Sanitize HTML content with strict CSP policy
   * Only allows specific safe tags and attributes
   */
  sanitizeHtml(html: string): SafeHtml {
    // Define allowed tags and attributes for banking application
    const allowedTags = [
      'p', 'br', 'strong', 'em', 'span', 'div', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
      'ul', 'ol', 'li', 'table', 'thead', 'tbody', 'tr', 'td', 'th',
      'a', 'img'
    ];

    const allowedAttributes = {
      'a': ['href', 'title', 'target'],
      'img': ['src', 'alt', 'title', 'width', 'height'],
      'span': ['class'],
      'div': ['class'],
      'p': ['class'],
      'td': ['colspan', 'rowspan'],
      'th': ['colspan', 'rowspan']
    };

    // First sanitize with Angular's built-in sanitizer
    const sanitized = this.sanitizer.sanitize(1, html) || '';

    // Additional custom sanitization for banking security
    const cleanHtml = this.customSanitize(sanitized, allowedTags, allowedAttributes);

    return this.sanitizer.bypassSecurityTrustHtml(cleanHtml);
  }

  /**
   * Sanitize URL for navigation
   * Blocks javascript:, data:, and other dangerous schemes
   */
  sanitizeUrl(url: string): SafeUrl {
    // Allow only safe URL schemes
    const safeSchemes = ['http:', 'https:', 'mailto:', 'tel:'];

    try {
      const urlObj = new URL(url, window.location.origin);

      if (!safeSchemes.includes(urlObj.protocol)) {
        console.warn('Blocked dangerous URL scheme:', urlObj.protocol);
        return this.sanitizer.bypassSecurityTrustUrl('#');
      }

      // Additional validation for banking domains
      if (this.isExternalBankingUrl(urlObj)) {
        console.warn('Blocked external banking URL:', url);
        return this.sanitizer.bypassSecurityTrustUrl('#');
      }

      return this.sanitizer.bypassSecurityTrustUrl(url);
    } catch (error) {
      console.warn('Invalid URL format:', url);
      return this.sanitizer.bypassSecurityTrustUrl('#');
    }
  }

  /**
   * Sanitize resource URL for iframes and embeds
   * Very restrictive for banking security
   */
  sanitizeResourceUrl(url: string): SafeResourceUrl {
    // Only allow trusted resource domains for banking
    const trustedDomains = [
      window.location.hostname,
      'cdn.ebanking.com',
      'static.ebanking.com'
    ];

    try {
      const urlObj = new URL(url, window.location.origin);

      if (!trustedDomains.some(domain => urlObj.hostname === domain)) {
        console.warn('Blocked untrusted resource URL:', url);
        return this.sanitizer.bypassSecurityTrustResourceUrl('about:blank');
      }

      return this.sanitizer.bypassSecurityTrustResourceUrl(url);
    } catch (error) {
      console.warn('Invalid resource URL format:', url);
      return this.sanitizer.bypassSecurityTrustResourceUrl('about:blank');
    }
  }

  /**
   * Sanitize CSS styles
   * Blocks dangerous CSS properties and values
   */
  sanitizeStyle(style: string): SafeStyle {
    // Dangerous CSS properties to block
    const dangerousProperties = [
      'expression',
      'javascript:',
      'data:',
      'vbscript:',
      '@import',
      'behavior',
      '-moz-binding'
    ];

    const lowerStyle = style.toLowerCase();

    for (const dangerous of dangerousProperties) {
      if (lowerStyle.includes(dangerous)) {
        console.warn('Blocked dangerous CSS:', dangerous);
        return this.sanitizer.bypassSecurityTrustStyle('');
      }
    }

    return this.sanitizer.bypassSecurityTrustStyle(style);
  }

  /**
   * Validate and sanitize user input for financial data
   */
  sanitizeFinancialInput(input: string): string {
    // Remove any script tags or dangerous content
    let sanitized = input.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '');
    sanitized = sanitized.replace(/javascript:/gi, '');
    sanitized = sanitized.replace(/on\w+\s*=/gi, '');

    // Trim and limit length for banking inputs
    sanitized = sanitized.trim().substring(0, 1000);

    return sanitized;
  }

  /**
   * Generate CSP nonce for inline scripts (when absolutely necessary)
   */
  generateNonce(): string {
    const array = new Uint8Array(16);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
  }

  /**
   * Validate CSP compliance for dynamic content
   */
  validateCspCompliance(content: string): boolean {
    // Check for inline script patterns
    const inlineScriptPattern = /<script(?:\s[^>]*)?>[\s\S]*?<\/script>/gi;
    const inlineEventPattern = /\son\w+\s*=/gi;
    const javascriptUrlPattern = /javascript:/gi;

    if (inlineScriptPattern.test(content)) {
      console.warn('CSP violation: Inline script detected');
      return false;
    }

    if (inlineEventPattern.test(content)) {
      console.warn('CSP violation: Inline event handler detected');
      return false;
    }

    if (javascriptUrlPattern.test(content)) {
      console.warn('CSP violation: JavaScript URL detected');
      return false;
    }

    return true;
  }

  /**
   * Custom HTML sanitization for banking security
   */
  private customSanitize(html: string, allowedTags: string[], allowedAttributes: Record<string, string[]>): string {
    // Create a temporary DOM element to parse HTML
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = html;

    // Recursively clean the DOM
    this.cleanNode(tempDiv, allowedTags, allowedAttributes);

    return tempDiv.innerHTML;
  }

  /**
   * Recursively clean DOM nodes
   */
  private cleanNode(node: Element, allowedTags: string[], allowedAttributes: Record<string, string[]>): void {
    const children = Array.from(node.children);

    for (const child of children) {
      const tagName = child.tagName.toLowerCase();

      if (!allowedTags.includes(tagName)) {
        // Remove disallowed tags but keep content
        const textContent = child.textContent || '';
        child.replaceWith(document.createTextNode(textContent));
        continue;
      }

      // Clean attributes
      const allowedAttrs = allowedAttributes[tagName] || [];
      const attributes = Array.from(child.attributes);

      for (const attr of attributes) {
        if (!allowedAttrs.includes(attr.name)) {
          child.removeAttribute(attr.name);
        } else {
          // Additional validation for specific attributes
          if (attr.name === 'href' || attr.name === 'src') {
            if (!this.isValidAttributeValue(attr.value)) {
              child.removeAttribute(attr.name);
            }
          }
        }
      }

      // Recursively clean child nodes
      this.cleanNode(child, allowedTags, allowedAttributes);
    }
  }

  /**
   * Validate attribute values for security
   */
  private isValidAttributeValue(value: string): boolean {
    const lowerValue = value.toLowerCase();

    // Block dangerous protocols
    const dangerousProtocols = ['javascript:', 'data:', 'vbscript:', 'file:'];

    for (const protocol of dangerousProtocols) {
      if (lowerValue.startsWith(protocol)) {
        return false;
      }
    }

    return true;
  }

  /**
   * Check if URL is external banking URL (potentially phishing)
   */
  private isExternalBankingUrl(url: URL): boolean {
    const bankingKeywords = [
      'bank', 'banking', 'finance', 'payment', 'paypal', 'visa', 'mastercard',
      'credit', 'loan', 'mortgage', 'investment'
    ];

    // If it's not our domain and contains banking keywords, it's suspicious
    if (url.hostname !== window.location.hostname) {
      const urlText = url.hostname.toLowerCase();
      return bankingKeywords.some(keyword => urlText.includes(keyword));
    }

    return false;
  }
}

/**
 * CSP Configuration for banking application
 */
export const CSP_CONFIG = {
  'default-src': "'self'",
  'script-src': "'self' 'strict-dynamic'",
  'style-src': "'self' 'unsafe-inline'", // Allow inline styles for Angular
  'img-src': "'self' data: https:",
  'font-src': "'self' https:",
  'connect-src': "'self' https://api.ebanking.com wss://api.ebanking.com",
  'frame-src': "'none'", // Block all frames for security
  'object-src': "'none'", // Block plugins
  'base-uri': "'self'",
  'form-action': "'self'",
  'frame-ancestors': "'none'", // Prevent clickjacking
  'upgrade-insecure-requests': '',
  'block-all-mixed-content': ''
};

/**
 * Generate CSP header string
 */
export function generateCspHeader(): string {
  return Object.entries(CSP_CONFIG)
    .map(([directive, value]) => `${directive} ${value}`)
    .join('; ');
}
