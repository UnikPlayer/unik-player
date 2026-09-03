<script>
  import { onMount, onDestroy } from 'svelte';
  import { page } from '$app/state';
  import { trackBpm } from '$lib/stores/stores.js';

  const gifName = page.url.searchParams.get('gif');
  const transparent = page.url.searchParams.get('bg') === 'transparent';

  // Basic speed is saved per gif on the main dancesync page.
  // Fall back to the legacy global key, then to 1.0.
  function readBasicSpeed(gifName) {
    const clamp = (v) => Math.min(2.0, Math.max(0.5, v));
    try {
      const m = JSON.parse(localStorage.getItem('dancesync_basicSpeeds') || '{}');
      const key = gifName ? gifName.replace(/\.[^.]+$/, '') : null;
      if (key && typeof m[key] === 'number') return clamp(m[key]);
    } catch {}
    try {
      const raw = localStorage.getItem('dancesync_basicSpeed');
      if (raw) return clamp(parseFloat(raw) || 1.0);
    } catch {}
    return 1.0;
  }

  let basicSpeed = readBasicSpeed(gifName);

  const baseBpm = 120;
  let videoEl = null;
  let detectedBpm = null;
  let unsub = null;

  const src = gifName
    ? `/api/dance-gifs/${encodeURIComponent(gifName)}`
    : null;

  function apply() {
    if (!videoEl) return;
    if (detectedBpm) {
      const rate = Math.min(4.0, Math.max(0.25, (basicSpeed * detectedBpm) / baseBpm));
      videoEl.playbackRate = +rate.toFixed(3);
    } else {
      videoEl.playbackRate = basicSpeed;
    }
  }

  unsub = trackBpm.subscribe(v => {
    detectedBpm = v;
    apply();
  });

  onMount(() => {
    document.body.style.background = transparent ? 'transparent' : '#000000';
    document.body.style.margin = '0';
    document.body.style.overflow = 'hidden';
  });

  onDestroy(() => {
    if (unsub) unsub();
  });
</script>

<svelte:head>
  <title>DANCE GIF</title>
</svelte:head>

<div class="page">
  {#if src}
    <video
      bind:this={videoEl}
      src={src}
      loop
      muted
      autoplay
      playsinline
      class="gif-video"
      on:loadedmetadata={apply}
    ></video>
  {:else}
    <div class="missing">no gif specified</div>
  {/if}
</div>

<style>
  :global(header) { display: none !important; }
  :global(html), :global(body) { height: 100%; }

  .page {
    height: 100vh;
    width: 100vw;
    display: flex;
    align-items: center;
    justify-content: center;
  }
  .gif-video {
    width: 100%;
    height: 100%;
    object-fit: contain;
    display: block;
  }
  .missing {
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    color: #fff;
    opacity: 0.6;
  }
</style>
