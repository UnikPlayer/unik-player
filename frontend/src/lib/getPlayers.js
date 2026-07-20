// Auto-discovery: Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РЅР°С…РѕРґРёРј РІСЃРµ РїР»РµРµСЂС‹ РІ РїР°РїРєРµ /players
const playerModules = import.meta.glob('./players/*.svelte', { eager: true });

// РљРѕРјРїРѕРЅРµРЅС‚ РґР»СЏ РєР°СЃС‚РѕРјРЅС‹С… РїР»РµРµСЂРѕРІ
import CustomPlayerRenderer from '$lib/components/CustomPlayerRenderer.svelte';

// РџСЂРµРѕР±СЂР°Р·СѓРµРј РјРѕРґСѓР»Рё РІ РѕР±СЉРµРєС‚ { name: { component, meta } }
const builtInPlayers = {};
for (const [path, module] of Object.entries(playerModules)) {
  const name = path.match(/\/([^/]+)\.svelte$/)?.[1];
  if (name) {
    builtInPlayers[name] = {
      component: module.default,
      meta: module.meta || { name, defaultCSS: '' }
    };
  }
}

// Backend API base URL
function getApiBase() {
  if (typeof window === 'undefined') return 'http://127.0.0.1:27272';
  const port = window.location.port;
  // Dev mode - proxy С‡РµСЂРµР· Vite РёР»Рё РЅР°РїСЂСЏРјСѓСЋ РЅР° backend
  if (port === '7270' || port === '5173') return '';
  // Production - С‚РѕС‚ Р¶Рµ С…РѕСЃС‚
  return '';
}

// Cache for custom players list
let customPlayersCache = [];
let customPlayersCacheTime = 0;
const CACHE_TTL = 5000; // 5 seconds

/**
 * Fetch custom players from API
 * @returns {Promise<Array<{name: string, hasBackup: boolean, isCustom: boolean}>>}
 */
export async function fetchCustomPlayers() {
  const now = Date.now();

  // Return cache if valid
  if (customPlayersCache.length > 0 && now - customPlayersCacheTime < CACHE_TTL) {
    return customPlayersCache;
  }

  try {
    const res = await fetch(`${getApiBase()}/api/custom-players`);
    if (res.ok) {
      const text = await res.text();
      try {
        const data = JSON.parse(text);
        customPlayersCache = data.players || [];
        customPlayersCacheTime = now;
        return customPlayersCache;
      } catch (parseErr) {
        console.error('Failed to parse custom players response:', text.substring(0, 200));
      }
    } else {
      console.error('Custom players API returned:', res.status);
    }
  } catch (e) {
    console.error('Failed to fetch custom players:', e);
  }

  return customPlayersCache;
}

/**
 * Invalidate custom players cache
 */
export function invalidateCustomPlayersCache() {
  customPlayersCache = [];
  customPlayersCacheTime = 0;
}

/**
 * Fetch built-in players from API (players/ directory, served as static)
 * @returns {Promise<Array<{name: string}>>}
 */
async function fetchBuiltInPlayers() {
  try {
    const res = await fetch(`${getApiBase()}/api/players`);
    if (res.ok) {
      const data = await res.json();
      const players = data.players || [];
      // Update cache for sync getPickedPlayer
      knownExamplePlayers = players.map(p => p.name);
      return players;
    }
  } catch (e) {
    console.error('Failed to fetch built-in players:', e);
  }
  return [];
}

/**
 * Get metadata for a player (built-in only)
 * @param {string} name - Player name
 * @returns {{ name: string, defaultCSS: string } | null}
 */
export function getPlayerMeta(name) {
  if (builtInPlayers[name]) {
    return builtInPlayers[name].meta;
  }
  return null;
}

/**
 * Get all built-in player names
 * @returns {string[]}
 */
export function getBuiltInPlayerNames() {
  return Object.keys(builtInPlayers);
}

/**
 * Get all players (built-in + custom)
 * @returns {Array<{component: any, name: string, isCustom?: boolean}>}
 */
export function getAllPlayers() {
  return Object.entries(builtInPlayers).map(([name, { component }]) => ({
    component,
    name,
    isCustom: false
  }));
}

/**
 * Get all players including custom and example (async version)
 * @returns {Promise<Array<{component: any, name: string, isCustom: boolean, isExample?: boolean, hasBackup?: boolean, error?: string}>>}
 */
export async function getAllPlayersAsync() {
  const builtIn = Object.entries(builtInPlayers).map(([name, { component }]) => {
    try {
      if (!component) {
        return {
          component: null,
          name,
          isCustom: false,
          error: `Component ${name} is null`
        };
      }
      return {
        component,
        name,
        isCustom: false
      };
    } catch (err) {
      return {
        component: null,
        name,
        isCustom: false,
        error: err.message || 'Unknown error'
      };
    }
  });

  const custom = await fetchCustomPlayers();
  const customMapped = custom.map(p => ({
    component: CustomPlayerRenderer,
    name: p.name,
    isCustom: true,
    hasBackup: p.hasBackup
  }));

  const players = await fetchBuiltInPlayers();
  const playersMapped = players.map(p => ({
    component: CustomPlayerRenderer,
    name: p.name,
    isCustom: false,
    isExample: true
  }));

  return [...builtIn, ...playersMapped, ...customMapped];
}

/**
 * Get picked player by name
 * @param {string} styleName
 * @returns {Array<{component: any, name: string, isCustom: boolean}>}
 */
// Cache of known example player names (populated by getAllPlayersAsync / fetchBuiltInPlayers)
let knownExamplePlayers = [];

/**
 * Get picked player by name (sync — relies on cached names for example players)
 * @param {string} styleName
 * @returns {Array<{component: any, name: string, isCustom: boolean, isExample?: boolean}>}
 */
export function getPickedPlayer(styleName) {
  if (!styleName) return [];

  // Check built-in players first
  if (builtInPlayers[styleName]) {
    return [{
      component: builtInPlayers[styleName].component,
      name: styleName,
      isCustom: false
    }];
  }

  // Check if it's a known example player (from players/ directory)
  if (knownExamplePlayers.includes(styleName)) {
    return [{
      component: CustomPlayerRenderer,
      name: styleName,
      isCustom: false,
      isExample: true
    }];
  }

  // Fallback: assume it's a custom player
  return [{
    component: CustomPlayerRenderer,
    name: styleName,
    isCustom: true,
    hasBackup: false
  }];
}

/**
 * Check if player name is a custom player
 * @param {string} name
 * @returns {boolean}
 */
export function isCustomPlayer(name) {
  return !builtInPlayers[name];
}

/**
 * Delete a custom player
 * @param {string} name
 * @returns {Promise<boolean>}
 */
export async function deleteCustomPlayer(name) {
  try {
    const res = await fetch(`${getApiBase()}/api/custom-players/${name}`, { method: 'DELETE' });
    if (res.ok) {
      invalidateCustomPlayersCache();
      return true;
    }
  } catch (e) {
    console.error('Failed to delete custom player:', e);
  }
  return false;
}
