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
  trackBpm,
} from "./stores/stores.js";
import { get } from "svelte/store";

// Dynamic import for Vibrant (browser-only)
let Vibrant = null;

let mediaData = null;
let ws;
let reconnectTimeout = 500;
let currentBlobUrl = null;
let currentBlob = null;
let isConnected = false;
let healthCheckInterval = null;
let lastMessageTime = Date.now();

const HEALTH_CHECK_MS = 30000; // Check every 30s
const MAX_SILENCE_MS = 120000; // Force reconnect if no data for 2 mins

// Track last values to prevent duplicate updates
let lastTitle = null;
let lastArtist = null;
let lastThumbnailHash = null; // hash instead of reference comparison
let nullThumbnailCount = 0;

// Delay before hiding player (prevents flash on track switch)
let hideTimeout = null;

// Detect Windows 10 at startup (once)
let isWindows10 = false;
if (typeof window !== 'undefined') {
  const ua = navigator.userAgent;
  if (ua.includes('Windows NT 10.0') || ua.includes('Windows NT 6.3') || ua.includes('Windows NT 6.2') || ua.includes('Windows NT 6.1')) {
    isWindows10 = true;
  }
  console.log("[WS] UserAgent:", ua);
  console.log("[WS] isWindows10:", isWindows10);
}

