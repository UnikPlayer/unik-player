<script>
  import { onMount } from 'svelte';

  export let mode = 'dynamic';
  export let color = '#B87333';

  let showColorWheel = false;
  let hue = 30;
  let saturation = 70;
  let lightness = 46;
  let isDragging = false;
  let wheelEl;

  onMount(() => {
    if (color && color.startsWith('#')) {
      const rgb = hexToRgb(color);
      if (rgb) {
        const hsl = rgbToHsl(rgb.r, rgb.g, rgb.b);
        hue = hsl.h;
        saturation = hsl.s;
        lightness = hsl.l;
      }
    }
  });

  function updateColorFromHSL() {
    if (mode === 'static') {
      color = hslToHex(hue, saturation, lightness);
    }
  }

  function hexToRgb(hex) {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? {
      r: parseInt(result[1], 16),
      g: parseInt(result[2], 16),
      b: parseInt(result[3], 16)
    } : null;
  }

  function rgbToHsl(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    const max = Math.max(r, g, b), min = Math.min(r, g, b);
    let h = 0, s = 0, l = (max + min) / 2;

    if (max !== min) {
      const d = max - min;
      s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
      switch (max) {
        case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;
        case g: h = ((b - r) / d + 2) / 6; break;
        case b: h = ((r - g) / d + 4) / 6; break;
      }
    }
    return { h: Math.round(h * 360), s: Math.round(s * 100), l: Math.round(l * 100) };
  }

  function hslToHex(h, s, l) {
    s /= 100; l /= 100;
    const a = s * Math.min(l, 1 - l);
    const f = n => {
      const k = (n + h / 30) % 12;
      const clr = l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
      return Math.round(255 * clr).toString(16).padStart(2, '0');
    };
    return `#${f(0)}${f(8)}${f(4)}`;
  }

  function updateFromPosition(clientX, clientY) {
    if (!wheelEl) return;

    const rect = wheelEl.getBoundingClientRect();
    const centerX = rect.width / 2;
    const centerY = rect.height / 2;
    const x = clientX - rect.left - centerX;
    const y = clientY - rect.top - centerY;

    // CSS conic-gradient starts from top (90deg offset), going clockwise
    // atan2 gives angle from right (0), counter-clockwise positive
    // We need to convert: CSS angle = 90 - atan2_angle
    let angle = Math.atan2(y, x) * (180 / Math.PI);
    // Convert to CSS conic-gradient coordinate system (starts from top, clockwise)
    let cssAngle = (angle + 90 + 360) % 360;

    const maxRadius = rect.width / 2;
    const distance = Math.min(Math.sqrt(x * x + y * y) / maxRadius, 1);

    hue = Math.round(cssAngle);
    saturation = Math.round(distance * 100);
    updateColorFromHSL();
  }

  function handleWheelMouseDown(e) {
    isDragging = true;
    updateFromPosition(e.clientX, e.clientY);
  }

  function handleWheelMouseMove(e) {
    if (isDragging) {
      updateFromPosition(e.clientX, e.clientY);
    }
  }

  function handleWheelMouseUp() {
    isDragging = false;
  }

  function handleLightnessChange(e) {
    lightness = parseInt(e.target.value);
    updateColorFromHSL();
  }

  // Calculate indicator position from hue and saturation
  $: indicatorAngle = hue - 90; // Convert back from CSS angle
  $: indicatorDistance = saturation * 0.5; // 50% of radius = 60px max for 120px wheel
</script>

<svelte:window on:mousemove={handleWheelMouseMove} on:mouseup={handleWheelMouseUp} />

