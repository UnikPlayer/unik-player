<script>
  import { FFmpeg } from '@ffmpeg/ffmpeg';
  import { fetchFile } from '@ffmpeg/util';
  // AVIF disabled - @jsquash/avif has init export issues
  // import { decode, init as avifInit } from '@jsquash/avif';

  let ffmpeg = null;
  let ffmpegLoaded = false;
  let loading = false;
  let converting = false;
  let statusMsg = 'DROP GIF / AVIF FILE';
  let fileName = '';
  let dragOver = false;

  const ALLOWED = ['.gif', '.avif'];

  async function ensureFFmpeg() {
    if (ffmpegLoaded) return ffmpeg;
    loading = true;
    statusMsg = 'LOADING FFMPEG...';
    try {
      ffmpeg = new FFmpeg();
      await ffmpeg.load({
        coreURL: '/ffmpeg-core.js',
        wasmURL: '/ffmpeg-core.wasm'
      });
      ffmpegLoaded = true;
      statusMsg = 'READY';
    } catch (err) {
      statusMsg = 'FFMPEG LOAD FAILED';
      console.error(err);
      ffmpeg = null;
    } finally {
      loading = false;
    }
    return ffmpeg;
  }

  async function decodeAVIF(_file) {
    throw new Error('AVIF decoder not available (libavif WASM init issue). Use GIF instead.');
  }

  async function convertAndDownload(file) {
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'));
    if (!ALLOWED.includes(ext)) {
      statusMsg = 'INVALID FORMAT (use .gif or .avif)';
      return;
    }

    fileName = file.name;
    converting = true;
    statusMsg = 'CONVERTING ' + fileName + '...';

    try {
      const ff = await ensureFFmpeg();
      if (!ff) return;

      const outputName = file.name.replace(/\.[^.]+$/, '.mov');

      if (ext === '.gif') {
        const inputName = 'input.gif';
        await ff.writeFile(inputName, await fetchFile(file));
        await ff.exec([
          '-i', inputName,
          '-vf', 'scale=trunc(iw/2)*2:trunc(ih/2)*2',
          '-c:v', 'png',
          '-pix_fmt', 'rgba',
          '-an',
          outputName
        ]);
        await ff.deleteFile(inputName);
      } else if (ext === '.avif') {
        statusMsg = 'DECODING AVIF...';
        const { data, width, height } = await decodeAVIF(file);
        const w2 = width % 2 === 0 ? width : width + 1;
        const h2 = height % 2 === 0 ? height : height + 1;
        statusMsg = 'ENCODING VIDEO...';
        await ff.writeFile('raw.rgba', data);
        await ff.exec([
          '-f', 'rawvideo',
          '-pix_fmt', 'rgba',
          '-s', w2 + 'x' + h2,
          '-r', '30',
          '-i', 'raw.rgba',
          '-c:v', 'png',
          '-pix_fmt', 'rgba',
          '-an',
          outputName
        ]);
        await ff.deleteFile('raw.rgba');
      }

      const result = await ff.readFile(outputName);
      await ff.deleteFile(outputName);

      const blob = new Blob([result.buffer], { type: 'video/quicktime' });
      const url = URL.createObjectURL(blob);

      const a = document.createElement('a');
      a.href = url;
      a.download = outputName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      statusMsg = 'DONE ' + outputName;
    } catch (err) {
      statusMsg = 'CONVERSION FAILED';
      console.error('[DanceSync2]', err);
    } finally {
      converting = false;
    }
  }

  function handleFile(file) {
    if (!file) return;
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'));
    if (!ALLOWED.includes(ext)) {
      statusMsg = 'INVALID FORMAT (use .gif or .avif)';
      return;
    }
    convertAndDownload(file);
  }

  function onFileSelect(e) {
    handleFile(e.target.files?.[0]);
    if (e.target) e.target.value = '';
  }

  function onDrop(e) {
    e.preventDefault();
    dragOver = false;
    handleFile(e.dataTransfer.files?.[0]);
  }

  function onDragOver(e) {
    e.preventDefault();
    dragOver = true;
  }

  function onDragLeave() {
    dragOver = false;
  }
</script>

