import { writable } from "svelte/store";

// Backend API base URL - empty for same origin (production), full URL for dev
const isBrowser = typeof window !== 'undefined';
const API_BASE = isBrowser && (window.location.port === '5173' || window.location.port === '7270')
  ? ''  // Dev mode: use proxy to localhost:27272
  : '';                             // Production: same origin, use relative URLs

// Helper to create a persistent store with localStorage (for non-critical settings)
function persistentWritable(key, initialValue) {
  const isBrowser = typeof window !== 'undefined';

  let storedValue = initialValue;
  if (isBrowser) {
    try {
      const stored = localStorage.getItem(key);
      if (stored) {
        storedValue = JSON.parse(stored);
      }
    } catch (e) {
      console.warn(`Failed to load ${key} from localStorage:`, e);
    }
  }

  const store = writable(storedValue);

  if (isBrowser) {
    store.subscribe(value => {
      try {
        localStorage.setItem(key, JSON.stringify(value));
      } catch (e) {
        console.warn(`Failed to save ${key} to localStorage:`, e);
      }
    });
  }

  return store;
}

// Create playerStyles store that syncs with backend API
// This ensures styles work in OBS (which loads from backend port)
function createPlayerStylesStore() {
  const store = writable({});
  const isBrowser = typeof window !== 'undefined';
  let isLoaded = false; // Flag to prevent saving before loading

  if (isBrowser) {
      // Load from API on init
      if (isBrowser) {
        fetch(`${API_BASE}/api/styles`)
          .then(res => res.json())
          .then(data => {
            console.log('[Styles] Loaded from API:', data);
            store.set(data);
            isLoaded = true; // Now we can save
          })
          .catch(err => {
            console.warn('[Styles] Failed to load from API, using localStorage fallback:', err);
            // Fallback to localStorage
            try {
              const stored = localStorage.getItem('unikplayer_styles');
              if (stored) store.set(JSON.parse(stored));
            } catch (e) {}
            isLoaded = true; // Allow saving even if load failed
          });
      }

    // Save to API on change (debounced)
    let saveTimeout = null;
    let isFirstCall = true;
    store.subscribe(value => {
      // Skip the initial subscription call
      if (isFirstCall) {
        isFirstCall = false;
        return;
      }

      // Don't save until we've loaded
      if (!isLoaded) return;

      // Also save to localStorage as backup
      try {
        localStorage.setItem('unikplayer_styles', JSON.stringify(value));
      } catch (e) {}

      // Debounce API save
      if (saveTimeout) clearTimeout(saveTimeout);
      saveTimeout = setTimeout(() => {
        fetch(`${API_BASE}/api/styles`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(value)
        })
          .then(res => res.json())
          .then(data => console.log('[Styles] Saved to API:', data.success))
          .catch(err => console.warn('[Styles] Failed to save to API:', err));
      }, 500);
    });
  }

  return store;
}

export let style = writable("BackPicture")

// Track meta data
export let mediaData = writable(null)
export let title     = writable(null)
export let artist    = writable(null)
export let thumbnail = writable(null)

// Track progress data
export let trackPosition = writable(0)      // current position in seconds
export let trackDuration = writable(0)      // total duration in seconds
export let trackProgress = writable(0)      // 0-100 percentage
export let isPlaying = writable(false)      // playback status

// UI state
export let ShowTrack = writable(false)
export let ShowNotification = writable(false)
export let notificationText = writable("Copied!")

// Editor state
export let editorOpen = writable(false)
export let editingPlayer = writable(null)
export let editingPlayerIsCustom = writable(false)
export let editorCSS = writable("")
export let editorHTML = writable("") // For custom player HTML editing

// Player custom styles (saved per player) - synced with backend API for OBS
export let playerStyles = createPlayerStylesStore()

// Color mode: 'dynamic' (from album art) or 'static' (user picked) - PERSISTENT
export let colorMode = persistentWritable('unikplayer_colorMode', 'dynamic')
export let staticColor = persistentWritable('unikplayer_staticColor', '#B87333')

// Font settings - PERSISTENT
export let selectedFont = persistentWritable('unikplayer_font', 'Rubik')

// Language: 'en' or 'ru'
export let language = writable('ru')

// Potato mode: disable all animations
export let potatoMode = persistentWritable('unikplayer_potato', false)