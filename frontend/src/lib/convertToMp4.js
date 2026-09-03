// Browser-side ffmpeg.wasm helper: converts GIF/AVIF to a format that plays in <video>.
// Tries to keep transparency: WebM (VP9/VP8 with yuva420p alpha) first, then MP4 h264.
// If the wasm instance crashes, it is terminated and recreated for the next attempt.
let ffmpegPromise = null;
let lastLog = '';

export function needsConvert(name) {
  return /\.(gif|avif)$/i.test(name);
}

// Sniffs whether a file is actually an animated image, even without a name extension.
// Returns 'gif' | 'avif' | null.
export async function detectAnimImage(file) {
  try {
    const buf = new Uint8Array(await file.slice(0, 16).arrayBuffer());
    const head = String.fromCharCode(...buf);
    if (head.startsWith('GIF8')) return 'gif';
    if (head.substring(4, 8) === 'ftyp') {
      const brand = head.substring(8, 12);
      if (/^(avif|avis|av01|mif1|msf1)/.test(brand)) return 'avif';
    }
  } catch {}
  return null;
}

async function getFFmpeg() {
  if (ffmpegPromise) return ffmpegPromise;
  ffmpegPromise = (async () => {
    const { FFmpeg } = await import('@ffmpeg/ffmpeg');
    const { toBlobURL } = await import('@ffmpeg/util');
    const ffmpeg = new FFmpeg();
    ffmpeg.on('log', ({ message }) => {
      if (message && /error|not found|failed|unable|unknown|out of bounds/i.test(message)) {
        lastLog = message;
      }
    });
    try {
      await ffmpeg.load({
        coreURL: await toBlobURL('/ffmpeg-core.js', 'text/javascript'),
        wasmURL: await toBlobURL('/ffmpeg-core.wasm', 'application/wasm'),
      });
    } catch (e) {
      const isolated = typeof self !== 'undefined' && self.crossOriginIsolated;
      const hint = isolated
        ? ''
        : ' (страница не cross-origin isolated — ffmpeg.wasm не может стартовать; открой через localhost:5173 в dev)';
      throw new Error('ffmpeg load failed' + hint + ': ' + (e && e.message ? e.message : e));
    }
    return ffmpeg;
  })();
  return ffmpegPromise;
}

async function resetFFmpeg() {
  try {
    const old = await ffmpegPromise;
    if (old && typeof old.terminate === 'function') old.terminate();
  } catch {}
  ffmpegPromise = null;
}

// Single fast h264 MP4 pass for opaque sources.
const H264_ATTEMPT = {
  out: 'out.mp4',
  type: 'video/mp4',
  ext: '.mp4',
  args: [
    '-pix_fmt', 'yuv420p',
    '-c:v', 'libx264',
    '-preset', 'medium',
    '-crf', '14',
    '-movflags', '+faststart',
    '-an'
  ]
};

// Single VP9 WebM pass that keeps transparency (used for sources with alpha).
const VP9_ALPHA_ATTEMPT = {
  out: 'out_alpha.webm',
  type: 'video/webm',
  ext: '.webm',
  args: [
    '-pix_fmt', 'yuva420p',
    '-c:v', 'libvpx-vp9',
    '-crf', '22',
    '-b:v', '0',
    '-cpu-used', '4',
    '-auto-alt-ref', '0',
    '-an'
  ]
};

// VP8 WebM with alpha — more robust fallback when VP9 alpha fails.
const VP8_ALPHA_ATTEMPT = {
  out: 'out_alpha_vp8.webm',
  type: 'video/webm',
  ext: '.webm',
  args: [
    '-pix_fmt', 'yuva420p',
    '-c:v', 'libvpx',
    '-crf', '8',
    '-b:v', '0',
    '-auto-alt-ref', '0',
    '-cpu-used', '6',
    '-deadline', 'good',
    '-an'
  ]
};

const str = (e) => {
  if (e == null) return 'unknown error';
  if (typeof e === 'string') return e;
  if (e.message) return String(e.message);
  try { return JSON.stringify(e); } catch { return String(e); }
};

