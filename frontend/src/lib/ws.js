
import { Vibrant } from "node-vibrant/browser";
import { rgbToHex } from './convertToHex.js';

import { title, artist, thumbnail, ShowTrack} from './stores/stores.js'
import { get } from 'svelte/store';

let mediaData = null;
let ws;
let reconnectTimeout = 1000;
let currentBlobUrl = null;
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

          // Обновляем только если что-то изменилось
          const titleChanged = get(title) !== newTitle;
          const artistChanged = get(artist) !== newArtist;
          const thumbChanged = base64 !== lastBase64;

          if (titleChanged || artistChanged || thumbChanged) {
            // Обновляем title и artist
            if (titleChanged) title.set(newTitle);
            if (artistChanged) artist.set(newArtist);

            // Обновляем thumbnail если изменился
            if (thumbChanged) {
              lastBase64 = base64;

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

              thumbnail.set(currentBlobUrl);

              Vibrant.from(currentBlobUrl)
               .getPalette()
               .then((palette) => rgbToHex(palette))
               .catch(() => {});
            }

            ShowTrack.set(true);
          }
        } else if (mediaData && mediaData.media === null) {
          // Медиа остановлено - скрываем трек
          ShowTrack.set(false);
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