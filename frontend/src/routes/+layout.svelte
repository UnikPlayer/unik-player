<script>
  import { connect } from '$lib/ws.js';
  import { onMount } from 'svelte';
  import { page } from '$app/state';

  let showHeader = false;

  onMount(() => {
    connect();
  });

  $: {
    showHeader = page.url?.pathname !== '/player' && page.url?.pathname !== '/';
  }
</script>

<!-- SVG filter for pixelate effect -->
<svg class="svg-filters" aria-hidden="true">
  <defs>
    <filter id="pixelate-8">
      <feFlood x="0" y="0" height="1" width="1" />
      <feComposite width="8" height="8" />
      <feTile result="a" />
      <feComposite in="SourceGraphic" in2="a" operator="in" />
      <feMorphology operator="dilate" radius="4" />
    </filter>
    <filter id="pixelate-4">
      <feFlood x="0" y="0" height="1" width="1" />
      <feComposite width="4" height="4" />
      <feTile result="a" />
      <feComposite in="SourceGraphic" in2="a" operator="in" />
      <feMorphology operator="dilate" radius="2" />
    </filter>
  </defs>
</svg>

<!-- Background -->
<div class="bg-gradient"></div>

{#if showHeader}
  <header>
    <a href="/" class="nav-link">PLAYERS</a>
    <a href="/howToMake" class="nav-link">DOCS</a>
  </header>
{/if}

<slot />

<style>
  .svg-filters {
    position: absolute;
    width: 0;
    height: 0;
    overflow: hidden;
  }

  .bg-gradient {
    position: fixed;
    inset: 0;
    background: #ffffff;
    z-index: -3;
  }

  :global(.marquee__inner),
  :global(.marquee__content),
  :global(.marquee__content--clone) {
    color: inherit;
    font-family: inherit;
    white-space: nowrap;
  }

  .nav-link {
    cursor: pointer;
    user-select: none;
    text-decoration: none;
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    letter-spacing: 0.05em;
    color: rgba(255, 255, 255, 0.5);
    transition: color 0.2s;
    background: none;
    border: none;
    padding: 0.5rem 1rem;
  }

  .nav-link:visited {
    color: rgba(255, 255, 255, 0.5);
  }

  .nav-link:hover {
    color: white;
    text-decoration: none;
  }

  header {
    display: grid;
    grid-template-columns: auto auto;
    align-items: center;
    justify-content: center;
    gap: 1.5rem;
    padding: 1rem 2rem;
    position: sticky;
    top: 0;
    z-index: 50;
    background: rgba(255, 255, 255, 0.85);
    backdrop-filter: blur(12px);
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  }

  :global(html, body, main) {
    margin: 0;
    padding: 0;
    background-color: #ffffff;
  }

  :global(h1, h2, h3, h4, h5, h6, p) {
    color: var(--c-text);
  }
</style>
