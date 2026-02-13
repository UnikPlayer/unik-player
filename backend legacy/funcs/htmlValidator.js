/**
 * HTML Validator for Custom Players
 * Validates HTML files for security and required structure
 */

const MAX_FILE_SIZE = 50 * 1024; // 50KB

// Allowed external domains
const ALLOWED_DOMAINS = [
  'fonts.googleapis.com',
  'fonts.gstatic.com'
];

// Forbidden event handlers
const EVENT_HANDLERS = [
  'onabort', 'onafterprint', 'onbeforeprint', 'onbeforeunload', 'onblur',
  'oncanplay', 'oncanplaythrough', 'onchange', 'onclick', 'oncontextmenu',
  'oncopy', 'oncuechange', 'oncut', 'ondblclick', 'ondrag', 'ondragend',
  'ondragenter', 'ondragleave', 'ondragover', 'ondragstart', 'ondrop',
  'ondurationchange', 'onemptied', 'onended', 'onerror', 'onfocus',
  'onhashchange', 'oninput', 'oninvalid', 'onkeydown', 'onkeypress',
  'onkeyup', 'onload', 'onloadeddata', 'onloadedmetadata', 'onloadstart',
  'onmessage', 'onmousedown', 'onmousemove', 'onmouseout', 'onmouseover',
  'onmouseup', 'onmousewheel', 'onoffline', 'ononline', 'onpagehide',
  'onpageshow', 'onpaste', 'onpause', 'onplay', 'onplaying', 'onpopstate',
  'onprogress', 'onratechange', 'onreset', 'onresize', 'onscroll',
  'onsearch', 'onseeked', 'onseeking', 'onselect', 'onstalled', 'onstorage',
  'onsubmit', 'onsuspend', 'ontimeupdate', 'ontoggle', 'onunload',
  'onvolumechange', 'onwaiting', 'onwheel'
];

/**
 * Validate HTML content for custom player
 * @param {string} html - HTML content to validate
 * @returns {{valid: boolean, errors: Array<{line: number, column: number, message: string, severity: string}>}}
 */
function validateHTML(html) {
  const errors = [];

  // Check file size
  if (Buffer.byteLength(html, 'utf8') > MAX_FILE_SIZE) {
    errors.push({
      line: 1,
      column: 1,
      message: `File size exceeds ${MAX_FILE_SIZE / 1024}KB limit`,
      severity: 'error'
    });
    return { valid: false, errors };
  }

  const lines = html.split('\n');

  // Check for script tags
  const scriptRegex = /<script[\s\S]*?<\/script>/gi;
  let match;
  while ((match = scriptRegex.exec(html)) !== null) {
    const pos = getLineAndColumn(html, match.index);
    errors.push({
      line: pos.line,
      column: pos.column,
      message: '<script> tags are not allowed',
      severity: 'error'
    });
  }

  // Check for inline script tags (self-closing or unclosed)
  const inlineScriptRegex = /<script[^>]*\/?\s*>/gi;
  while ((match = inlineScriptRegex.exec(html)) !== null) {
    // Skip if already caught by full script tag check
    if (!html.slice(match.index).match(/^<script[\s\S]*?<\/script>/i)) {
      const pos = getLineAndColumn(html, match.index);
      errors.push({
        line: pos.line,
        column: pos.column,
        message: '<script> tags are not allowed',
        severity: 'error'
      });
    }
  }

  // Check for event handlers
  for (const handler of EVENT_HANDLERS) {
    const handlerRegex = new RegExp(`\\s${handler}\\s*=`, 'gi');
    while ((match = handlerRegex.exec(html)) !== null) {
      const pos = getLineAndColumn(html, match.index);
      errors.push({
        line: pos.line,
        column: pos.column,
        message: `Event handler "${handler}" is not allowed`,
        severity: 'error'
      });
    }
  }

  // Check for javascript: URLs
  const jsUrlRegex = /javascript\s*:/gi;
  while ((match = jsUrlRegex.exec(html)) !== null) {
    const pos = getLineAndColumn(html, match.index);
    errors.push({
      line: pos.line,
      column: pos.column,
      message: '"javascript:" URLs are not allowed',
      severity: 'error'
    });
  }

  // Check for forbidden tags
  const forbiddenTags = ['iframe', 'object', 'embed', 'applet', 'form'];
  for (const tag of forbiddenTags) {
    const tagRegex = new RegExp(`<${tag}[\\s>]`, 'gi');
    while ((match = tagRegex.exec(html)) !== null) {
      const pos = getLineAndColumn(html, match.index);
      errors.push({
        line: pos.line,
        column: pos.column,
        message: `<${tag}> tag is not allowed`,
        severity: 'error'
      });
    }
  }

  // Check external resources (src, href)
  const resourceRegex = /(src|href)\s*=\s*["']([^"']+)["']/gi;
  while ((match = resourceRegex.exec(html)) !== null) {
    const url = match[2];

    // Skip template variables
    if (url.includes('{{') && url.includes('}}')) continue;

    // Skip data URLs and blob URLs
    if (url.startsWith('data:') || url.startsWith('blob:')) continue;

    // Skip relative URLs (no protocol)
    if (!url.includes('://') && !url.startsWith('//')) continue;

    // Check if domain is allowed
    const isAllowed = ALLOWED_DOMAINS.some(domain => {
      const urlLower = url.toLowerCase();
      return urlLower.includes(`//${domain}/`) || urlLower.includes(`//${domain}`);
    });

    if (!isAllowed) {
      const pos = getLineAndColumn(html, match.index);
      errors.push({
        line: pos.line,
        column: pos.column,
        message: `External resource not allowed: ${url.substring(0, 50)}${url.length > 50 ? '...' : ''}`,
        severity: 'error'
      });
    }
  }

  // Check for required template variables (warning, not error)
  const hasTitle = html.includes('{{title}}');
  const hasArtist = html.includes('{{artist}}');
  const hasThumbnail = html.includes('{{thumbnail}}');

  if (!hasTitle) {
    errors.push({
      line: 1,
      column: 1,
      message: 'Missing {{title}} template variable',
      severity: 'warning'
    });
  }
  if (!hasArtist) {
    errors.push({
      line: 1,
      column: 1,
      message: 'Missing {{artist}} template variable',
      severity: 'warning'
    });
  }
  if (!hasThumbnail) {
    errors.push({
      line: 1,
      column: 1,
      message: 'Missing {{thumbnail}} template variable',
      severity: 'warning'
    });
  }

  // Only hard errors make validation fail
  const hasErrors = errors.some(e => e.severity === 'error');

  return {
    valid: !hasErrors,
    errors
  };
}