// Crop Spotify branding bar from thumbnails (Win 10)
// Removes 35px left/right, 70px bottom
async function cropSpotifyThumbnail(blob) {
  const img = await new Promise((resolve, reject) => {
    const i = new Image();
    i.onload = () => resolve(i);
    i.onerror = reject;
    i.src = URL.createObjectURL(blob);
  });

  const w = img.width;
  const h = img.height;
  console.log("[CROP] Original size:", w, "×", h);

  // If image is too small, skip
  if (w <= 70 || h <= 70) {
    console.log("[CROP] Image too small, skipping");
    URL.revokeObjectURL(img.src);
    return blob;
  }

  const cropX = 35;
  const cropRight = 35;
  const cropBottom = 70;

  const outW = w - cropX - cropRight;
  const outH = h - cropBottom;
  console.log("[CROP] Output size:", outW, "×", outH);

  const canvas = document.createElement('canvas');
  canvas.width = outW;
  canvas.height = outH;
  const ctx = canvas.getContext('2d');
  ctx.drawImage(img, cropX, 0, outW, outH, 0, 0, outW, outH);

  const croppedBlob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
  URL.revokeObjectURL(img.src);
  console.log("[CROP] Done");
  return croppedBlob;
}

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
// Вся логика позиции в бэкенде — фронтенд просто отображает
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

  // Connect to backend WebSocket server FIRST, then load Vibrant in parallel
  const url = `ws://127.0.0.1:62727/ws`;
  console.log("[WS] Connecting to", url);

  try {
    ws = new WebSocket(url);
  } catch (e) {
    console.error("[WS] Failed to create WebSocket:", e);
    setTimeout(connect, reconnectTimeout);
    return;
  }

  // Load Vibrant in background (don't block connection)
  if (!Vibrant) {
    import("node-vibrant/browser").then(module => {
      Vibrant = module.Vibrant;
      console.log("[WS] Vibrant loaded");
    }).catch(e => {
      console.error("[WS] Failed to load Vibrant:", e);
    });
  }

  ws.onopen = () => {
    console.log("[WS] Connected successfully!");
    isConnected = true;
    reconnectTimeout = 500;
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

  ws.onmessage = async (e) => {
    lastMessageTime = Date.now();
    
    // Handle ping/pong keep-alive
    if (typeof e.data === "string" && e.data.trim().toLowerCase() === "ping") {
      ws.send("pong");
      return;
    }
    
    try {
      mediaData = JSON.parse(e.data);

      // BPM of the currently playing track (from backend loopback analysis)
      if (mediaData.bpm !== undefined) {
        trackBpm.set(mediaData.bpm);
      }

      // Log timeline data (skip full media/thumbnail to reduce noise)
      if (mediaData.timeline) {
        console.log(
          "[WS] timeline:",
          mediaData.timeline,
          "playback:",
          mediaData.playback,
        );
      }

      // Проверяем что media существует (title/artist могут быть пустыми строками)
      if (
        mediaData &&
        mediaData.media &&
        (mediaData.media.title !== undefined ||
          mediaData.media.artist !== undefined)
      ) {
        // New media arrived — cancel any pending hide
        if (hideTimeout) {
          clearTimeout(hideTimeout);
          hideTimeout = null;
        }

        const newTitle = mediaData.media.title || "Unknown";
        const newArtist = mediaData.media.artist || "Unknown";
        const thumbnailObj = mediaData.media.thumbnail;

        // base64 может быть строкой, массивом байтов, или null
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
          if (base64 === null) {
            // Wait for 3 null thumbnails before showing placeholder
            nullThumbnailCount++;
            console.log("[WS] No thumbnail (attempt " + nullThumbnailCount + "/3)");
            if (nullThumbnailCount < 3) {
              // Keep old thumbnail, skip the rest this cycle
              if (mediaData.timeline) updateTimeline(mediaData.timeline, mediaData.playback);
              return;
            }
            // 3 consecutive nulls — show placeholder
            nullThumbnailCount = 0;
            console.log("[WS] No thumbnail after 3 attempts — using placeholder");
            if (currentBlobUrl) { URL.revokeObjectURL(currentBlobUrl); currentBlobUrl = null; currentBlob = null; }
            newThumbnailUrl = null;
          } else {
            nullThumbnailCount = 0; // Reset counter
            // Освобождаем старый blob URL
            if (currentBlobUrl) {
              URL.revokeObjectURL(currentBlobUrl);
              currentBlobUrl = null;
              currentBlob = null;
            }

            // Декодируем base64 в blob
            if (Array.isArray(base64)) {
              const bytes = new Uint8Array(base64);
              const blob = new Blob([bytes], { type: "image/png" });
              currentBlob = blob;
              currentBlobUrl = URL.createObjectURL(blob);
              newThumbnailUrl = currentBlobUrl;
            } else if (typeof base64 === "object" && base64 !== null && base64.data && Array.isArray(base64.data)) {
              const bytes = new Uint8Array(base64.data);
              const blob = new Blob([bytes], { type: "image/png" });
              currentBlob = blob;
              currentBlobUrl = URL.createObjectURL(blob);
              newThumbnailUrl = currentBlobUrl;
            } else if (typeof base64 === "string") {
              let cleanBase64 = base64;
              if (cleanBase64.includes(",")) cleanBase64 = cleanBase64.split(",")[1];
              cleanBase64 = cleanBase64.replace(/[\s\r\n]/g, "");
              try {
                const binaryString = atob(cleanBase64);
                const bytes = new Uint8Array(binaryString.length);
                for (let i = 0; i < binaryString.length; i++) bytes[i] = binaryString.charCodeAt(i);
                const blob = new Blob([bytes], { type: "image/png" });
                currentBlob = blob;
                currentBlobUrl = URL.createObjectURL(blob);
                newThumbnailUrl = currentBlobUrl;
              } catch (decodeErr) {
                console.error("[WS] Failed to decode base64:", decodeErr);
                if (base64.startsWith("data:")) newThumbnailUrl = base64;
              }
            }

            // Crop Spotify branding bar (only on Windows 10 + Spotify)
            if (isWindows10 && (mediaData.source || "").toLowerCase().includes('spotify') && currentBlob) {
              try {
                const croppedBlob = await cropSpotifyThumbnail(currentBlob);
                if (croppedBlob !== currentBlob) {
                  URL.revokeObjectURL(currentBlobUrl);
                  currentBlobUrl = URL.createObjectURL(croppedBlob);
                  newThumbnailUrl = currentBlobUrl;
                  currentBlob = croppedBlob;
                }
              } catch (e) {
                console.warn("[WS] Thumbnail crop failed:", e);
              }
            }

            // Extract colors BEFORE updating stores (atomic update)
            if (newThumbnailUrl && Vibrant) {
              try {
                const palette = await Vibrant.from(newThumbnailUrl).getPalette();
                rgbToHex(palette);
                window.dispatchEvent(new CustomEvent('unik-colors-updated'));
              } catch (err) {
                console.error("[Vibrant] Failed to extract palette:", err);
              }
            }
          }
        }

        // ATOMIC UPDATE: all stores at once — colors already on :root
        title.set(newTitle);
        artist.set(newArtist);
        thumbnail.set(newThumbnailUrl);
        ShowTrack.set(true);

        // Update timeline/progress data
        updateTimeline(mediaData.timeline, mediaData.playback);
      } else if (mediaData && mediaData.media === null) {
        // Медиа остановлено — скрываем с задержкой (при смене трека
        // бэкенд кратковременно шлёт null, потом новый трек)
        console.log("[WS] Media null — scheduling hide (1500ms)");
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

        // Check if playback stopped (pause/stop) — schedule hide
        const playing =
          mediaData.playback && mediaData.playback.playbackStatus === 4;
        if (!playing && get(ShowTrack)) {
          console.log("[WS] Playback paused/stopped — scheduling hide");
          if (hideTimeout) clearTimeout(hideTimeout);
          hideTimeout = setTimeout(() => {
            hideTimeout = null;
            // Check again — maybe resumed during the delay
            if (!get(isPlaying)) {
              console.log("[WS] Still paused — hiding player");
              ShowTrack.set(false);
              lastTitle = null;
              lastArtist = null;
              lastThumbnailHash = null;
              updateTimeline(null, null);
            }
          }, 600);
        } else if (playing && hideTimeout) {
          // Resumed — cancel pending hide
          clearTimeout(hideTimeout);
          hideTimeout = null;
          if (!get(ShowTrack)) {
            ShowTrack.set(true);
          }
        }
      }
      // Если данные неполные - просто игнорируем, оставляем предыдущее состояние
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
    reconnectTimeout = Math.min(reconnectTimeout * 2, 5000);
  };
}
