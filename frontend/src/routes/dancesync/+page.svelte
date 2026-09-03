<script>
  import { onMount, onDestroy } from 'svelte';
  import { fly, fade } from 'svelte/transition';
  import { trackBpm } from '$lib/stores/stores.js';
  import SmoothNumber from '$lib/components/SmoothNumber.svelte';
  import GifThumb from '$lib/components/GifThumb.svelte';
  import DanceGuide from '$lib/components/DanceGuide.svelte';
  import { fetchGifs, uploadGif, deleteGif, gifUrl, obSLink, nameNoExt } from '$lib/danceGifs.js';
  import { convertToMp4, detectAnimImage, hasTransparency } from '$lib/convertToMp4.js';

  // ─── Page transitions ─────────────────────────────
  let entered = false;
  let guideOpen = false;

  function closeGuide() {
    guideOpen = false;
    try { localStorage.setItem('dancesync_guide_shown', '1'); } catch {}
  }

  // ─── State ───────────────────────────────────────────────
  let targetBPM = 100;
  let playbackRate = 1.0;
  let videoEl = null;
  let beatCanvas = null;

  const BEAT_BAR_HEIGHT = 52;
  const TICK_SPACING_PX = 60;

  // ─── BPM sync ────────────────────────────────────────────
  let detectedBpm = null;
  const baseBpm = 120;
  let basicSpeed = 1.0;
  let unsubBpm = null;

  function applyAutoRate() {
    if (!detectedBpm) {
      playbackRate = basicSpeed;
    } else {
      const rate = Math.min(4.0, Math.max(0.25, (basicSpeed * detectedBpm) / baseBpm));
      playbackRate = +rate.toFixed(3);
    }
    if (videoEl) videoEl.playbackRate = playbackRate;
  }

  unsubBpm = trackBpm.subscribe(v => {
    detectedBpm = v;
    if (v) {
      targetBPM = v;
      applyAutoRate();
    }
  });

  $: if (detectedBpm && baseBpm) {
    targetBPM = detectedBpm;
    applyAutoRate();
  }

  $: basicSpeed, applyAutoRate();

  // ─── Basic speed: one saved multiplier per gif ────────────
  const SPEEDS_KEY = 'dancesync_basicSpeeds';

  function loadSpeeds() {
    try { return JSON.parse(localStorage.getItem(SPEEDS_KEY) || '{}'); } catch { return {}; }
  }

  function clampSpeed(v) {
    return Math.min(2.0, Math.max(0.5, v));
  }

  // Legacy global fallback (still used by the OBS player page as a default)
  try {
    const raw = localStorage.getItem('dancesync_basicSpeed');
    if (raw) basicSpeed = clampSpeed(parseFloat(raw) || 1.0);
  } catch {}

  function persistSpeed(key) {
    try {
      const m = loadSpeeds();
      m[key] = basicSpeed;
      localStorage.setItem(SPEEDS_KEY, JSON.stringify(m));
    } catch {}
  }

  function applyBasicSpeed(key) {
    const v = loadSpeeds()[key];
    basicSpeed = typeof v === 'number' && isFinite(v) ? clampSpeed(v) : 1.0;
  }

  function setBasicSpeed(v) {
    basicSpeed = clampSpeed(+(v.toFixed(2)));
    if (activeName) persistSpeed(nameNoExt(activeName));
    try { localStorage.setItem('dancesync_basicSpeed', String(basicSpeed)); } catch {}
  }

  // ─── Saved GIF list (backend) ────────────────────────────
  let gifs = [];
  let activeName = null;
  let gifsLoading = true;
  let overlayOpen = false;
  let mainDragOver = false;
  let overlayDragOver = false;
  let pickerInput = null;
  let converting = false;
  let conversionPct = 0;
  let splitAnimate = false;
  let uploadError = null;

  async function refreshGifs() {
    try {
      gifs = await fetchGifs();
    } catch (e) {
      console.warn('[dancesync] load gifs failed:', e);
      gifs = [];
    }
    if (gifs.length > 0 && !gifs.some(g => g.name === activeName)) {
      activeName = gifs[0].name;
    }
    if (gifs.length === 0) activeName = null;
    if (!shownName || !gifs.some(g => g.name === shownName)) shownName = activeName;
    gifsLoading = false;
  }

  async function pickFile(file) {
    if (!file) return;
    uploadError = null;

    const byName = /\.gif$/i.test(file.name) ? 'gif' : /\.avif$/i.test(file.name) ? 'avif' : null;
    const detected = (await detectAnimImage(file)) || byName;

    if (detected === 'avif') {
      uploadError = 'AVIF не поддерживается приложением. Поддерживаются: GIF, MP4, WebM — перекодируй файл в один из этих форматов.';
      return;
    }

    // Convert real animated gifs to a playable video (even without a name extension)
    if (detected === 'gif') {
      converting = true;
      conversionPct = 0;
      try {
        // transparent gifs keep their alpha via WebM VP9, the rest become fast MP4
        const alpha = await hasTransparency(file);
        file = await convertToMp4(file, (pct) => { conversionPct = pct; }, alpha);
      } catch (e) {
        console.error('[dancesync] convert failed:', e);
        converting = false;
        conversionPct = 0;
        uploadError = 'Не удалось переделать GIF в видео: ' + (e && e.message ? e.message : e);
        return; // never upload a raw gif — it can't play in <video>
      }
      converting = false;
      conversionPct = 100;
    }
    const wasEmpty = gifs.length === 0;
    try {
      const data = await uploadGif(file);
      overlayOpen = false;
      mainDragOver = false;
      overlayDragOver = false;
      if (wasEmpty) splitAnimate = true;
      await refreshGifs();
      activeName = data.name;
      shownName = data.name;
      applyPreset(nameNoExt(data.name));
      applyBasicSpeed(nameNoExt(data.name));
      applyAutoRate();
    } catch (e) {
      console.error('[dancesync] upload failed:', e);
      uploadError = e.message || 'Не удалось загрузить файл';
    }
  }

  function onPickerChange() {
    const file = pickerInput?.files?.[0];
    if (file) pickFile(file);
    if (pickerInput) pickerInput.value = '';
  }

  function pickFromDialog() {
    pickerInput?.click();
  }

  function onDrop(e) {
    e.preventDefault();
    mainDragOver = false;
    overlayDragOver = false;
    const file = e.dataTransfer?.files?.[0];
    if (file) pickFile(file);
  }

  async function removeGif(g, e) {
    e.stopPropagation();
    const prevGifs = gifs;
    const prevActive = activeName;
    const prevShown = shownName;
    gifs = gifs.filter(x => x.name !== g.name);
    if (prevActive === g.name) {
      activeName = gifs[0]?.name ?? null;
      shownName = activeName;
      resetVideoStyles();
      if (activeName) applyBasicSpeed(nameNoExt(activeName));
    }
    try {
      await deleteGif(g.name);
      // forget that gif's own basic speed
      try {
        const m = loadSpeeds();
        delete m[nameNoExt(g.name)];
        localStorage.setItem(SPEEDS_KEY, JSON.stringify(m));
      } catch {}
    } catch (err) {
      console.warn('[dancesync] delete failed:', err);
      gifs = prevGifs;
      activeName = prevActive;
      shownName = prevShown;
    }
  }

  function selectGif(g) {
    switchGif(g.name);
  }

  // ─── Preview swap animation: old shrinks inward, then new grows from center ───
  let shownName = null;     // drives the actual <video> src
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));
  let fxToken = 0;

  function resetVideoStyles() {
    const el = videoEl;
    if (el) {
      el.style.transition = '';
      el.style.transform = '';
      el.style.opacity = '';
    }
  }

  async function switchGif(name) {
    if (!name || name === shownName) return;
    const token = ++fxToken;
    activeName = name;                 // typewriter name starts immediately
    applyPreset(nameNoExt(name));
    applyBasicSpeed(nameNoExt(name));  // that gif's own basic speed

    const el = videoEl;
    try {
      // shrink the current picture inward
      if (el) {
        el.style.transition = 'transform 0.32s ease-in, opacity 0.32s ease-in';
        el.style.transform = 'scale(0.08)';
        el.style.opacity = '0';
      }
      await sleep(320);
      if (token !== fxToken) return;

      shownName = name;                // swap the source while scaled ~0
      await sleep(40);
      if (token !== fxToken) return;

      // grow the new picture from the center
      if (el) {
        el.style.transition = 'transform 0.5s cubic-bezier(0.34,1.56,0.64,1), opacity 0.5s';
        el.style.transform = 'scale(1)';
        el.style.opacity = '1';
      }
      await sleep(520);
    } finally {
      resetVideoStyles();
    }
    try { videoEl?.play(); } catch {}
  }

  // ─── Preview name "rewrites" itself when the gif changes ───
  let nameShown = '';
  let lastNameKey = null;
  let nameRaf = null;

  function animateName(target) {
    if (nameRaf) cancelAnimationFrame(nameRaf);
    const from = nameShown;
    const t0 = performance.now();
    const DUR = 900;
    const MID = 420;
    const step = (t) => {
      const el = t - t0;
      if (el < MID) {
        const p = from.length * (1 - el / MID);
        nameShown = from.slice(0, Math.max(0, Math.round(p)));
      } else {
        const p = target.length * Math.min(1, (el - MID) / (DUR - MID));
        nameShown = target.slice(0, Math.max(0, Math.round(p)));
      }
      if (el < DUR) {
        nameRaf = requestAnimationFrame(step);
      } else {
        nameShown = target;
      }
    };
    nameRaf = requestAnimationFrame(step);
  }

  $: if (activeName && activeName !== lastNameKey) {
    lastNameKey = activeName;
    animateName(nameNoExt(activeName));
  }

  // Copy feedback: transient "copied" popup over the list slot
  let copiedNames = new Set();

  async function copyLink(g) {
    try {
      await navigator.clipboard.writeText(obSLink(g.name));
      copiedNames = new Set([...copiedNames, g.name]);
      setTimeout(() => {
        copiedNames = new Set([...copiedNames].filter(n => n !== g.name));
      }, 1200);
    } catch {}
  }

  let copiedActive = false;
  let copiedTimer = null;

  async function copyActiveLink() {
    if (!activeName) return;
    try {
      await navigator.clipboard.writeText(obSLink(activeName));
      copiedActive = true;
      if (copiedTimer) clearTimeout(copiedTimer);
      copiedTimer = setTimeout(() => { copiedActive = false; }, 1300);
    } catch {}
  }

  // Visible shelf slots: always show a few empties so the list reads as a shelf
  const SLOT_COUNT = 5;

  // ─── Presets persistence (per gif name) ───────────────────
  const PRESETS_KEY = 'dancesync_presets';

  function loadPresets() {
    try {
      return JSON.parse(localStorage.getItem(PRESETS_KEY) || '{}');
    } catch { return {}; }
  }

  function savePreset() {
    if (!activeName) return;
    const presets = loadPresets();
    presets[nameNoExt(activeName)] = { playbackRate, targetBPM };
    try { localStorage.setItem(PRESETS_KEY, JSON.stringify(presets)); } catch {}
  }

  function applyPreset(gifKey) {
    const p = loadPresets()[gifKey];
    if (p) {
      playbackRate = p.playbackRate;
      targetBPM = p.targetBPM;
    }
  }

  $: activeName, targetBPM, playbackRate, savePreset();

  // ─── Logo overlap → progressive shrinking ─────────────────
  let pageEl = null;
  let logoEl = null;
  let debugEl = null;
  let logoStage = 0;

  const LOGO_TEXTS = ['[UNIKPLAYER]', '[UPLAYER]', 'UPLAYER', 'UNIK', 'U'];

  function measureStageWidth(stage) {
    const probe = document.createElement('span');
    probe.style.cssText =
      `position:fixed;visibility:hidden;white-space:nowrap;` +
      `font-family:'8bitwonder',monospace;font-size:32px;letter-spacing:0.05em;`;
    probe.textContent = LOGO_TEXTS[stage];
    document.body.appendChild(probe);
    const w = probe.getBoundingClientRect().width;
    probe.remove();
    return w;
  }

  function updateLogoStage() {
    if (!logoEl || !debugEl) return;
    const logoLeft = logoEl.getBoundingClientRect().left;
    const debugLeft = debugEl.getBoundingClientRect().left;
    const available = Math.max(0, debugLeft - logoLeft);
    let stage = 0;
    while (stage < 4 && measureStageWidth(stage) > available) stage++;
    logoStage = stage;
  }

  // ─── Dynamic accent from the track cover (Vibrant) ────────
  let accentColor = null;
  let accentRgb = [238, 34, 204];

  function parseColorToRgb(color) {
    const m = color.trim().match(/rgba?\(([^)]+)\)/);
    if (m) {
      const p = m[1].split(',').map(Number);
      return [p[0] ?? 238, p[1] ?? 34, p[2] ?? 204];
    }
    const hex = color.trim().replace('#', '');
    if (/^[0-9a-fA-F]{3}$/.test(hex)) {
      const v = hex.split('').map(c => c + c).join('');
      return [parseInt(v.slice(0, 2), 16), parseInt(v.slice(2, 4), 16), parseInt(v.slice(4, 6), 16)];
    }
    if (/^[0-9a-fA-F]{6}$/.test(hex)) {
      return [parseInt(hex.slice(0, 2), 16), parseInt(hex.slice(2, 4), 16), parseInt(hex.slice(4, 6), 16)];
    }
    return [238, 34, 204];
  }

  function readVibrantAccent() {
    try {
      const vibrant = getComputedStyle(document.documentElement).getPropertyValue('--vibrant').trim();
      if (!vibrant || vibrant === '#555555' || vibrant === '#D4944A') return;
      if (vibrant === accentColor || !pageEl) return;
      accentColor = vibrant;
      accentRgb = parseColorToRgb(vibrant);
      pageEl.style.setProperty('--accent', vibrant);
      console.log('[dancesync] accent:', vibrant);
    } catch {}
  }

  // ─── Preview size ─────────────────────────────────────────
  let previewSize = 380;
  let previewVideoH = 250;
  let glowEl = null;

  function computePreviewSize(vw) {
    if (vw >= 1920) return Math.round(vw * 0.22);
    if (vw <= 960) return Math.round(vw * 0.55);
    const t = (vw - 960) / (1920 - 960);
    return Math.round(vw * (0.55 - 0.33 * t));
  }

  function updatePreviewSize() {
    const size = computePreviewSize(window.innerWidth);
    const maxByWidth = window.innerWidth - 460;
    previewSize = Math.max(160, Math.min(size, maxByWidth, window.innerHeight - 330));
    previewVideoH = Math.max(120, previewSize - 140);
  }

  // ─── Mount / destroy ─────────────────────────────────────
  onMount(() => {
    refreshGifs();
    updatePreviewSize();
    // first visit: launch the guide automatically
    try {
      if (!localStorage.getItem('dancesync_guide_shown')) {
        guideOpen = true;
      }
    } catch {}
    const onResize = () => { updatePreviewSize(); updateLogoStage(); };
    window.addEventListener('resize', onResize);
    window.addEventListener('unik-colors-updated', readVibrantAccent);
    readVibrantAccent();

    requestAnimationFrame(() => {
      requestAnimationFrame(() => { entered = true; updateLogoStage(); });
    });

    // ─── Beat bar canvas animation ──────────────────────────
    let rafId;
    const ro = new ResizeObserver(() => {});
    if (beatCanvas?.parentElement) ro.observe(beatCanvas.parentElement);

    function render() {
      rafId = requestAnimationFrame(render);
      try {
      const canvas = beatCanvas;
      if (!canvas) return;
      const dpr = window.devicePixelRatio || 1;
      const cssW = canvas.clientWidth || canvas.parentElement?.clientWidth || 300;
      const W = Math.max(1, Math.round(cssW * dpr));
      const H = Math.max(1, Math.round(BEAT_BAR_HEIGHT * dpr));
      if (canvas.width !== W) canvas.width = W;
      if (canvas.height !== H) canvas.height = H;
      canvas.style.height = BEAT_BAR_HEIGHT + 'px';
      const ctx = canvas.getContext('2d');
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      const w = cssW;
      const h = BEAT_BAR_HEIGHT;
      const center = w / 2;
      const offset = (performance.now() / 1000 * targetBPM / 60 * TICK_SPACING_PX) % TICK_SPACING_PX;

      ctx.clearRect(0, 0, w, h);
      const [ar, ag, ab] = accentRgb;

      const beatAtCenter = Math.round((offset - center) / TICK_SPACING_PX);
      const isBarBeat = beatAtCenter % 4 === 0;
      // distance (0..0.5 beats) from the exact beat crossing to the centre playhead
      const phase = ((center - offset) % TICK_SPACING_PX + TICK_SPACING_PX) % TICK_SPACING_PX;
      const distPx = Math.min(phase, TICK_SPACING_PX - phase);
      const beatDist = distPx / TICK_SPACING_PX;
      const pulse = isBarBeat ? 1 - beatDist * 2 : 0;

      // Title above the gif glows briefly around the exact beat crossing
      // (same phase as the bar — tight 0.09s window, symmetric via beatDist)
      const beatSecs = beatDist * (60 / Math.max(1, targetBPM));
      const glowWindowSec = 0.09;
      const g = Math.max(0, Math.min(1, 1 - beatSecs / glowWindowSec));

      if (glowEl) {
        const on = g > 0;
        const had = glowEl.classList.contains('glow-beat');
        if (on !== had) {
          glowEl.classList.toggle('glow-beat', on);
        }
      }

      for (let x = -TICK_SPACING_PX + offset; x < w + TICK_SPACING_PX; x += TICK_SPACING_PX) {
        const tickIdx = Math.round((offset - x) / TICK_SPACING_PX);
        if (tickIdx % 4 === 0) {
          ctx.fillStyle = `rgba(${ar},${ag},${ab},0.95)`;
          ctx.fillRect(Math.round(x) - 1, 2, 2, h - 4);
        } else {
          ctx.fillStyle = `rgba(${ar},${ag},${ab},0.35)`;
          ctx.fillRect(Math.round(x) - 1, 5, 2, h - 10);
        }
      }

      const subSpacing = TICK_SPACING_PX / 4;
      for (let x = -subSpacing + (offset % subSpacing); x < w + subSpacing; x += subSpacing) {
        const rx = Math.round(x);
        const dist = Math.abs((rx - offset + TICK_SPACING_PX * 100) % TICK_SPACING_PX - TICK_SPACING_PX);
        if (dist < 4 || Math.abs(((rx - offset + TICK_SPACING_PX * 100) % TICK_SPACING_PX)) < 4) continue;
        ctx.fillStyle = `rgba(${ar},${ag},${ab},0.15)`;
        ctx.fillRect(rx, Math.round(h * 0.4), 1, Math.round(h * 0.2));
      }

      if (pulse > 0) {
        const ph = h * (0.7 + 1.3 * pulse);
        const pw = 2 + 4 * pulse;
        ctx.fillStyle = pulse > 0.6 ? '#ffffff' : `rgba(${ar},${ag},${ab},0.9)`;
        ctx.fillRect(Math.round(center) - pw / 2, (h - ph) / 2, pw, ph);
      }

      ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
      ctx.fillRect(Math.round(center), 0, 1, h);

      ctx.fillStyle = `rgba(${ar},${ag},${ab},0.4)`;
      ctx.fillRect(0, 0, w, 1);
      ctx.fillRect(0, h - 1, w, 1);
    } catch (err) {
      console.warn('[dancesync] beat render error:', err);
    }
  }

    rafId = requestAnimationFrame(render);
    return () => {
      cancelAnimationFrame(rafId);
      ro.disconnect();
      window.removeEventListener('resize', onResize);
      window.removeEventListener('unik-colors-updated', readVibrantAccent);
    };
  });

  onDestroy(() => {
    if (unsubBpm) unsubBpm();
  });
