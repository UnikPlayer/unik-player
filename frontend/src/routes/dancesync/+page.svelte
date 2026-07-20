<script>
  import { onMount, onDestroy } from 'svelte';

  // ─── Page transitions ─────────────────────────────
  let entered = false;

  // ─── State ───────────────────────────────────────────────
  let targetBPM = 100;
  let playbackRate = 1.0;
  let videoEl = null;

  let beatCanvas = null;
  let fileInput = null;

  const BEAT_BAR_HEIGHT = 40;
  const TICK_SPACING_PX = 60;

  // ─── Multi-layer system ─────────────────────────────────
  let layers = [];       // Array of { id, name, src, playbackRate }
  let activeLayer = 0;   // index into layers

  // ─── Drag & drop ────────────────────────────────────────
  let dragOver = false;

  const PRESETS_KEY = 'dancesync_presets';
  const METRO_KEY = 'dancesync_metro';
  const MIN_BPM = 60;
  const MAX_BPM = 300;

  // ─── FFmpeg backend status & download ──────────────────────
  let ffmpegStatus = 'checking'; // 'missing' | 'downloading' | 'ready'
  let showDownloadPrompt = false;
  let downloadProgress = '';
  let convertProgress = '';
  let isConverting = false;

  async function checkFfmpeg() {
    try {
      const res = await fetch('/api/ffmpeg/status');
      const data = await res.json();
      if (data.ready) {
        ffmpegStatus = 'ready';
      } else {
        ffmpegStatus = 'missing';
        showDownloadPrompt = true;
      }
    } catch {
      ffmpegStatus = 'missing';
      showDownloadPrompt = true;
    }
  }

  async function startDownload() {
    showDownloadPrompt = false;
    ffmpegStatus = 'downloading';
    downloadProgress = 'Downloading FFmpeg (~35MB)...';
    try {
      const res = await fetch('/api/ffmpeg/download', { method: 'POST' });
      if (res.ok) {
        downloadProgress = 'FFmpeg installed!';
        ffmpegStatus = 'ready';
      } else {
        const err = await res.text();
        downloadProgress = 'Failed: ' + err;
        ffmpegStatus = 'missing';
      }
    } catch (e) {
      downloadProgress = 'Error: ' + e.message;
      ffmpegStatus = 'missing';
    }
  }

  async function convertOnBackend(file) {
    isConverting = true;
    convertProgress = 'Converting...';
    try {
      const form = new FormData();
      form.append('file', file);
      const res = await fetch('/api/ffmpeg/convert', { method: 'POST', body: form });
      if (!res.ok) {
        const err = await res.text();
        convertProgress = 'Conversion failed: ' + err;
        isConverting = false;
        return null;
      }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      convertProgress = '';
      isConverting = false;
      return url;
    } catch (e) {
      convertProgress = 'Error: ' + e.message;
      isConverting = false;
      return null;
    }
  }

  // ─── Metronome ──────────────────────────────────────────
  let metroOn = false;
  let metroInterval = null;
  let audioCtx = null;

  function getAudioCtx() {
    if (!audioCtx) audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    return audioCtx;
  }

  function playMetroClick() {
    const ctx = getAudioCtx();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'square';
    osc.frequency.value = 800;
    gain.gain.setValueAtTime(0.3, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.005);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(ctx.currentTime);
    osc.stop(ctx.currentTime + 0.006);
  }

  function startMetro() {
    stopMetro();
    playMetroClick();
    const ms = (60 / targetBPM) * 1000;
    metroInterval = setInterval(playMetroClick, ms);
  }

  function stopMetro() {
    if (metroInterval) {
      clearInterval(metroInterval);
      metroInterval = null;
    }
  }

  function toggleMetro() {
    metroOn = !metroOn;
    if (metroOn) {
      startMetro();
    } else {
      stopMetro();
    }
    localStorage.setItem(METRO_KEY, JSON.stringify(metroOn));
  }

