
import { Vibrant } from "node-vibrant/browser";
import { rgbToHex } from './convertToHex.js';

import { title, artist, thumbnail, ShowTrack } from './stores/stores.js'
import { get } from 'svelte/store';

let mediaData = null;
let ws;
let reconnectTimeout = 1000;
let currentBlobUrl = null;

// Track last values to prevent duplicate updates
let lastTitle = null;
let lastArtist = null;
let lastBase64 = null;

export function connect() {
    const url = 'ws://localhost:62727';

    ws = new WebSocket(url);

    ws.onopen = () => {
      console.log('[WS] Connected');
      reconnectTimeout = 1000;
    };

    ws.onmessage = (e) => {
      try {
        mediaData = JSON.parse(e.data);
        console.log(mediaData);

        // Проверяем что media существует и имеет ВСЕ необходимые данные
        if (mediaData && mediaData.media &&
            mediaData.media.title &&
            mediaData.media.artist &&
            mediaData.media.thumbnail &&
            mediaData.media.thumbnail.data) {

          const newTitle = mediaData.media.title;
          const newArtist = mediaData.media.artist;
          const base64 = mediaData.media.thumbnail.data;

          // Check if ANYTHING changed - compare with cached values
          const titleChanged = lastTitle !== newTitle;
          const artistChanged = lastArtist !== newArtist;
          const thumbChanged = lastBase64 !== base64;

          // Skip if nothing changed (duplicate message)
          if (!titleChanged && !artistChanged && !thumbChanged) {
            console.log('[WS] Duplicate message, skipping');
            return;
          }

          console.log('[WS] Track changed:', { titleChanged, artistChanged, thumbChanged });

          // Update cached values FIRST
          lastTitle = newTitle;
          lastArtist = newArtist;
          lastBase64 = base64;

          // Create new blob URL if thumbnail changed
          let newThumbnailUrl = get(thumbnail);
          if (thumbChanged) {
            // Освобождаем старый blob URL
            if (currentBlobUrl) {
              URL.revokeObjectURL(currentBlobUrl);
            }

            // Декодируем base64 в blob
            const binaryString = atob(base64);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
              bytes[i] = binaryString.charCodeAt(i);
            }
            const blob = new Blob([bytes], { type: 'image/png' });
            currentBlobUrl = URL.createObjectURL(blob);
            newThumbnailUrl = currentBlobUrl;

            // Extract colors from new thumbnail
            Vibrant.from(currentBlobUrl)
             .getPalette()
             .then((palette) => {
               console.log('[Vibrant] Palette extracted:', palette);
               rgbToHex(palette);
             })
             .catch((err) => {
               console.error('[Vibrant] Failed to extract palette:', err);
             });
          }

          // ATOMIC UPDATE: Set all three stores at once to trigger single {#key} change
          title.set(newTitle);
          artist.set(newArtist);
          thumbnail.set(newThumbnailUrl);
          ShowTrack.set(true);

        } else if (mediaData && mediaData.media === null) {
          // Медиа остановлено - скрываем трек
          ShowTrack.set(false);
          // Reset cached values
          lastTitle = null;
          lastArtist = null;
          lastBase64 = null;
        }
        // Если данные неполные - просто игнорируем, оставляем предыдущее состояние

      } catch(err) {
        console.error('[WS] Parsing error', err);
      }
    };
  
    ws.onerror = (e) => {
      console.error('[WS] Error', e);
    };
  
    ws.onclose = () => {
      console.log('[WS] Closed, reconnect in', reconnectTimeout, 'ms');
      setTimeout(connect, reconnectTimeout);
      reconnectTimeout = Math.min(reconnectTimeout * 2, 30000);
    };
  }