<script>
  // Lightweight thumbnail: first frame only — no full animation decoding.
  export let name = '';
  export let ext = '';

  $: src = name ? `/api/dance-gifs/${encodeURIComponent(name)}` : null;
  $: isVideo = ext === 'mp4' || ext === 'webm';

  let videoEl = null;
  let frameUrl = null; // canvas snapshot for gif/avif first frame
  let canvas = null;

  function onVideoMeta() {
    // Seek near 0 so the first frame is decoded, then stay paused
    if (videoEl) videoEl.currentTime = 0.02;
  }
  function onSeeked() {
    if (videoEl && !videoEl.ended) videoEl.pause();
  }
  function onImgLoad(e) {
    const img = e.target;
    if (!canvas) canvas = document.createElement('canvas');
    const W = 96;
    const H = 96;
    canvas.width = W;
    canvas.height = H;
    const g = canvas.getContext('2d');
    const ratio = (img.naturalWidth || 1) / (img.naturalHeight || 1);
    let w = W;
    let h = H;
    if (ratio > 1) h = W / ratio;
    else w = H * ratio;
    g.drawImage(img, (W - w) / 2, (H - h) / 2, w, h);
    frameUrl = canvas.toDataURL('image/png');
    img.remove(); // stop any gif animation
  }
</script>

{#if isVideo}
  <video
    bind:this={videoEl}
    src={src}
    muted
    playsinline
    preload="metadata"
    on:loadedmetadata={onVideoMeta}
    on:seeked={onSeeked}
  ></video>
{:else if frameUrl}
  <img src={frameUrl} alt="" />
{:else}
  <img src={src} alt="" loading="eager" decoding="async" on:load={onImgLoad} />
{/if}

<style>
  video, img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    display: block;
    background: #000;
  }
</style>