// ─── Presets persistence ──────────────────────────────────
  function loadPresets() {
    try {
      const raw = localStorage.getItem(PRESETS_KEY);
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  }

  function savePreset() {
    const layer = layers[activeLayer];
    if (!layer) return;
    const presets = loadPresets();
    presets[layer.name] = { playbackRate: layer.playbackRate, targetBPM };
    localStorage.setItem(PRESETS_KEY, JSON.stringify(presets));
  }

  function applyPreset(name) {
    const presets = loadPresets();
    const p = presets[name];
    if (p) {
      playbackRate = p.playbackRate;
      targetBPM = p.targetBPM;
    }
  }

// ─── Layer management ────────────────────────────────────
  async function addLayer(file) {
    const ext = file.name.toLowerCase();
    const needsConvert = ext.endsWith('.gif') || ext.endsWith('.avif');
    let src;
    if (needsConvert && ffmpegStatus === 'ready') {
      src = await convertOnBackend(file) || URL.createObjectURL(file);
    } else {
      src = URL.createObjectURL(file);
    }
    layers = [...layers, { id: Date.now(), name: file.name, src, playbackRate: 1.0 }];
    activeLayer = layers.length - 1;
    applyPreset(file.name);
    requestAnimationFrame(() => {
      if (videoEl) {
        videoEl.playbackRate = layers[activeLayer].playbackRate;
        playbackRate = layers[activeLayer].playbackRate;
        videoEl.play().catch(() => {});
      }
    });
  }

  function removeLayer(idx) {
    URL.revokeObjectURL(layers[idx].src);
    layers = layers.filter((_, i) => i !== idx);
    if (activeLayer >= layers.length) {
      activeLayer = Math.max(0, layers.length - 1);
    }
  }

  function selectLayer(idx) {
    activeLayer = idx;
    playbackRate = layers[idx].playbackRate;
    requestAnimationFrame(() => {
      if (videoEl) {
        videoEl.playbackRate = playbackRate;
        videoEl.play().catch(() => {});
      }
    });
  }

  // ─── File handling ────────────────────────────────────────
  function handleFileSelect(e) {
    const file = e.target.files[0];
    if (!file) return;
    addLayer(file);
    e.target.value = '';
  }

  // ─── Drag & drop handlers ─────────────────────────────────
  function handleDragOver(e) {
    e.preventDefault();
    dragOver = true;
  }

  function handleDragLeave() {
    dragOver = false;
  }

  function handleDrop(e) {
    e.preventDefault();
    dragOver = false;
    const file = e.dataTransfer.files[0];
    if (file) addLayer(file);
  }

  // ─── BPM adjustment ───────────────────────────────────────
  function adjustBPM(delta) {
    targetBPM = Math.max(MIN_BPM, Math.min(MAX_BPM, targetBPM + delta));
  }

  $: targetBPM, playbackRate, savePreset();

  // ─── Metronome restart when BPM changes ──────────────────
  $: if (metroOn && targetBPM) {
    startMetro();
  }

  // ─── Mount / destroy ─────────────────────────────────────
  onMount(() => {
    checkFfmpeg();

    requestAnimationFrame(() => {
      requestAnimationFrame(() => { entered = true; });
    });

    // Restore metronome state
    try {
      const saved = JSON.parse(localStorage.getItem(METRO_KEY));
      if (saved === true) {
        metroOn = true;
        startMetro();
      }
    } catch {}

    // ─── Beat bar canvas animation ──────────────────────────
    let rafId;
    const ro = new ResizeObserver(() => {});
    if (beatCanvas?.parentElement) ro.observe(beatCanvas.parentElement);

    function render() {
      const canvas = beatCanvas;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = BEAT_BAR_HEIGHT;
      const offset = (performance.now() / 1000 * targetBPM / 60 * TICK_SPACING_PX) % TICK_SPACING_PX;

      ctx.clearRect(0, 0, w, h);

      // Main ticks
      for (let x = -TICK_SPACING_PX + offset; x < w + TICK_SPACING_PX; x += TICK_SPACING_PX) {
        ctx.fillStyle = 'rgba(238, 34, 204, 0.9)';
        ctx.fillRect(Math.round(x) - 1, 0, 2, h);
      }

      // Center playhead
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(Math.round(w / 2), 0, 1, h);

      // Sub-ticks
      const subSpacing = TICK_SPACING_PX / 4;
      for (let x = -subSpacing + (offset % subSpacing); x < w + subSpacing; x += subSpacing) {
        const rx = Math.round(x);
        const dist = Math.abs((rx - offset + TICK_SPACING_PX * 100) % TICK_SPACING_PX - TICK_SPACING_PX);
        if (dist < 4 || Math.abs(((rx - offset + TICK_SPACING_PX * 100) % TICK_SPACING_PX)) < 4) continue;
        ctx.fillStyle = 'rgba(238, 34, 204, 0.2)';
        ctx.fillRect(rx, Math.round(h * 0.3), 1, Math.round(h * 0.4));
      }

      // Borders
      ctx.fillStyle = 'rgba(238, 34, 204, 0.4)';
      ctx.fillRect(0, 0, w, 1);
      ctx.fillRect(0, h - 1, w, 1);

      rafId = requestAnimationFrame(render);
    }

    rafId = requestAnimationFrame(render);
    return () => {
      cancelAnimationFrame(rafId);
      ro.disconnect();
    };
  });

  onDestroy(() => {
    stopMetro();
  });

  // ─── Video rate sync ──────────────────────────────────────
  $: if (videoEl) videoEl.playbackRate = layers[activeLayer]?.playbackRate ?? playbackRate;
</script>
<div class="page" class:entered>
  <!-- Back navigation -->
  <a href="/" class="back-link">[&lt; BACK]</a>

  <h1 class="title anim-title">DANCESYNC</h1>

  {#if showDownloadPrompt}
    <div class="download-prompt">
      <div class="prompt-text">This feature needs FFmpeg (~35MB) to convert GIF/AVIF to MP4.</div>
      <div class="prompt-text">Download and install now?</div>
      <div class="prompt-actions">
        <button class="prompt-btn prompt-yes" on:click={startDownload}>YES</button>
        <button class="prompt-btn prompt-no" on:click={() => showDownloadPrompt = false}>NO</button>
      </div>
    </div>
  {/if}

  {#if downloadProgress}
    <div class="download-progress">{downloadProgress}</div>
  {/if}

  <div class="layout">
    <!-- ═══ LEFT COLUMN: Video Calibration ═══ -->
    <div class="col-left anim-left">
      <div class="panel">
        <div class="panel-header">VIDEO CALIBRATION</div>

        <!-- Drop zone / video area -->
        <div
          class="video-wrap"
          class:drag-over={dragOver}
          on:dragover|preventDefault={handleDragOver}
          on:dragleave={handleDragLeave}
          on:drop={handleDrop}
          role="button"
          tabindex="0"
          on:click={() => fileInput.click()}
          on:keydown={e => e.key === 'Enter' && fileInput.click()}
        >
          <input bind:this={fileInput} type="file" accept=".gif,.avif,.mp4,.webm" on:change={handleFileSelect} class="file-input-hidden" />

          {#if layers.length > 0}
            {#each layers as layer, i (layer.id)}
              {#if i === activeLayer}
                <video
                  bind:this={videoEl}
                  src={layer.src}
                  loop muted autoplay playsinline controls={false}
                  class="video-player"
                ></video>
              {/if}
            {/each}
          {:else}
            <div class="video-placeholder">
              <div class="placeholder-icon">+</div>
              <div class="placeholder-text">DROP FILE OR CLICK</div>
            </div>
          {/if}
        </div>

        <!-- Layer chips -->
        {#if layers.length > 0}
          <div class="layers-row">
            {#each layers as layer, i (layer.id)}
              <button
                class="layer-chip"
                class:active={i === activeLayer}
                on:click={() => selectLayer(i)}
              >
                <span class="layer-name">{layer.name}</span>
                <span class="layer-speed">{layer.playbackRate.toFixed(2)}x</span>
                <span class="layer-remove" on:click|stopPropagation={() => removeLayer(i)} role="button" tabindex="0" on:keydown={e=>e.key==='Enter' && removeLayer(i)}>×</span>
              </button>
            {/each}
          </div>
        {/if}
        <!-- Controls: BPM left, Metronome center, Speed right -->
        <div class="controls-row">
          <div class="ctrl-group">
            <span class="ctrl-label">BPM:</span>
            <button class="ctrl-btn" on:click={() => adjustBPM(-10)}>-10</button>
            <button class="ctrl-btn" on:click={() => adjustBPM(-1)}>-1</button>
            <span class="ctrl-value">{targetBPM}</span>
            <button class="ctrl-btn" on:click={() => adjustBPM(1)}>+1</button>
            <button class="ctrl-btn" on:click={() => adjustBPM(10)}>+10</button>
          </div>

          <button class="ctrl-btn metro-btn" class:metro-active={metroOn} on:click={toggleMetro}>
            {metroOn ? '🔊' : '🔇'}
          </button>

          <div class="ctrl-group">
            <span class="ctrl-label">SPEED:</span>
            <button class="ctrl-btn ctrl-btn-narrow" on:click={() => {
              const newRate = Math.max(0.1, +(playbackRate - 0.1).toFixed(1));
              playbackRate = newRate;
              if (layers[activeLayer]) layers[activeLayer].playbackRate = newRate;
            }}>-</button>
            <span class="ctrl-value speed-display">{playbackRate.toFixed(1)}x</span>
            <button class="ctrl-btn ctrl-btn-narrow" on:click={() => {
              const newRate = Math.min(4.0, +(playbackRate + 0.1).toFixed(1));
              playbackRate = newRate;
              if (layers[activeLayer]) layers[activeLayer].playbackRate = newRate;
            }}>+</button>
          </div>
        </div>

        <!-- Beat bar -->
        <div class="beat-bar-container">
          <canvas bind:this={beatCanvas} class="beat-canvas" height={BEAT_BAR_HEIGHT}></canvas>
        </div>
      </div>
    </div>

    <!-- ═══ RIGHT COLUMN: Debug Panel ═══ -->
    <div class="col-right anim-right">
      <div class="panel">
        <div class="panel-header">DEBUG</div>

        <div class="debug-row">
          <span class="debug-key">BPM DETECTED:</span>
          <span class="debug-val">---</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">ENERGY:</span>
          <span class="debug-val">---</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">BEAT:</span>
          <span class="debug-val">---</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">SOURCE:</span>
          <span class="debug-val">---</span>
        </div>

        <div class="debug-separator"></div>

        <div class="debug-row">
          <span class="debug-key">TARGET BPM:</span>
          <span class="debug-val">{targetBPM}</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">PLAY RATE:</span>
          <span class="debug-val">{playbackRate.toFixed(2)}</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">FILE:</span>
          <span class="debug-val">{layers[activeLayer]?.name || '---'}</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">PRESET:</span>
          <span class="debug-val">{layers[activeLayer]?.name ? 'LOADED' : '---'}</span>
        </div>
        <div class="debug-row">
          <span class="debug-key">LAYERS:</span>
          <span class="debug-val">{layers.length}</span>
        </div>
      </div>
    </div>
  </div>
</div>
