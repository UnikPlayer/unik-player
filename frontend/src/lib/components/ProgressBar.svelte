<script>
  import { trackProgress, trackPosition, trackDuration } from '$lib/stores/stores.js';

  // Props for customization
  export let showTime = true;
  export let height = '4px';
  export let borderRadius = '2px';

  // Format seconds to MM:SS
  function formatTime(seconds) {
    if (!seconds || seconds < 0) return '0:00';
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  }

  // Всё из store — бэкенд отправляет позицию каждую секунду
  $: currentTime = formatTime($trackPosition);
  $: totalTime = formatTime($trackDuration);
</script>

<div class="progress-container">
  {#if showTime}
    <span class="time current">{currentTime}</span>
  {/if}

  <div
    class="progress-bar"
    style="height: {height}; border-radius: {borderRadius};"
  >
    <div
      class="progress-fill"
      style="width: {$trackProgress}%; border-radius: {borderRadius};"
    ></div>
    <div
      class="progress-glow"
      style="width: {$trackProgress}%;"
    ></div>
  </div>

  {#if showTime}
    <span class="time total">{totalTime}</span>
  {/if}
</div>

<style>
  .progress-container {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    width: 100%;
  }

  .time {
    font-family: 'Rubik', sans-serif;
    font-size: 0.75rem;
    color: var(--lightVibrant, rgba(255, 255, 255, 0.7));
    min-width: 3.5rem;
    text-shadow: 0 0 10px var(--vibrant, rgba(184, 115, 51, 0.3));
  }

  .time.current {
    text-align: right;
  }

  .time.total {
    text-align: left;
    opacity: 0.6;
  }

  .progress-bar {
    flex: 1;
    position: relative;
    background: var(--darkMuted, rgba(255, 255, 255, 0.1));
    overflow: hidden;
  }

  .progress-fill {
    height: 100%;
    background: linear-gradient(
      90deg,
      var(--vibrant, #B87333) 0%,
      var(--lightVibrant, #D4944A) 100%
    );
    transition: width 0.1s linear;
  }

  .progress-glow {
    position: absolute;
    top: 0;
    left: 0;
    height: 100%;
    background: var(--vibrant, #B87333);
    filter: blur(8px);
    opacity: 0.4;
    pointer-events: none;
    transition: width 0.1s linear;
  }
</style>
