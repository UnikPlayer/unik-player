import { rgbToHex } from "./convertToHex.js";

import {
  title,
  artist,
  thumbnail,
  ShowTrack,
  trackPosition,
  trackDuration,
  trackProgress,
  isPlaying,
} from "./stores/stores.js";
import { get } from "svelte/store";

// Dynamic import for Vibrant (browser-only)
let Vibrant = null;

let mediaData = null;
let ws;
let reconnectTimeout = 1000;
let currentBlobUrl = null;
let isConnected = false;
let healthCheckInterval = null;
let lastMessageTime = Date.now();

const HEALTH_CHECK_MS = 30000; // Check every 30s
const MAX_SILENCE_MS = 120000; // Force reconnect if no data for 2 mins

// Track last values to prevent duplicate updates
let lastTitle = null;
let lastArtist = null;
let lastThumbnailHash = null; // hash instead of reference comparison

// Delay before hiding player (prevents flash on track switch)
let hideTimeout = null;

// Simple hash for thumbnail byte arrays (avoids reference comparison on arrays)
function thumbnailHash(data) {
  if (data === null || data === undefined) return null;
  if (typeof data === "string") return data;
  // For arrays: use length + first/middle/last bytes as fingerprint
  const arr = Array.isArray(data) ? data : data?.data || [];
  if (arr.length === 0) return "empty";
  const mid = Math.floor(arr.length / 2);
  return `${arr.length}:${arr[0]}:${arr[mid]}:${arr[arr.length - 1]}`;
}

// Update timeline data from backend
// Р’СЃСЏ Р»РѕРіРёРєР° РїРѕР·РёС†РёРё РІ Р±СЌРєРµРЅРґРµ вЂ” С„СЂРѕРЅС‚РµРЅРґ РїСЂРѕСЃС‚Рѕ РѕС‚РѕР±СЂР°Р¶Р°РµС‚
function updateTimeline(timeline, playback) {
  if (!timeline) {
    trackDuration.set(0);
    trackPosition.set(0);
    trackProgress.set(0);
    isPlaying.set(false);
    return;
  }

  const duration = timeline.duration || 0;
  const position = timeline.position || 0;
  const playing = playback && playback.playbackStatus === 4;

  trackDuration.set(duration);
  trackPosition.set(position);
  isPlaying.set(playing);

  if (duration > 0) {
    trackProgress.set((position / duration) * 100);
  } else {
    trackProgress.set(0);
  }
}