<div class="color-picker">
  <div class="mode-toggle">
    <button
      class="mode-btn"
      class:active={mode === 'dynamic'}
      on:click={() => { mode = 'dynamic'; showColorWheel = false; }}
    >
      DYNAMIC
    </button>
    <button
      class="mode-btn"
      class:active={mode === 'static'}
      on:click={() => { mode = 'static'; showColorWheel = true; }}
    >
      STATIC
    </button>
  </div>

  {#if mode === 'static'}
    <div class="color-controls">
      <button
        class="color-preview"
        style="background: {color}"
        on:click={() => showColorWheel = !showColorWheel}
      >
        <span class="color-hex">{color.toUpperCase()}</span>
      </button>

      {#if showColorWheel}
        <div class="color-wheel-container">
          <div
            class="color-wheel"
            bind:this={wheelEl}
            on:mousedown={handleWheelMouseDown}
            on:keydown={() => {}}
            role="slider"
            tabindex="0"
            aria-label="Color wheel"
            aria-valuenow={hue}
          >
            <div
              class="wheel-indicator"
              style="transform: rotate({indicatorAngle}deg) translateX({indicatorDistance}px);"
            ></div>
          </div>

          <div class="lightness-control">
            <span class="slider-label">L</span>
            <input
              type="range"
              min="10"
              max="90"
              value={lightness}
              on:input={handleLightnessChange}
              class="lightness-slider"
            />
            <span class="slider-value">{lightness}%</span>
          </div>
        </div>
      {/if}
    </div>
  {:else}
    <div class="dynamic-info">
      <span class="info-text">Colors sync from album art</span>
      <div class="palette-preview">
        <span class="palette-dot" style="background: var(--vibrant, #D4944A)" title="var(--vibrant)">
          <span class="tooltip">var(--vibrant)</span>
        </span>
        <span class="palette-dot" style="background: var(--lightVibrant, #F5DEB3)" title="var(--lightVibrant)">
          <span class="tooltip">var(--lightVibrant)</span>
        </span>
        <span class="palette-dot" style="background: var(--darkVibrant, #5C4033)" title="var(--darkVibrant)">
          <span class="tooltip">var(--darkVibrant)</span>
        </span>
        <span class="palette-dot" style="background: var(--muted, #8B6914)" title="var(--muted)">
          <span class="tooltip">var(--muted)</span>
        </span>
        <span class="palette-dot" style="background: var(--lightMuted, #B87333)" title="var(--lightMuted)">
          <span class="tooltip">var(--lightMuted)</span>
        </span>
        <span class="palette-dot" style="background: var(--darkMuted, #1a1510)" title="var(--darkMuted)">
          <span class="tooltip">var(--darkMuted)</span>
        </span>
      </div>
    </div>
  {/if}
</div>

<style lang="scss">
  .color-picker {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .mode-toggle {
    display: flex;
    gap: 0.5rem;
  }

  .mode-btn {
    flex: 1;
    padding: 0.6rem 1rem;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    font-weight: 600;
    letter-spacing: 0.1em;
    background: rgba(255, 255, 255, 0.05);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 2px;
    color: rgba(255, 255, 255, 0.5);
    cursor: pointer;
    transition: all 0.2s ease;

    &:hover {
      background: rgba(255, 255, 255, 0.1);
      color: white;
    }

    &.active {
      background: rgba(184, 115, 51, 0.2);
      border-color: #B87333;
      color: #B87333;
    }
  }

  .color-controls {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .color-preview {
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 0.75rem 1rem;
    border: 1px solid rgba(255, 255, 255, 0.2);
    border-radius: 4px;
    cursor: pointer;
    transition: all 0.2s ease;

    &:hover {
      border-color: rgba(255, 255, 255, 0.4);
    }
  }

  .color-hex {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.85rem;
    font-weight: 600;
    color: white;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
  }

  .color-wheel-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    padding: 1rem;
    background: rgba(0, 0, 0, 0.3);
    border-radius: 4px;
  }

  .color-wheel {
    width: 120px;
    height: 120px;
    border-radius: 50%;
    background: conic-gradient(
      from 0deg,
      hsl(0, 100%, 50%),
      hsl(60, 100%, 50%),
      hsl(120, 100%, 50%),
      hsl(180, 100%, 50%),
      hsl(240, 100%, 50%),
      hsl(300, 100%, 50%),
      hsl(360, 100%, 50%)
    );
    position: relative;
    cursor: crosshair;

    &::after {
      content: '';
      position: absolute;
      inset: 0;
      border-radius: 50%;
      background: radial-gradient(circle, white 0%, transparent 70%);
    }
  }

  .wheel-indicator {
    position: absolute;
    top: 50%;
    left: 50%;
    width: 12px;
    height: 12px;
    margin: -6px 0 0 -6px;
    border: 2px solid white;
    border-radius: 50%;
    box-shadow: 0 0 4px rgba(0, 0, 0, 0.5);
    pointer-events: none;
    z-index: 1;
    transform-origin: center center;
  }

  .lightness-control {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    width: 100%;
  }

  .slider-label,
  .slider-value {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    color: rgba(255, 255, 255, 0.5);
    min-width: 2rem;
  }

  .slider-value {
    text-align: right;
  }

  .lightness-slider {
    flex: 1;
    height: 4px;
    -webkit-appearance: none;
    appearance: none;
    background: linear-gradient(90deg, #000, #fff);
    border-radius: 2px;
    cursor: pointer;

    &::-webkit-slider-thumb {
      -webkit-appearance: none;
      width: 12px;
      height: 12px;
      background: white;
      border-radius: 50%;
      border: 2px solid #333;
      cursor: pointer;
    }
  }

  .dynamic-info {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0.75rem;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 4px;
  }

  .info-text {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    color: rgba(255, 255, 255, 0.4);
  }

  .palette-preview {
    display: flex;
    gap: 0.5rem;
  }

  .palette-dot {
    position: relative;
    width: 24px;
    height: 24px;
    border-radius: 4px;
    border: 1px solid rgba(255, 255, 255, 0.2);
    cursor: pointer;
    transition: transform 0.2s;

    &:hover {
      transform: scale(1.2);
      z-index: 10;

      .tooltip {
        opacity: 1;
        visibility: visible;
      }
    }
  }

  .tooltip {
    position: absolute;
    bottom: 100%;
    left: 50%;
    transform: translateX(-50%);
    padding: 0.3rem 0.5rem;
    background: rgba(0, 0, 0, 0.9);
    border: 1px solid rgba(255, 255, 255, 0.2);
    border-radius: 3px;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.6rem;
    color: #B87333;
    white-space: nowrap;
    opacity: 0;
    visibility: hidden;
    transition: all 0.2s;
    margin-bottom: 4px;
    pointer-events: none;
  }
</style>
