<script>
  import { onMount } from 'svelte';
  import { editorOpen, editingPlayer, ShowNotification, notificationText, playerStyles } from '$lib/stores/stores.js';
  import { copyPlayerStyle } from '$lib/playerButtons.js';

  export let component;
  export let name;

  let containerEl;
  let playerEl;

  // Color manipulation functions for static color mode
  function lightenColor(hex, percent) {
    const num = parseInt(hex.slice(1), 16);
    const r = Math.min(255, (num >> 16) + Math.round(255 * percent / 100));
    const g = Math.min(255, ((num >> 8) & 0x00FF) + Math.round(255 * percent / 100));
    const b = Math.min(255, (num & 0x0000FF) + Math.round(255 * percent / 100));
    return `#${(1 << 24 | r << 16 | g << 8 | b).toString(16).slice(1)}`;
  }

  function darkenColor(hex, percent) {
    const num = parseInt(hex.slice(1), 16);
    const r = Math.max(0, (num >> 16) - Math.round(255 * percent / 100));
    const g = Math.max(0, ((num >> 8) & 0x00FF) - Math.round(255 * percent / 100));
    const b = Math.max(0, (num & 0x0000FF) - Math.round(255 * percent / 100));
    return `#${(1 << 24 | r << 16 | g << 8 | b).toString(16).slice(1)}`;
  }

  function desaturateColor(hex, percent) {
    const num = parseInt(hex.slice(1), 16);
    const r = (num >> 16);
    const g = ((num >> 8) & 0x00FF);
    const b = (num & 0x0000FF);
    const gray = (r + g + b) / 3;
    const nr = Math.round(r + (gray - r) * percent / 100);
    const ng = Math.round(g + (gray - g) * percent / 100);
    const nb = Math.round(b + (gray - b) * percent / 100);
    return `#${(1 << 24 | nr << 16 | ng << 8 | nb).toString(16).slice(1)}`;
  }

  function generateColorVars(hex) {
    const base = hex || '#B87333';
    return {
      vibrant: base,
      lightVibrant: lightenColor(base, 30),
      darkVibrant: darkenColor(base, 30),
      muted: desaturateColor(base, 30),
      lightMuted: lightenColor(base, 20),
      darkMuted: darkenColor(base, 40)
    };
  }

  // Get saved style for this player
  $: savedStyle = $playerStyles[name] || {};
  $: useStaticColor = savedStyle.colorMode === 'static';
  $: staticColorValue = savedStyle.staticColor || '#B87333';
  $: colors = useStaticColor ? generateColorVars(staticColorValue) : null;
  $: fontFamily = savedStyle.font || 'Rubik';
  $: previewScale = savedStyle.previewScale || 0.5; // Default 50%

  $: previewStyle = useStaticColor && colors ? `
    --vibrant: ${colors.vibrant};
    --lightVibrant: ${colors.lightVibrant};
    --darkVibrant: ${colors.darkVibrant};
    --muted: ${colors.muted};
    --lightMuted: ${colors.lightMuted};
    --darkMuted: ${colors.darkMuted};
    font-family: "${fontFamily}", sans-serif;
    transform: scale(${previewScale});
  ` : `font-family: "${fontFamily}", sans-serif; transform: scale(${previewScale});`;

  function openEditor() {
    editingPlayer.set(name);
    editorOpen.set(true);
  }

  async function selectPlayer() {
    await copyPlayerStyle(name);
    notificationText.set('COPIED_TO_BUFFER');
    ShowNotification.set(true);
    setTimeout(() => ShowNotification.set(false), 2500);
  }

  onMount(async () => {
    await tick();
    // Wait for fonts to load
    if (document.fonts && document.fonts.ready) {
      await document.fonts.ready;
    }
  });
</script>

<div class="player-card">
  <div class="card-preview" bind:this={containerEl}>
    <div class="preview-container" bind:this={playerEl} style={previewStyle}>
      <svelte:component this={component} preview={false} showAlways={true} />
    </div>
    <span class="player-name">{name.replace(/([A-Z])/g, '_$1').toUpperCase()}</span>
  </div>

  <div class="card-actions">
    <button class="btn btn-select" on:click={selectPlayer}>
      SELECT
    </button>
    <button class="btn btn-edit" on:click={openEditor}>
      EDIT
    </button>
  </div>
</div>

<style lang="scss">
  .player-card {
    position: relative;
    background: linear-gradient(135deg, rgba(20, 20, 25, 0.9), rgba(30, 30, 40, 0.8));
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 8px;
    overflow: hidden;
    transition: all 0.3s ease;

    &::before {
      content: '';
      position: absolute;
      inset: 0;
      background: linear-gradient(
        135deg,
        rgba(184, 115, 51, 0.05) 0%,
        transparent 50%,
        rgba(99, 102, 241, 0.05) 100%
      );
      pointer-events: none;
    }

    &:hover {
      border-color: rgba(184, 115, 51, 0.4);
      transform: translateY(-2px);
      box-shadow: 0 8px 32px rgba(184, 115, 51, 0.15);
    }
  }

  .card-preview {
    position: relative;
    padding: 2rem 1.5rem;
    min-height: 180px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background:
      linear-gradient(180deg, transparent 0%, rgba(0, 0, 0, 0.3) 100%),
      repeating-linear-gradient(
        0deg,
        transparent,
        transparent 2px,
        rgba(255, 255, 255, 0.02) 2px,
        rgba(255, 255, 255, 0.02) 4px
      );
  }

  .preview-container {
    transform-origin: center;
    display: flex;
    align-items: center;
    justify-content: center;
    /* Colors inherited from :root (set by Vibrant.js) */
  }

  .player-name {
    position: absolute;
    bottom: 0.75rem;
    left: 1rem;
    font-family: 'JetBrains Mono', 'Courier New', monospace;
    font-size: 0.75rem;
    font-weight: 600;
    color: rgba(255, 255, 255, 0.6);
    letter-spacing: 0.05em;
  }

  .card-actions {
    display: flex;
    border-top: 1px solid rgba(255, 255, 255, 0.1);
  }

  .btn {
    flex: 1;
    padding: 1rem;
    font-family: 'JetBrains Mono', 'Courier New', monospace;
    font-size: 0.8rem;
    font-weight: 600;
    letter-spacing: 0.1em;
    border: none;
    cursor: pointer;
    transition: all 0.2s ease;
    text-transform: uppercase;
  }

  .btn-select {
    background: rgba(184, 115, 51, 0.2);
    color: #B87333;
    border-right: 1px solid rgba(255, 255, 255, 0.1);

    &:hover {
      background: rgba(184, 115, 51, 0.4);
      color: #D4944A;
    }
  }

  .btn-edit {
    background: rgba(255, 255, 255, 0.05);
    color: rgba(255, 255, 255, 0.7);

    &:hover {
      background: rgba(255, 255, 255, 0.1);
      color: white;
    }
  }
</style>