export async function connect() {
  // SSR guard - only run in browser
  if (typeof window === "undefined") {
    console.log("[WS] Skipping - not in browser");
    return;
  }

  // Prevent multiple connections
  if (isConnected || (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING))) {
    console.log("[WS] Already connected or connecting");
    return;
  }

  // Dynamically import Vibrant (browser-only)
  if (!Vibrant) {
    try {
      const module = await import("node-vibrant/browser");
      Vibrant = module.Vibrant;
      console.log("[WS] Vibrant loaded");
    } catch (e) {
      console.error("[WS] Failed to load Vibrant:", e);
    }
  }

  // Use relative path - Vite proxies /ws in dev, backend handles in prod
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  const url = `${protocol}//${window.location.host}/ws`;
  console.log("[WS] Connecting to", url);

  try {
    ws = new WebSocket(url);
  } catch (e) {
    console.error("[WS] Failed to create WebSocket:", e);
    setTimeout(connect, reconnectTimeout);
    return;
  }

  ws.onopen = () => {
    console.log("[WS] Connected successfully!");
    isConnected = true;
    reconnectTimeout = 1000;
    lastMessageTime = Date.now();

    // Start health check
    if (healthCheckInterval) clearInterval(healthCheckInterval);
    healthCheckInterval = setInterval(() => {
      if (Date.now() - lastMessageTime > MAX_SILENCE_MS) {
        console.warn("[WS] No data for 2 minutes, forcing reconnect...");
        if (ws && ws.readyState === WebSocket.OPEN) {
          ws.close();
        } else {
          isConnected = false;
          connect();
        }
      }
    }, HEALTH_CHECK_MS);
  };

  ws.onmessage = (e) => {
    lastMessageTime = Date.now();
    
    // Handle ping/pong keep-alive
    if (typeof e.data === "string" && e.data.trim().toLowerCase() === "ping") {
      ws.send("pong");
      return;
    }
    
    try {
      mediaData = JSON.parse(e.data);
      // Log timeline data (skip full media/thumbnail to reduce noise)
      if (mediaData.timeline) {
        console.log(
          "[WS] timeline:",
          mediaData.timeline,
          "playback:",
          mediaData.playback,
        );
      }

      // РџСЂРѕРІРµСЂСЏРµРј С‡С‚Рѕ media СЃСѓС‰РµСЃС‚РІСѓРµС‚ (title/artist РјРѕРіСѓС‚ Р±С‹С‚СЊ РїСѓСЃС‚С‹РјРё СЃС‚СЂРѕРєР°РјРё)
      if (
        mediaData &&
        mediaData.media &&
        (mediaData.media.title !== undefined ||
          mediaData.media.artist !== undefined)
      ) {
        // New media arrived вЂ” cancel any pending hide
        if (hideTimeout) {
          clearTimeout(hideTimeout);
          hideTimeout = null;
        }

        const newTitle = mediaData.media.title || "Unknown";
        const newArtist = mediaData.media.artist || "Unknown";
        const thumbnailObj = mediaData.media.thumbnail;

        // base64 РјРѕР¶РµС‚ Р±С‹С‚СЊ СЃС‚СЂРѕРєРѕР№, РјР°СЃСЃРёРІРѕРј Р±Р°Р№С‚РѕРІ, РёР»Рё null
        const base64 = thumbnailObj?.data || null;

        // Check if ANYTHING changed - compare with cached values
        const titleChanged = lastTitle !== newTitle;
        const artistChanged = lastArtist !== newArtist;
        const currentThumbHash = thumbnailHash(base64);
        const thumbChanged = lastThumbnailHash !== currentThumbHash;

        // If media didn't change but we have timeline data, just update timeline
        if (!titleChanged && !artistChanged && !thumbChanged) {
          console.log("[WS] Media same, updating timeline only");
          if (mediaData.timeline) {
            updateTimeline(mediaData.timeline, mediaData.playback);
          }
          return;
        }

        console.log("[WS] Track changed:", {
          titleChanged,
          artistChanged,
          thumbChanged,
        });

        // If track (title/artist) changed, reset position first
        const isNewTrack = titleChanged || artistChanged;
        if (isNewTrack) {
          console.log("[WS] New track detected, resetting position to 0");
          trackPosition.set(0);
          trackProgress.set(0);
        }

        // Update cached values FIRST
        lastTitle = newTitle;
        lastArtist = newArtist;
        lastThumbnailHash = currentThumbHash;

        // Create new blob URL if thumbnail changed
        let newThumbnailUrl = get(thumbnail);
        if (thumbChanged) {
          // РћСЃРІРѕР±РѕР¶РґР°РµРј СЃС‚Р°СЂС‹Р№ blob URL
          if (currentBlobUrl) {
            URL.revokeObjectURL(currentBlobUrl);
            currentBlobUrl = null;
          }

          // Р•СЃР»Рё РЅРµС‚ thumbnail - РѕСЃС‚Р°РІР»СЏРµРј null
          if (base64 === null) {
            console.log("[WS] No thumbnail data, showing without image");
            newThumbnailUrl = null;
          }
          // Р”РµРєРѕРґРёСЂСѓРµРј base64 РІ blob
          // base64 РјРѕР¶РµС‚ Р±С‹С‚СЊ: СЃС‚СЂРѕРєРѕР№, РјР°СЃСЃРёРІРѕРј Р±Р°Р№С‚РѕРІ, РёР»Рё РѕР±СЉРµРєС‚РѕРј Buffer
          else if (Array.isArray(base64)) {
            console.log("[WS] base64 type: array");
            // РњР°СЃСЃРёРІ Р±Р°Р№С‚РѕРІ РЅР°РїСЂСЏРјСѓСЋ
            const bytes = new Uint8Array(base64);
            const blob = new Blob([bytes], { type: "image/png" });
            currentBlobUrl = URL.createObjectURL(blob);
            newThumbnailUrl = currentBlobUrl;
          } else if (
            typeof base64 === "object" &&
            base64 !== null &&
            base64.data &&
            Array.isArray(base64.data)
          ) {
            console.log("[WS] base64 type: Buffer object");
            // Node Buffer serialized as {type: 'Buffer', data: [...]}
            const bytes = new Uint8Array(base64.data);
            const blob = new Blob([bytes], { type: "image/png" });
            currentBlobUrl = URL.createObjectURL(blob);
            newThumbnailUrl = currentBlobUrl;
          } else if (typeof base64 === "string") {
            console.log("[WS] base64 type: string");
            // РЈРґР°Р»СЏРµРј data URL prefix РµСЃР»Рё РµСЃС‚СЊ
            let cleanBase64 = base64;
            if (cleanBase64.includes(",")) {
              cleanBase64 = cleanBase64.split(",")[1];
            }
            // РЈРґР°Р»СЏРµРј РїСЂРѕР±РµР»С‹ Рё РїРµСЂРµРЅРѕСЃС‹ СЃС‚СЂРѕРє
            cleanBase64 = cleanBase64.replace(/[\s\r\n]/g, "");

            try {
              const binaryString = atob(cleanBase64);
              const bytes = new Uint8Array(binaryString.length);
              for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
              }
              const blob = new Blob([bytes], { type: "image/png" });
              currentBlobUrl = URL.createObjectURL(blob);
              newThumbnailUrl = currentBlobUrl;
            } catch (decodeErr) {
              console.error("[WS] Failed to decode base64:", decodeErr);
              // Fallback - try to use as-is if it's a data URL
              if (base64.startsWith("data:")) {
                newThumbnailUrl = base64;
              }
            }
          }

          // Extract colors from new thumbnail (only if we have one and Vibrant is loaded)
          if (newThumbnailUrl && Vibrant) {
            Vibrant.from(newThumbnailUrl)
              .getPalette()
              .then((palette) => {
                console.log("[Vibrant] Palette extracted:", palette);
                rgbToHex(palette);
              })
              .catch((err) => {
                console.error("[Vibrant] Failed to extract palette:", err);
              });
          }
        }

        // ATOMIC UPDATE: Set all three stores at once to trigger single {#key} change
        title.set(newTitle);
        artist.set(newArtist);
        thumbnail.set(newThumbnailUrl);
        ShowTrack.set(true);

        // Update timeline/progress data
        updateTimeline(mediaData.timeline, mediaData.playback);
      } else if (mediaData && mediaData.media === null) {
        // РњРµРґРёР° РѕСЃС‚Р°РЅРѕРІР»РµРЅРѕ вЂ” СЃРєСЂС‹РІР°РµРј СЃ Р·Р°РґРµСЂР¶РєРѕР№ (РїСЂРё СЃРјРµРЅРµ С‚СЂРµРєР°
        // Р±СЌРєРµРЅРґ РєСЂР°С‚РєРѕРІСЂРµРјРµРЅРЅРѕ С€Р»С‘С‚ null, РїРѕС‚РѕРј РЅРѕРІС‹Р№ С‚СЂРµРє)
        console.log("[WS] Media null вЂ” scheduling hide (1500ms)");
        if (hideTimeout) clearTimeout(hideTimeout);
        hideTimeout = setTimeout(() => {
          hideTimeout = null;
          console.log("[WS] Media stopped/filtered - hiding player");
          ShowTrack.set(false);
          // Keep last title/artist/thumbnail so {#key} doesn't flash on next track
          // Only reset cache so the next track is treated as new
          lastTitle = null;
          lastArtist = null;
          lastThumbnailHash = null;
          updateTimeline(null, null);
        }, 600);
      } else if (mediaData && mediaData.timeline) {
        // Timeline update without full media change (e.g., position update)
        updateTimeline(mediaData.timeline, mediaData.playback);

        // Check if playback stopped (pause/stop) вЂ” schedule hide
        const playing =
          mediaData.playback && mediaData.playback.playbackStatus === 4;
        if (!playing && get(ShowTrack)) {
          console.log("[WS] Playback paused/stopped вЂ” scheduling hide");
          if (hideTimeout) clearTimeout(hideTimeout);
          hideTimeout = setTimeout(() => {
            hideTimeout = null;
            // Check again вЂ” maybe resumed during the delay
            if (!get(isPlaying)) {
              console.log("[WS] Still paused вЂ” hiding player");
              ShowTrack.set(false);
              lastTitle = null;
              lastArtist = null;
              lastThumbnailHash = null;
              updateTimeline(null, null);
            }
          }, 600);
        } else if (playing && hideTimeout) {
          // Resumed вЂ” cancel pending hide
          clearTimeout(hideTimeout);
          hideTimeout = null;
          if (!get(ShowTrack)) {
            ShowTrack.set(true);
          }
        }
      }
      // Р•СЃР»Рё РґР°РЅРЅС‹Рµ РЅРµРїРѕР»РЅС‹Рµ - РїСЂРѕСЃС‚Рѕ РёРіРЅРѕСЂРёСЂСѓРµРј, РѕСЃС‚Р°РІР»СЏРµРј РїСЂРµРґС‹РґСѓС‰РµРµ СЃРѕСЃС‚РѕСЏРЅРёРµ
    } catch (err) {
      console.error("[WS] Parsing error", err);
    }
  };

  ws.onerror = (e) => {
    console.error("[WS] Error:", e);
    isConnected = false;
  };

  ws.onclose = () => {
    console.log("[WS] Closed, reconnect in", reconnectTimeout, "ms");
    isConnected = false;
    if (healthCheckInterval) clearInterval(healthCheckInterval);
    healthCheckInterval = null;
    setTimeout(connect, reconnectTimeout);
    reconnectTimeout = Math.min(reconnectTimeout * 2, 30000);
  };
}
