<script>
  import { onMount } from 'svelte';
  import { siteAuth } from '$lib/stores/siteAuth.js';

  let syncing = false;
  let syncStatus = '';

  onMount(() => {
    siteAuth.loadFromBackend();
  });

  function openLogin() {
    const url = siteAuth.getLoginURL();
    const popup = window.open(url, '_blank', 'width=500,height=600');
    const poll = setInterval(async () => {
      if (!popup || popup.closed) {
        clearInterval(poll);
        await siteAuth.loadFromBackend();
        // Auto-sync after login
        if (currentAuthState) {
          await handleSync();
        }
      }
    }, 500);
  }

  // Track auth state for auto-sync
  let currentAuthState = null;
  siteAuth.subscribe(v => { currentAuthState = v; });

  async function handleLogout() {
    await siteAuth.logout();
    // Refresh player list to remove liked_* players
    window.dispatchEvent(new CustomEvent('unik-player-deleted'));
  }

  async function handleSync() {
    syncing = true;
    syncStatus = 'Syncing...';
    try {
      const players = await siteAuth.syncLikedPlayers();
      if (players.length > 0) {
        for (const p of players) {
          try {
            await fetch('/api/custom-players', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ name: `liked_${p.name}`, html: p.html_content })
            });
          } catch {}
        }
        syncStatus = `Synced ${players.length} player(s)`;
        window.dispatchEvent(new CustomEvent('unik-player-deleted'));
      } else {
        syncStatus = 'No liked players';
      }
    } catch (e) {
      syncStatus = 'Sync failed';
    }
    syncing = false;
    setTimeout(() => { syncStatus = ''; }, 3000);
  }
</script>

<div class="account-panel">
  <div class="panel-header">
    <span class="panel-title">CLOUD SYNC</span>
  </div>

  <div class="panel-content">
    {#if $siteAuth}
      <div class="logged-in">
        <div class="actions">
          <button class="btn sync-btn" on:click={handleSync} disabled={syncing}>
            {syncing ? '...' : 'SYNC'}
          </button>
          <button class="btn logout-btn" on:click={handleLogout}>
            LOGOUT
          </button>
        </div>
        {#if syncStatus}
          <span class="sync-status">{syncStatus}</span>
        {/if}
      </div>
    {:else}
      <button class="btn login-btn" on:click={openLogin}>
        SIGN IN
      </button>
      <span class="hint">Sign in to sync liked players</span>
    {/if}
  </div>
</div>

<style lang="scss">
  .account-panel {
    margin-top: 0.5rem;
    border: 1px solid rgba(0, 0, 0, 0.15);
    overflow: hidden;
  }

  .panel-header {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.6rem 0.75rem;
    border-bottom: 1px solid rgba(0, 0, 0, 0.1);
  }

  .panel-title {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
    letter-spacing: 0.08em;
  }

  .panel-content {
    padding: 0.75rem;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .logged-in {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .actions {
    display: flex;
    gap: 0.4rem;
  }

  .btn {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    letter-spacing: 0.05em;
    padding: 0.4rem 0.6rem;
    cursor: pointer;
    transition: all 0.2s;
  }

  .login-btn {
    background: rgba(0, 0, 0, 0.05);
    border: 1px solid rgba(0, 0, 0, 0.2);
    color: var(--c1);
    width: 100%;

    &:hover {
      background: rgba(0, 0, 0, 0.1);
      border-color: var(--c1);
    }
  }

  .sync-btn {
    flex: 1;
    background: rgba(0, 0, 0, 0.05);
    border: 1px solid rgba(0, 0, 0, 0.15);
    color: var(--c1);

    &:hover:not(:disabled) {
      background: rgba(0, 0, 0, 0.1);
    }

    &:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  }

  .logout-btn {
    background: rgba(0, 0, 0, 0.03);
    border: 1px solid rgba(0, 0, 0, 0.1);
    color: rgba(0, 0, 0, 0.5);

    &:hover {
      background: rgba(0, 0, 0, 0.08);
      color: var(--c1);
    }
  }

  .sync-status {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: rgba(0, 0, 0, 0.5);
  }

  .hint {
    font-family: 'Rubik', sans-serif;
    font-size: 1rem;
    color: rgba(0, 0, 0, 0.4);
    text-align: center;
  }
</style>
