<script>
  import { connect } from '$lib/ws.js';
  import { onMount } from 'svelte';
  import { page } from '$app/state';

  let showHeader = false;

  onMount(() => {
    connect();
  });

  $: {
    // Hide header on main page (has its own) and player page
    showHeader = page.url?.pathname !== '/player' && page.url?.pathname !== '/';
  }
</script>

{#if showHeader}
  <header>
    <a href="/">Main</a>
    <a href="/howToMake">Custom Design</a>
  </header>
{/if}

<slot />

<style>
  :global(.marquee__inner),
  :global(.marquee__content),
  :global(.marquee__content--clone) {
    color: inherit;
    font-family: inherit;
    white-space: nowrap;
  }

  a,
  a:visited,
  a::selection {
    cursor: pointer;
    user-select: none;
    text-decoration: none;

    font-family: 'JetBrains Mono', monospace;
    font-size: 0.85rem;
    font-weight: 600;
    letter-spacing: 0.05em;

    color: rgba(255, 255, 255, 0.7);
    transition: all 0.2s;

    height: 80%;
    background: rgba(255, 255, 255, 0.05);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 4px;

    display: flex;
    justify-content: center;
    align-items: center;
    padding: 0.75rem 1.5rem;
  }

  a:hover,
  a:active,
  a:focus {
    background: rgba(184, 115, 51, 0.15);
    border-color: rgba(184, 115, 51, 0.3);
    color: #B87333;
    text-decoration: none;
    outline: none;
  }

  header {
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    gap: 1rem;
    padding: 1.5rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  }

  :global(html, body, main) {
    margin: 0;
    padding: 0;
    background-color: #050510;
  }

  :global(h1, h2, h3, h4, h5, h6, p) {
    color: white;
    font-family: 'JetBrains Mono', system-ui, sans-serif;
  }
</style>