/**
 * Get line and column number from character index
 */
function getLineAndColumn(text, index) {
  const lines = text.substring(0, index).split('\n');
  return {
    line: lines.length,
    column: lines[lines.length - 1].length + 1
  };
}

/**
 * Sanitize HTML by removing dangerous content
 * @param {string} html - HTML to sanitize
 * @returns {string} - Sanitized HTML
 */
function sanitizeHTML(html) {
  // Remove script tags
  html = html.replace(/<script[\s\S]*?<\/script>/gi, '');
  html = html.replace(/<script[^>]*>/gi, '');

  // Remove event handlers
  for (const handler of EVENT_HANDLERS) {
    const regex = new RegExp(`\\s${handler}\\s*=\\s*["'][^"']*["']`, 'gi');
    html = html.replace(regex, '');
  }

  // Remove javascript: URLs
  html = html.replace(/javascript\s*:[^"']*/gi, '#');

  // Remove forbidden tags
  const forbiddenTags = ['iframe', 'object', 'embed', 'applet', 'form'];
  for (const tag of forbiddenTags) {
    html = html.replace(new RegExp(`<${tag}[\\s\\S]*?<\\/${tag}>`, 'gi'), '');
    html = html.replace(new RegExp(`<${tag}[^>]*>`, 'gi'), '');
  }

  return html;
}

/**
 * Format seconds to MM:SS
 * @param {number} seconds
 * @returns {string}
 */
function formatTime(seconds) {
  if (!seconds || seconds < 0) return '0:00';
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

/**
 * Process template variables in HTML
 * @param {string} html - HTML with template variables
 * @param {object} data - Data to substitute
 * @returns {string} - Processed HTML
 */
function processTemplate(html, data) {
  const escapeHtml = (str) => {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  };

  // Calculate progress percentage
  const duration = data.duration || 0;
  const position = data.position || 0;
  const progress = duration > 0 ? Math.min((position / duration) * 100, 100) : 0;

  return html
    // Basic track info
    .replace(/\{\{title\}\}/g, escapeHtml(data.title || ''))
    .replace(/\{\{artist\}\}/g, escapeHtml(data.artist || ''))
    .replace(/\{\{thumbnail\}\}/g, data.thumbnail || '')
    // Progress/timeline variables
    .replace(/\{\{progress\}\}/g, progress.toFixed(1))
    .replace(/\{\{position\}\}/g, position.toFixed(0))
    .replace(/\{\{duration\}\}/g, duration.toFixed(0))
    .replace(/\{\{currentTime\}\}/g, formatTime(position))
    .replace(/\{\{totalTime\}\}/g, formatTime(duration));
}

/**
 * Inject CSS color variables into HTML
 * @param {string} html - HTML content
 * @param {object} colors - Color values
 * @returns {string} - HTML with injected colors
 */
function injectColors(html, colors) {
  const colorCSS = `
    :root {
      --vibrant: ${colors.vibrant || '#D4944A'};
      --lightVibrant: ${colors.lightVibrant || '#F5DEB3'};
      --darkVibrant: ${colors.darkVibrant || '#5C4033'};
      --muted: ${colors.muted || '#8B6914'};
      --lightMuted: ${colors.lightMuted || '#B87333'};
      --darkMuted: ${colors.darkMuted || 'rgba(20, 15, 10, 0.9)'};
    }
  `;

  // Inject before </head> if exists, otherwise at the start
  if (html.includes('</head>')) {
    return html.replace('</head>', `<style>${colorCSS}</style></head>`);
  } else {
    return `<style>${colorCSS}</style>${html}`;
  }
}

module.exports = {
  validateHTML,
  sanitizeHTML,
  processTemplate,
  injectColors,
  MAX_FILE_SIZE,
  ALLOWED_DOMAINS
};