<div class="page">
  <a href="/" class="back-link">[&lt; BACK]</a>
  <h1 class="title">DANCESYNC2</h1>
  <p class="subtitle">GIF / AVIF &rarr; MOV CONVERTER</p>

  <div class="converter-card">
    <div
      class="drop-zone"
      class:drag-over={dragOver}
      class:converting
      class:done={statusMsg.startsWith('DONE')}
      on:dragover|preventDefault={onDragOver}
      on:dragleave={onDragLeave}
      on:drop={onDrop}
      role="button"
      tabindex="0"
      on:click={() => {
        if (!converting) document.getElementById('file-input').click();
      }}
      on:keydown={e => e.key === 'Enter' && !converting && document.getElementById('file-input').click()}
    >
      <input
        id="file-input"
        type="file"
        accept=".gif,.avif"
        on:change={onFileSelect}
        class="file-input-hidden"
        disabled={converting}
      />
{#if !loading && !converting}
        <div class="drop-icon">+</div>
        <div class="drop-text">{statusMsg}</div>
      {:else if loading}
        <div class="spinner"></div>
        <div class="drop-text">{statusMsg}</div>
      {:else if converting}
        <div class="spinner"></div>
        <div class="drop-text">{statusMsg}</div>
      {/if}
    </div>

    <div class="status-bar">
      <span class="status-label">STATUS:</span>
      <span class="status-value" class:error={statusMsg.includes('FAILED') || statusMsg.includes('INVALID')}>
        {statusMsg}
      </span>
    </div>
  </div>
</div>

<style>
  :global(header) { display: none !important; }

  .page {
    min-height: 100vh;
    background: var(--c1);
    color: var(--c2);
    font-family: '8bitwonder', monospace;
    padding: 1.5rem 2rem;
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
    clip-path: polygon(
      0 0,
      calc(100% - 12px) 0,
      100% 12px,
      100% 100%,
      12px 100%,
      0 calc(100% - 12px)
    );
  }

  .back-link {
    font-family: '8bitwonder', monospace;
    font-size: 0.7rem;
    color: color-mix(in srgb, var(--ca) 70%, transparent);
    text-decoration: none;
    letter-spacing: 0.08em;
    transition: color 0.2s;
    align-self: flex-start;
  }
  .back-link:hover { color: var(--ca); }

  .title {
    font-family: '8bitwonder', monospace;
    font-size: 2rem;
    letter-spacing: 0.1em;
    margin: 0;
    color: var(--c2);
    text-shadow: 2px 2px 0 color-mix(in srgb, var(--ca) 30%, transparent);
  }

  .subtitle {
    font-size: 0.7rem;
    letter-spacing: 0.12em;
    color: color-mix(in srgb, var(--c2) 50%, transparent);
    margin: -1rem 0 0 0;
  }

  .converter-card {
    max-width: 640px;
    border: 3px solid color-mix(in srgb, var(--ca) 40%, var(--c2) 20%);
    background: color-mix(in srgb, var(--c1) 90%, var(--ca) 10%);
    clip-path: polygon(
      0 0,
      calc(100% - 8px) 0,
      100% 8px,
      100% 100%,
      8px 100%,
      0 calc(100% - 8px)
    );
    padding: 1.5rem;
  }

  .drop-zone {
    width: 100%;
    aspect-ratio: 16 / 9;
    background: #000;
    border: 2px solid color-mix(in srgb, var(--ca) 25%, var(--c2) 10%);
    clip-path: polygon(
      0 0,
      calc(100% - 6px) 0,
      100% 6px,
      100% 100%,
      6px 100%,
      0 calc(100% - 6px)
    );
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.6rem;
    cursor: pointer;
    transition: border-color 0.15s, box-shadow 0.15s;
    user-select: none;
    position: relative;
  }

  .file-input-hidden { display: none; }

  .drop-zone.drag-over {
    border-color: var(--ca);
    box-shadow: inset 0 0 30px color-mix(in srgb, var(--ca) 25%, transparent);
  }

  .drop-zone.converting {
    cursor: not-allowed;
    border-color: var(--ca);
    box-shadow: inset 0 0 20px color-mix(in srgb, var(--ca) 15%, transparent);
  }

  .drop-zone.done {
    border-color: color-mix(in srgb, #0f0 50%, transparent);
    box-shadow: inset 0 0 20px color-mix(in srgb, #0f0 10%, transparent);
  }

  .drop-icon {
    font-size: 3rem;
    opacity: 0.3;
    color: var(--c2);
    line-height: 1;
  }

  .drop-text {
    font-size: 0.65rem;
    letter-spacing: 0.12em;
    opacity: 0.4;
    text-align: center;
    padding: 0 1rem;
  }

  .spinner {
    width: 32px;
    height: 32px;
    border: 3px solid color-mix(in srgb, var(--ca) 20%, transparent);
    border-top: 3px solid var(--ca);
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
  }

  @keyframes spin {
    to { transform: rotate(360deg); }
  }

  .status-bar {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    margin-top: 0.8rem;
    padding-top: 0.8rem;
    border-top: 2px solid color-mix(in srgb, var(--ca) 20%, transparent);
  }

  .status-label {
    font-size: 0.6rem;
    letter-spacing: 0.08em;
    color: color-mix(in srgb, var(--c2) 50%, transparent);
    flex-shrink: 0;
  }

  .status-value {
    font-size: 0.6rem;
    letter-spacing: 0.06em;
    color: color-mix(in srgb, var(--ca) 80%, transparent);
    word-break: break-all;
  }

  .status-value.error {
    color: #f44;
  }
</style>
