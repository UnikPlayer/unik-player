<script>
  import { onMount } from 'svelte';
  import { page } from '$app/stores';

  let status = $state('working');

  onMount(async () => {
    const token = $page.url.searchParams.get('token');
    if (!token) {
      status = 'error';
      return;
    }

    // Send token to backend to save
    try {
      const res = await fetch('/api/site-auth', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token })
      });
      if (res.ok) {
        status = 'done';
        setTimeout(() => { try { window.close(); } catch {} }, 1500);
      } else {
        status = 'error';
      }
    } catch {
      status = 'error';
    }
  });
</script>

<div class="cb-page">
  <div class="box">
    {#if status === 'working'}
      <h1>...</h1>
    {:else if status === 'done'}
      <h1>Logged in</h1>
      <p>You can close this window</p>
    {:else}
      <h1>Error</h1>
      <p>No token provided</p>
    {/if}
  </div>
</div>

<style>
  .cb-page {
    background: #0a0a0a;
    color: #fff;
    font-family: monospace;
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100vh;
    margin: 0;
  }
  .box { text-align: center; }
  h1 { font-size: 2rem; margin-bottom: 0.5rem; }
  p { color: rgba(255,255,255,0.6); }
</style>
