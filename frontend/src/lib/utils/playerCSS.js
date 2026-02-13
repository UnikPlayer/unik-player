// Shared CSS utilities for player styling:
// scoping, loading from backend, and DOM injection

function getApiBase() {
  if (typeof window === 'undefined') return 'http://localhost:27272';
  const port = window.location.port;
  if (port === '7270' || port === '5173') return '';
  return '';
}

/**
 * Transform raw user CSS to be scoped to a specific player.
 *
 * Input:   .mainDiv { background: red; }
 * Output:  .player-Generic .mainDiv { background: red !important; }
 *
 * @param {string} rawCSS
 * @param {string} playerName
 * @param {string} [containerScope] - optional prefix (e.g. '.preview-container')
 */
export function transformCSS(rawCSS, playerName, containerScope = '') {
  if (!rawCSS || !rawCSS.trim()) return '';

  const playerScope = `.player-${playerName}`;
  const fullScope = containerScope
    ? `${containerScope} ${playerScope}`
    : playerScope;

  let css = rawCSS.replace(/\/\*[\s\S]*?\*\//g, '');
  const parts = css.split('}');
  const transformed = [];

  for (let part of parts) {
    part = part.trim();
    if (!part) continue;

    const braceIdx = part.indexOf('{');
    if (braceIdx === -1) continue;

    let selector = part.substring(0, braceIdx).trim();
    let body = part.substring(braceIdx + 1).trim();

    if (!selector || !body) continue;

    if (selector.startsWith('@')) {
      transformed.push(part + '}');
      continue;
    }

    const selectors = selector.split(',').map(s => {
      s = s.trim();
      if (!s) return '';
      // Already contains our player scope — don't double-nest
      if (s.includes(playerScope)) return s;
      if (s === '*') return `${fullScope} *`;
      return `${fullScope} ${s}`;
    }).filter(s => s);

    if (selectors.length === 0) continue;

    body = body.replace(/:\s*([^;!]+);/g, ': $1 !important;');
    if (body && !body.endsWith(';') && !body.endsWith('!important')) {
      body = body.replace(/:\s*([^;!{}]+)$/, ': $1 !important');
    }

    transformed.push(`${selectors.join(', ')} { ${body} }`);
  }

  return transformed.join('\n');
}

/**
 * Inject CSS into <head> as a <style> element. Replaces existing with same id.
 */
export function injectCSS(css, id = 'unik-player-custom-css') {
  const existing = document.getElementById(id);
  if (existing) existing.remove();

  if (!css || !css.trim()) return;

  const style = document.createElement('style');
  style.id = id;
  style.textContent = css;
  document.head.appendChild(style);
}

/**
 * Remove a previously injected <style> element by id.
 */
export function removeCSS(id) {
  const el = document.getElementById(id);
  if (el) el.remove();
}

/**
 * Load CSS from backend. Returns raw CSS string or null.
 */
export async function loadCSSFromBackend(playerName) {
  try {
    const res = await fetch(`${getApiBase()}/api/css/${playerName}`);
    if (res.ok) {
      const css = await res.text();
      if (css && css.trim()) return css;
    }
  } catch (err) {
    console.warn(`[CSS] Failed to load CSS for ${playerName}:`, err);
  }
  return null;
}

/**
 * Save CSS to backend.
 */
export async function saveCSSToBackend(playerName, css) {
  try {
    const res = await fetch(`${getApiBase()}/api/css/${playerName}`, {
      method: 'POST',
      headers: { 'Content-Type': 'text/css' },
      body: css
    });
    return res.ok;
  } catch (err) {
    console.warn(`[CSS] Failed to save CSS for ${playerName}:`, err);
    return false;
  }
}

/**
 * Delete user CSS from backend (reset to factory defaults).
 */
export async function deleteCSSFromBackend(playerName) {
  try {
    const res = await fetch(`${getApiBase()}/api/css/${playerName}`, {
      method: 'DELETE'
    });
    return res.ok;
  } catch (err) {
    console.warn(`[CSS] Failed to delete CSS for ${playerName}:`, err);
    return false;
  }
}
