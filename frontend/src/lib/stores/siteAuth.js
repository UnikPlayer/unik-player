import { writable } from 'svelte/store';

const SITE_URL = 'https://player.uniknow.ru';
const CALLBACK_URL = 'http://127.0.0.1:27272/auth-callback';

function createSiteAuthStore() {
  const { subscribe, set, update } = writable(null);

  return {
    subscribe,
    set,

    async loadFromBackend() {
      try {
        const res = await fetch('/api/site-auth');
        if (res.ok) {
          const data = await res.json();
          if (data.token) {
            set({ token: data.token, nickname: data.nickname || 'User' });
          }
        }
      } catch (e) {
        console.warn('[SiteAuth] Failed to load auth state:', e);
      }
    },

    async logout() {
      try {
        await fetch('/api/site-auth', { method: 'DELETE' });
      } catch (e) {
        console.warn('[SiteAuth] Failed to logout:', e);
      }
      set(null);
    },

    getLoginURL() {
      return `${SITE_URL}/unikplayer-auth?redirect=${encodeURIComponent(CALLBACK_URL)}`;
    },

    async syncLikedPlayers() {
      const state = getSiteAuthState();
      if (!state?.token) return [];

      try {
        // Get liked players list
        const res = await fetch(`${SITE_URL}/api/me/liked-players`, {
          headers: { 'Authorization': `Bearer ${state.token}` }
        });
        if (!res.ok) return [];

        const data = await res.json();
        const players = data.players || [];

        // Download HTML for each player
        const result = [];
        for (const p of players) {
          try {
            const dlRes = await fetch(`${SITE_URL}/api/players/${p.id}/download`, {
              headers: { 'Authorization': `Bearer ${state.token}` }
            });
            if (dlRes.ok) {
              const html = await dlRes.text();
              result.push({ ...p, html_content: html });
            }
          } catch (e) {
            console.warn(`[SiteAuth] Failed to download player ${p.name}:`, e);
          }
        }
        return result;
      } catch (e) {
        console.warn('[SiteAuth] Failed to sync liked players:', e);
      }
      return [];
    }
  };
}

let currentState = null;
function getSiteAuthState() { return currentState; }

export const siteAuth = createSiteAuthStore();
siteAuth.subscribe(v => { currentState = v; });