</script>

<div class="page" class:entered bind:this={pageEl}>
  <!-- Brand logo: pinned to the top-left corner; text shrinks when it overlaps the debug strip -->
  <span class="logo" bind:this={logoEl}>
    {#if logoStage === 0}
      [<span class="logo-accent">UNIK</span>PLAYER]
    {:else if logoStage === 1}
      [<span class="logo-accent">U</span>PLAYER]
    {:else if logoStage === 2}
      <span class="logo-accent">U</span>PLAYER
    {:else if logoStage === 3}
      <span class="logo-accent">UNIK</span>
    {:else}
      <span class="logo-accent">U</span>
    {/if}
  </span>

  <div class="shell">
    <div class="toprow anim-title">
      <div class="debug-panel" bind:this={debugEl}>
        <div class="dg-col">
          <span class="dg-label">BPM</span>
          <span class="dg-value"><SmoothNumber value={detectedBpm} /></span>
        </div>
        <div class="dg-col">
          <span class="dg-label">TARGET</span>
          <span class="dg-value"><SmoothNumber value={targetBPM} /></span>
        </div>
        <div class="dg-col">
          <span class="dg-label">RATE</span>
          <span class="dg-value"><SmoothNumber value={playbackRate} decimals={2} suffix="x" /></span>
        </div>
        <div class="dg-col">
          <span class="dg-label">BASIC</span>
          <span class="dg-value"><SmoothNumber value={basicSpeed} decimals={2} /></span>
        </div>
      </div>

      <button class="guide-open" on:click={() => guideOpen = true}>ГАЙД</button>
    </div>

    <!-- ═══ Center: empty zone OR list + preview; overlay floats on top ═══ -->
    <div class="center">
      <input
        bind:this={pickerInput}
        type="file"
        accept=".gif,.avif,.mp4,.webm"
        class="file-input-hidden"
        on:change={onPickerChange}
      />

      <!-- Both states live in absolute layers, so they overlap during transitions
           instead of pushing each other around -->
      {#if gifs.length === 0}
        <div class="state-layer" in:fly={{ y: 160, duration: 450 }} out:fly={{ y: -90, duration: 250 }}>
          <div
            class="drop-empty anim-left"
            class:drag-over={mainDragOver}
            on:dragover|preventDefault={() => mainDragOver = true}
            on:dragleave={() => mainDragOver = false}
            on:drop={onDrop}
            role="button"
            tabindex="0"
            on:click={pickFromDialog}
            on:keydown={e => e.key === 'Enter' && pickFromDialog()}
          >
          {#if gifsLoading}
            <div class="drop-hint">Загрузка…</div>
          {:else if converting}
            <div class="drop-hint">Конвертация…</div>
            <div class="drop-pct">{conversionPct}%</div>
          {:else}
            <div class="drop-icon">+</div>
            <div class="drop-hint">Перетащи файл сюда</div>
            <div class="drop-sub">или нажми, чтобы выбрать на компьютере</div>
          {/if}
            {#if uploadError}
              <div class="drop-error">{uploadError}</div>
            {/if}
          </div>
        </div>
      {:else}
        <div class="state-layer" out:fly={{ y: -90, duration: 250 }}>
          <!-- List on the far left, preview on the far right -->
          <div class="split-view" class:list-in={splitAnimate} class:preview-in={splitAnimate}>
            <div class="gif-list-pane anim-left">
              <button class="add-btn" on:click={() => overlayOpen = true}>ADD GIF</button>

              <div class="gif-list">
                {#each gifs as g (g.name)}
                  <div
                    class="gif-item"
                    class:active={g.name === activeName}
                    role="button"
                    tabindex="0"
                    out:fly={{ y: -14, opacity: 0, duration: 180 }}
                    on:click={() => selectGif(g)}
                    on:keydown={e => e.key === 'Enter' && selectGif(g)}
                  >
                    <div class="thumb">
                      <GifThumb name={g.name} ext={g.ext} />
                    </div>
                    <span class="gif-name" title={g.name}>{nameNoExt(g.name)}</span>
                    <button
                      class="item-btn copy-btn"
                      title="Скопировать ссылку для OBS"
                      on:click|stopPropagation={() => copyLink(g)}
                    >&#128279;</button>
                    <button
                      class="item-btn del-btn"
                      title="Удалить"
                      on:click|stopPropagation={(e) => removeGif(g, e)}
                    >&times;</button>
                    {#if copiedNames.has(g.name)}
                      <span class="copied-pop" in:fade={{ duration: 120 }} out:fade={{ duration: 300 }}>СКОПИРОВАНО</span>
                    {/if}
                  </div>
                {/each}
                {#each Array(Math.max(0, SLOT_COUNT - gifs.length)) as _, i}
                  <div
                    class="gif-slot-empty"
                    role="button"
                    tabindex="0"
                    in:fade={{ duration: 250 }}
                    on:click={() => overlayOpen = true}
                    on:keydown={e => e.key === 'Enter' && (overlayOpen = true)}
                  ></div>
                {/each}
              </div>
            </div>

            <div class="gif-preview-pane anim-right">
              {#if shownName}
                <div class="preview-col">
                  <div class="preview-name" bind:this={glowEl} title={nameNoExt(activeName || shownName)}>
                    {nameShown.length > 10 ? nameShown.slice(0, 10) + '…' : nameShown}
                  </div>
                  <div class="preview-glow">
                    <video
                      bind:this={videoEl}
                      src={gifUrl(shownName)}
                      loop
                      muted
                      autoplay
                      playsinline
                      controls={false}
                      class="preview-video"
                      style="width:{previewSize}px;height:{previewVideoH}px;"
                      on:loadedmetadata={() => { applyAutoRate(); videoEl?.play().catch(() => {}); }}
                    ></video>
                  </div>
                  <button
                    class="obs-btn"
                    class:copied={copiedActive}
                    on:click={copyActiveLink}
                  >
                    <span class="obs-label">КОПИРОВАТЬ ССЫЛКУ</span>
                    {#if copiedActive}
                      <span class="obs-copied" in:fade={{ duration: 150 }}>СКОПИРОВАНО!</span>
                    {/if}
                  </button>
                </div>
              {/if}
            </div>
          </div>
        </div>
      {/if}

      <!-- "+ ADD" overlay: floats OVER the current content (which stays visible) -->
      {#if overlayOpen}
        <div
          class="overlay"
          class:drag-over={overlayDragOver}
          role="button"
          tabindex="0"
          on:dragover|preventDefault={() => overlayDragOver = true}
          on:dragleave={() => overlayDragOver = false}
          on:drop={onDrop}
          on:click={() => overlayOpen = false}
          on:keydown={e => e.key === 'Escape' && (overlayOpen = false)}
        >
          <div
            class="overlay-box"
            role="button"
            tabindex="0"
            on:click|stopPropagation={pickFromDialog}
            on:keydown={e => e.key === 'Enter' && pickFromDialog()}
          >
            {#if converting}
              <div class="drop-hint">Конвертация…</div>
              <div class="drop-pct">{conversionPct}%</div>
            {:else}
              <div class="drop-icon">+</div>
              <div class="drop-hint">Перетащи файл сюда</div>
              <div class="drop-sub">или нажми, чтобы выбрать на компьютере</div>
            {/if}
            {#if uploadError}
              <div class="drop-error">{uploadError}</div>
            {/if}
            <button class="close-btn" on:click|stopPropagation={() => overlayOpen = false} aria-label="Закрыть">
              &times;
            </button>
          </div>
        </div>
      {/if}
    </div>

    <!-- ═══ Bottom pinned: bpm/speed info above the running strip ═══ -->
    <div class="bottom-block anim-right">
      <div class="info-row">
        <div class="info-cell">
          <span class="info-label">BPM</span>
          <span class="info-value"><SmoothNumber value={detectedBpm} /></span>
        </div>

        <div class="info-cell">
          <span class="info-label">BASIC SPEED</span>
          <div class="stepper">
            <button class="cruise-btn cruise-far" title="−0.05" on:click={() => setBasicSpeed(basicSpeed - 0.05)}>&laquo;&laquo;</button>
            <button class="cruise-btn" title="−0.01" on:click={() => setBasicSpeed(basicSpeed - 0.01)}>&laquo;</button>
            <span class="cruise-value"><SmoothNumber value={basicSpeed} decimals={2} duration={320} minDelta={0.002} /></span>
            <button class="cruise-btn" title="+0.01" on:click={() => setBasicSpeed(basicSpeed + 0.01)}>&raquo;</button>
            <button class="cruise-btn cruise-far" title="+0.05" on:click={() => setBasicSpeed(basicSpeed + 0.05)}>&raquo;&raquo;</button>
          </div>
        </div>

        <div class="info-cell">
          <span class="info-label">SPEED</span>
          <span class="info-value"><SmoothNumber value={playbackRate} decimals={2} suffix="x" /></span>
        </div>
      </div>

      <div class="beat-bar-container">
        <canvas bind:this={beatCanvas} class="beat-canvas" height={BEAT_BAR_HEIGHT}></canvas>
      </div>
    </div>
  </div>
</div>

{#if guideOpen}
  <DanceGuide onClose={closeGuide} />
{/if}

<style>
  :global(header) { display: none !important; }
  :global(html), :global(body) {
    height: 100%;
    overflow: hidden;
  }

  @property --accent {
    syntax: '<color>';
    inherits: true;
    initial-value: #ee22cc;
  }

  .page {
    height: 100vh;
    box-sizing: border-box;
    overflow: hidden;
    background: var(--c1);
    color: var(--c2);
    font-family: '8bitwonder', monospace;
    padding: 1rem 1.5rem 1.25rem;
    display: flex;
    flex-direction: column;
    transition: opacity 0.4s ease, --accent 1.2s ease;
    clip-path: polygon(
      0 0,
      calc(100% - 12px) 0,
      100% 12px,
      100% 100%,
      12px 100%,
      0 calc(100% - 12px)
    );
    opacity: 0;
  }
  .page.entered { opacity: 1; }

  .shell {
    flex: 1;
    min-height: 0;
    width: 100%;
    max-width: 1200px;
    margin: 0 auto;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  /* ─── Logo ─── */
  .logo {
    position: fixed;
    top: 1.75rem;
    left: 1.5rem;
    z-index: 20;
    font-size: 2rem;
    line-height: 1;
    letter-spacing: 0.05em;
    color: var(--c2);
    white-space: nowrap;
    text-shadow: 2px 2px 0 color-mix(in srgb, var(--accent) 30%, transparent);
  }
  .logo-accent { color: var(--accent); }

  .toprow {
    display: flex;
    align-items: center;
    gap: 1rem;
    flex-shrink: 0;
  }
  .guide-open {
    position: fixed;
    top: 1.75rem;
    right: 1.5rem;
    z-index: 25;
    flex-shrink: 0;
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    letter-spacing: 0.08em;
    padding: 0.55rem 0.9rem;
    background: transparent;
    border: 1px solid color-mix(in srgb, var(--accent) 45%, transparent);
    color: var(--c2);
    cursor: pointer;
    opacity: 0;
    transform: translateY(-16px);
    transition: opacity 0.5s ease 0.15s, transform 0.5s cubic-bezier(0.16, 1, 0.3, 1) 0.15s,
      color 0.2s, background 0.2s, border-color 0.2s;
  }
  .page.entered .guide-open {
    opacity: 1;
    transform: translateY(0);
  }
  .guide-open:hover {
    color: var(--accent);
    border-color: var(--accent);
    background: color-mix(in srgb, var(--accent) 12%, transparent);
  }
  .page.entered .guide-open:hover {
    transform: scale(1.05);
  }
  /* keep the debug strip clear of the fixed top-right guide button */
  @media (max-width: 1500px) {
    .toprow { padding-right: 11rem; }
  }

  /* ─── Debug grid ─── */
  .debug-panel {
    display: flex;
    flex: 1;
    min-width: 0;
    border: 1px solid color-mix(in srgb, var(--accent) 20%, transparent);
    background: color-mix(in srgb, var(--c1) 85%, transparent);
    clip-path: polygon(
      0 0,
      calc(100% - 8px) 0,
      100% 8px,
      100% 100%,
      8px 100%,
      0 calc(100% - 8px)
    );
    padding: 0.45rem 0.75rem;
    flex-shrink: 0;
  }
  .dg-col {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.15rem;
    padding: 0 0.5rem;
    border-left: 1px solid rgba(255, 255, 255, 0.08);
  }
  .dg-col:first-child { border-left: none; }
  .dg-label {
    font-size: 16px;
    letter-spacing: 0.12em;
    color: var(--c2);
  }
  .dg-value {
    font-size: 18px;
    font-weight: 700;
    color: var(--c2);
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* ─── Center / empty drop / overlay ─── */
  .center {
    flex: 1;
    min-height: 0;
    position: relative;
  }

  /* Both states overlay the exact same box, so transitions overlap instead of pushing */
  .state-layer {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .file-input-hidden { display: none; }

  .drop-empty,
  .overlay-box {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.4rem;
    text-align: center;
    border: 2px dashed color-mix(in srgb, var(--accent) 45%, transparent);
    background: color-mix(in srgb, var(--c1) 55%, transparent);
    clip-path: polygon(
      0 0,
      calc(100% - 10px) 0,
      100% 10px,
      100% 100%,
      10px 100%,
      0 calc(100% - 10px)
    );
    cursor: pointer;
    transition: border-color 0.15s, box-shadow 0.15s, background 0.15s;
  }
  .drop-empty {
    width: min(420px, 70vw);
    height: 260px;
  }
  .drop-empty.drag-over,
  .overlay.drag-over .overlay-box {
    border-color: var(--accent);
    background: color-mix(in srgb, var(--accent) 12%, var(--c1) 60%);
    box-shadow: inset 0 0 40px color-mix(in srgb, var(--accent) 22%, transparent);
  }
  .drop-icon {
    font-size: 40px;
    line-height: 1;
    color: var(--accent);
  }
  .drop-hint {
    font-size: 16px;
    color: var(--c2);
  }
  .drop-pct {
    font-size: 16px;
    color: var(--accent);
    letter-spacing: 0.1em;
  }
  .drop-sub {
    font-size: 16px;
    color: rgba(255, 255, 255, 0.55);
  }
  .drop-error {
    font-size: 16px;
    color: #ff6666;
    text-align: center;
    padding: 0 1rem;
  }

  /* ─── "+ ADD" overlay ─── */
  .overlay {
    position: absolute;
    inset: 0;
    z-index: 30;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.7);
    backdrop-filter: blur(2px);
    animation: fadeIn 0.25s ease both;
  }
  .overlay-box {
    position: relative;
    width: min(560px, 82vw);
    height: 340px;
    z-index: 2;
    padding: 1rem 1.5rem;
  }
  .close-btn {
    position: absolute;
    top: 0.7rem;
    right: 0.9rem;
    width: 46px;
    height: 46px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: transparent;
    border: 1px solid rgba(255, 255, 255, 0.2);
    color: var(--c2);
    font-size: 30px;
    line-height: 1;
    cursor: pointer;
    clip-path: polygon(
      0 0,
      calc(100% - 8px) 0,
      100% 8px,
      100% 100%,
      8px 100%,
      0 calc(100% - 8px)
    );
    transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1), color 0.2s, border-color 0.2s, background 0.2s;
  }
  .close-btn:hover {
    color: var(--accent);
    border-color: var(--accent);
    background: color-mix(in srgb, var(--accent) 14%, transparent);
    transform: rotate(90deg) scale(1.15);
  }
  .close-btn:active {
    transform: rotate(90deg) scale(0.82);
  }

  /* ─── Split view: list + preview ─── */
  .split-view {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 2rem;
    width: 100%;
    height: 100%;
  }
  .gif-list-pane {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 0.6rem;
    flex: 0 0 340px;
    min-height: 0;
    height: 100%;
  }
  .gif-preview-pane {
    flex: 0 0 auto;
    min-height: 0;
  }
  .preview-video {
    width: 100%;
    height: auto;
    object-fit: contain;
    display: block;
  }

  .add-btn {
    width: calc(100% - 1.4rem);
    margin: 0 0.7rem;
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    letter-spacing: 0.08em;
    padding: 0.5rem 1rem;
    background: transparent;
    border: 1px solid color-mix(in srgb, var(--accent) 45%, transparent);
    color: var(--c2);
    cursor: pointer;
    transition: all 0.2s;
  }
  .add-btn:hover {
    background: color-mix(in srgb, var(--accent) 15%, transparent);
    border-color: var(--accent);
    color: var(--accent);
  }

  .gif-list {
    display: flex;
    flex-direction: column;
    gap: 0.45rem;
    flex: none;
    height: 380px;
    box-sizing: border-box;
    overflow-y: auto;
    overflow-x: hidden;
    /* room so a hover-scaled row is not clipped by the scroll container */
    padding: 0.7rem;
  }
  .gif-list::-webkit-scrollbar {
    width: 10px;
  }
  .gif-list::-webkit-scrollbar-track {
    background: color-mix(in srgb, var(--c1) 60%, transparent);
  }
  .gif-list::-webkit-scrollbar-thumb {
    background: color-mix(in srgb, var(--accent) 40%, transparent);
    border-radius: 0;
  }
  .gif-list::-webkit-scrollbar-thumb:hover {
    background: var(--accent);
  }
  .gif-list {
    scrollbar-width: thin;
    scrollbar-color: color-mix(in srgb, var(--accent) 40%, transparent) transparent;
  }
  .gif-item {
    position: relative;
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.35rem 0.5rem;
    border: 1px solid rgba(255, 255, 255, 0.1);
    background: color-mix(in srgb, var(--c1) 70%, transparent);
    cursor: pointer;
    transition: border-color 0.15s, background 0.15s, transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
  }
  .gif-item:hover {
    border-color: color-mix(in srgb, var(--accent) 50%, transparent);
    background: color-mix(in srgb, var(--accent) 7%, transparent);
    transform: scale(1.04);
    z-index: 3;
  }
  .gif-item.active {
    border-color: var(--accent);
    background: color-mix(in srgb, var(--accent) 12%, transparent);
  }
  .thumb {
    width: 68px;
    height: 52px;
    flex-shrink: 0;
    overflow: hidden;
    background: #000;
    border: 1px solid rgba(255, 255, 255, 0.12);
  }
  .gif-name {
    flex: 1;
    min-width: 0;
    font-size: 16px;
    color: var(--c2);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  /* Empty shelf slot: static placeholder, no hover behaviour */
  .gif-slot-empty {
    height: 64px;
    box-sizing: border-box;
    border: 1px dashed rgba(255, 255, 255, 0.14);
    background: color-mix(in srgb, var(--c1) 40%, transparent);
    display: flex;
    align-items: center;
    justify-content: center;
  }
  /* "copied" popup floats OVER the slot row (never expands the row) */
  .copied-pop {
    position: absolute;
    inset: 0;
    z-index: 6;
    display: flex;
    align-items: center;
    justify-content: center;
    background: color-mix(in srgb, var(--c1) 88%, transparent);
    border: 1px solid var(--accent);
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    letter-spacing: 0.1em;
    color: var(--accent);
    pointer-events: none;
    text-align: center;
  }
  /* Preview column: name over the gif, copy button under it */
  .preview-col {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.9rem;
  }
  .preview-glow {
    line-height: 0;
    will-change: box-shadow, filter;
  }
  .preview-name {
    font-size: 32px;
    letter-spacing: 0.08em;
    color: var(--c2);
    flex-shrink: 0;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    transition: color 0.06s, transform 0.06s;
  }
  /* beat glow: modest so it reads as a flash, not a permanent state */
  :global(.preview-name.glow-beat) {
    color: color-mix(in srgb, var(--accent) 70%, var(--c2));
    transform: scale(1.04);
    text-shadow:
      0 0 4px var(--accent),
      0 0 12px color-mix(in srgb, var(--accent) 80%, transparent);
  }
  .obs-btn {
    position: relative;
    overflow: hidden;
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    letter-spacing: 0.08em;
    padding: 0.5rem 1.4rem;
    background: transparent;
    border: 1px solid color-mix(in srgb, var(--accent) 45%, transparent);
    color: var(--c2);
    cursor: pointer;
    transition: transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1), border-color 0.2s, background 0.2s;
  }
  .obs-btn:hover {
    border-color: var(--accent);
    background: color-mix(in srgb, var(--accent) 15%, transparent);
    transform: scale(1.06);
  }
  .obs-btn:active {
    transform: scale(0.94);
  }
  .obs-label {
    transition: opacity 0.2s;
  }
  .obs-btn.copied {
    border-color: var(--accent);
    background: color-mix(in srgb, var(--accent) 10%, transparent);
  }
  .obs-btn.copied .obs-label { opacity: 0.25; }
  .obs-copied {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--accent);
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    letter-spacing: 0.08em;
    background: color-mix(in srgb, var(--c1) 55%, transparent);
    pointer-events: none;
  }
  .item-btn {
    background: none;
    border: none;
    color: rgba(255, 255, 255, 0.55);
    cursor: pointer;
    line-height: 1;
    padding: 0.2rem;
    flex-shrink: 0;
    transition: transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1), color 0.15s;
  }
  .copy-btn {
    font-size: 20px;
  }
  .copy-btn:hover {
    color: var(--accent);
    transform: scale(1.35) rotate(12deg);
  }
  .copy-btn:active {
    transform: scale(0.85);
  }
  .del-btn {
    font-size: 28px;
    color: rgba(255, 255, 255, 0.5);
  }
  .del-btn:hover {
    color: #ff5566;
    transform: scale(1.3) rotate(90deg);
    text-shadow: 0 0 8px rgba(255, 85, 102, 0.6);
  }
  .del-btn:active {
    transform: scale(0.8) rotate(90deg);
  }

  /* ─── Split animation (first add) ─── */
  .split-view.list-in .gif-list-pane { animation: listIn 1s cubic-bezier(0.16, 1, 0.3, 1) both; }
  .split-view.preview-in .gif-preview-pane { animation: previewIn 1s cubic-bezier(0.16, 1, 0.3, 1) both; }
  @keyframes listIn {
    from { transform: translateX(120%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
  }
  @keyframes previewIn {
    from { transform: translateX(-28%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
  }
  @keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
  }

  /* ─── Bottom info + beat bar ─── */
  .bottom-block {
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
  }
  .info-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 3rem;
  }
  .info-cell {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.15rem;
  }
  .info-label {
    font-size: 16px;
    letter-spacing: 0.14em;
    color: var(--c2);
  }
  .info-value {
    font-size: 26px;
    font-weight: 700;
    line-height: 1;
    color: var(--accent);
    min-width: 76px;
    text-align: center;
  }
  .stepper {
    display: flex;
    align-items: center;
    gap: 0.55rem;
  }
  .cruise-btn {
    font-family: '8bitwonder', monospace;
    font-size: 24px;
    padding: 0.25rem 0.55rem;
    background: transparent;
    border: 1px solid rgba(255, 255, 255, 0.25);
    color: var(--c2);
    cursor: pointer;
    transition: all 0.2s;
  }
  .cruise-btn:hover {
    color: var(--accent);
    border-color: var(--accent);
  }
  .cruise-value {
    font-size: 24px;
    line-height: 1;
    color: var(--accent);
    text-align: center;
    min-width: 84px;
  }

  .beat-bar-container {
    border: 1px solid color-mix(in srgb, var(--accent) 20%, transparent);
    background: color-mix(in srgb, var(--c1) 60%, transparent);
    overflow: hidden;
  }
  .beat-canvas { width: 100%; display: block; }

  /* ─── Entrance animations ─── */
  .anim-title {
    opacity: 0;
    transform: translateY(-20px);
    transition: opacity 0.6s ease, transform 0.6s ease;
  }
  .page.entered .anim-title { opacity: 1; transform: none; }
  .anim-left {
    opacity: 0;
    transform: translateX(-40px);
    transition: opacity 0.6s ease 0.1s, transform 0.6s ease 0.1s;
  }
  .page.entered .anim-left { opacity: 1; transform: none; }
  .anim-right {
    opacity: 0;
    transform: translateX(40px);
    transition: opacity 0.6s ease 0.15s, transform 0.6s ease 0.15s;
  }
  .page.entered .anim-right { opacity: 1; transform: none; }
</style>