// Detects (from the first frame) whether an image has any transparency.
export async function hasTransparency(file) {
  try {
    const url = URL.createObjectURL(file);
    try {
      const img = new Image();
      img.src = url;
      await img.decode();
      const w = Math.min(img.naturalWidth || 1, 320);
      const h = Math.min(img.naturalHeight || 1, 320);
      const cv = document.createElement('canvas');
      cv.width = w;
      cv.height = h;
      const ctx = cv.getContext('2d', { willReadFrequently: true });
      ctx.drawImage(img, 0, 0, w, h);
      const data = ctx.getImageData(0, 0, w, h).data;
      for (let i = 3; i < data.length; i += 4) {
        if (data[i] < 250) return true;
      }
      return false;
    } finally {
      URL.revokeObjectURL(url);
    }
  } catch {
    return false;
  }
}

export async function convertToMp4(file, onProgress = null, webmAlpha = false) {
  const { fetchFile } = await import('@ffmpeg/util');
  const ENCODE_ATTEMPTS = webmAlpha
    ? [VP9_ALPHA_ATTEMPT, VP8_ALPHA_ATTEMPT, H264_ATTEMPT]
    : [H264_ATTEMPT];
  const base = file.name.replace(/\.(gif|avif)$/i, '');
  console.log(`[ffmpeg] конвертация "${file.name}" (${(file.size / 1024).toFixed(1)} KB) → ${webmAlpha ? 'WebM VP9 (alpha)' : 'MP4 h264'}`);
  const started = performance.now();
  let lastPct = -1;

  for (const attempt of ENCODE_ATTEMPTS) {
    let ffmpeg = await getFFmpeg();
    const attemptStart = performance.now();
    console.log(`[ffmpeg] → пробую: ${attempt.out} (${attempt.args.filter(a => a.startsWith('-')).join(' ').trim() || '...'})`);
    const unsubProgress = ffmpeg.on('progress', ({ progress }) => {
      if (typeof progress === 'number') {
        const pct = Math.max(0, Math.min(100, Math.round(progress * 100)));
        if (pct !== lastPct) {
          lastPct = pct;
          console.log(`[ffmpeg]   прогресс: ${pct}%`);
          if (onProgress) onProgress(pct);
        }
      }
    });
    try {
      try { await ffmpeg.deleteFile('in'); } catch {}
      try { await ffmpeg.deleteFile(attempt.out); } catch {}
      // writeFile transfers the buffer to the worker and detaches it,
      // so fetch a fresh copy for every attempt
      await ffmpeg.writeFile('in', await fetchFile(file));

      const code = await ffmpeg.exec([
        '-y',
        '-i', 'in',
        '-vf', 'scale=trunc(iw/2)*2:trunc(ih/2)*2',
        ...attempt.args,
        attempt.out
      ]);
      if (code !== 0) {
        console.warn(`[ffmpeg]   ✖ попытка ${attempt.out}: код выхода ${code}${lastLog ? ' — ' + lastLog : ''}`);
        continue; // encoder missing/failed -> try next
      }

      const data = await ffmpeg.readFile(attempt.out);
      if (data && data.length) {
        const secs = ((performance.now() - attemptStart) / 1000).toFixed(1);
        const secsTotal = ((performance.now() - started) / 1000).toFixed(1);
        console.log(`[ffmpeg]   ✔ готово за ${secs}s (итого ${secsTotal}s), размер ${(data.length / 1024).toFixed(1)} KB → ${base}${attempt.ext}`);
        return new File([data], base + attempt.ext, { type: attempt.type });
      }
      console.warn(`[ffmpeg]   ✖ попытка ${attempt.out}: пустой вывод`);
    } catch (err) {
      // wasm trap / memory corruption: the instance is poisoned — make a fresh one
      console.warn('[ffmpeg]   ✖ попытка упала, пересоздаю инстанс:', str(err));
      await resetFFmpeg();
    } finally {
      try { if (unsubProgress) unsubProgress(); } catch {}
      try { await ffmpeg.deleteFile('in'); } catch {}
      try { await ffmpeg.deleteFile(attempt.out); } catch {}
    }
  }

  throw new Error('no video encoder available' + (lastLog ? ` (${lastLog})` : ''));
}
