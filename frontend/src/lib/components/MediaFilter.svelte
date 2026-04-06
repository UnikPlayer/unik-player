<script>
  import { fade, fly } from 'svelte/transition';
  import { onDestroy } from 'svelte';
  import { language } from '$lib/stores/stores.js';

  export let visible = false;
  export let onClose = () => {};

  const isBrowser = typeof window !== 'undefined';
  const API_BASE = isBrowser && (window.location.port === '7270' || window.location.port === '5173')
    ? '' : '';

  let mode = 'allowAll';
  let sources = [];
  let seenSources = [];
  let sourceInfo = []; // {id, displayName, title, artist, isPlaying}
  let loading = true;
  let pollInterval = null;

  const t = {
    en: {
      title: 'MEDIA FILTER',
      modeLabel: 'MODE',
      allowAll: 'Listen to everything',
      allowOnly: 'Listen only to selected',
      blockOnly: 'Listen to everything except',
      recommended: 'Recommended',
      sourcesLabel: 'SOURCES',
      noSources: 'Play media in any app so sources appear here',
      noMediaSource: 'No media',
      save: 'SAVE',
      cancel: 'CANCEL',
    },
    ru: {
      title: 'ФИЛЬТР МЕДИА',
      modeLabel: 'РЕЖИМ',
      allowAll: 'Слушать всё',
      allowOnly: 'Слушать только выбранные',
      blockOnly: 'Слушать всё кроме',
      recommended: 'Рекомендуется',
      sourcesLabel: 'ИСТОЧНИКИ',
      noSources: 'Запусти медиа в любом приложении, чтобы источники появились здесь',
      noMediaSource: 'Нет медиа',
      save: 'СОХРАНИТЬ',
      cancel: 'ОТМЕНА',
    }
  };

  $: texts = t[$language] || t.ru;

  $: if (visible) {
    loadFilter();
    startPolling();
  } else {
    stopPolling();
  }

  onDestroy(() => {
    stopPolling();
  });

  function startPolling() {
    stopPolling();
    pollInterval = setInterval(pollSources, 1500); // 1.5s for faster media updates
  }

  function stopPolling() {
    if (pollInterval) {
      clearInterval(pollInterval);
      pollInterval = null;
    }
  }

  async function pollSources() {
    try {
      const res = await fetch(`${API_BASE}/api/media-filter`);
      if (res.ok) {
        const data = await res.json();
        // Only update seenSources and sourceInfo, don't override user's mode/sources edits
        seenSources = [...(data.seenSources || [])];
        sourceInfo = [...(data.sourceInfo || [])];
      }
    } catch (e) {}
  }

  async function loadFilter() {
    loading = true;
    try {
      const res = await fetch(`${API_BASE}/api/media-filter`);
      if (res.ok) {
        const data = await res.json();
        mode = data.mode || 'allowAll';
        sources = [...(data.sources || [])];
        seenSources = [...(data.seenSources || [])];
        sourceInfo = [...(data.sourceInfo || [])];
      }
    } catch (e) {
      console.error('[MediaFilter] Failed to load:', e);
    }
    loading = false;
  }

  function toggleSource(appId) {
    if (sources.includes(appId)) {
      sources = sources.filter(s => s !== appId);
    } else {
      sources = [...sources, appId];
    }
  }

  async function handleSave() {
    try {
      await fetch(`${API_BASE}/api/media-filter`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ mode, sources, seenSources })
      });
    } catch (e) {
      console.error('[MediaFilter] Failed to save:', e);
    }
    onClose();
  }

</script>

{#if visible}
  <div class="filter-overlay" transition:fade={{ duration: 200 }} on:click|self={onClose}>
    <div class="filter-container" transition:fly={{ y: 30, duration: 250 }}>

      <header class="filter-header">
        <span class="filter-title">{texts.title}</span>
        <button class="close-btn" on:click={onClose}>X</button>
      </header>

      <main class="filter-body">
        {#if loading}
          <div class="loading">...</div>
        {:else}

          <!-- Mode selector -->
          <div class="section">
            <span class="section-label">{texts.modeLabel}</span>
            <div class="mode-options">
              <label class="mode-option" class:active={mode === 'allowAll'}>
                <input type="radio" bind:group={mode} value="allowAll" />
                <span class="radio-dot"></span>
                <span>{texts.allowAll}</span>
              </label>
              <label class="mode-option recommended" class:active={mode === 'allowOnly'}>
                <input type="radio" bind:group={mode} value="allowOnly" />
                <span class="radio-dot"></span>
                <span>{texts.allowOnly}</span>
                <span class="rec-badge">{texts.recommended}</span>
              </label>
              <label class="mode-option" class:active={mode === 'blockOnly'}>
                <input type="radio" bind:group={mode} value="blockOnly" />
                <span class="radio-dot"></span>
                <span>{texts.blockOnly}</span>
              </label>
            </div>
          </div>

          <!-- Sources list -->
          {#if mode !== 'allowAll'}
            <div class="section">
              <span class="section-label">{texts.sourcesLabel}</span>
              {#if sourceInfo.length === 0}
                <p class="no-sources">{texts.noSources}</p>
              {:else}
                <div class="sources-list">
                  {#each sourceInfo as info (`${info.id}-${info.title}-${info.isPlaying}`)}
                    <label class="source-item" class:checked={sources.includes(info.id)}>
                      <input
                        type="checkbox"
                        checked={sources.includes(info.id)}
                        on:change={() => toggleSource(info.id)}
                      />
                      <span class="checkbox-box"></span>
                      <div class="source-info">
                        <span class="source-name">{info.displayName || info.id}</span>
                        {#if info.title}
                          <span class="source-media" class:playing={info.isPlaying}>
                            {info.isPlaying ? '▶' : '⏸'} {info.title} — {info.artist || ''}
                          </span>
                        {:else}
                          <span class="source-media idle">{texts.noMediaSource}</span>
                        {/if}
                      </div>
                    </label>
                  {/each}
                </div>
              {/if}
            </div>
          {/if}
        {/if}
      </main>

      <footer class="filter-footer">
        <button class="btn btn-save" on:click={handleSave}>{texts.save}</button>
        <button class="btn btn-cancel" on:click={onClose}>{texts.cancel}</button>
      </footer>

    </div>
  </div>
{/if}

<style lang="scss">
  .filter-overlay {
    position: fixed;
    inset: 0;
    z-index: 1000;
    background: var(--c-backdrop, rgba(0, 0, 0, 0.65));
    backdrop-filter: blur(5px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 2rem;
  }

  .filter-container {
    width: 100%;
    max-width: 500px;
    background: var(--c1);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    clip-path: polygon(
      0px 12px, 4px 12px, 4px 8px, 8px 8px, 8px 4px, 12px 4px, 12px 0px,
      calc(100% - 12px) 0px, calc(100% - 12px) 4px, calc(100% - 8px) 4px, calc(100% - 8px) 8px, calc(100% - 4px) 8px, calc(100% - 4px) 12px, 100% 12px,
      100% calc(100% - 12px), calc(100% - 4px) calc(100% - 12px), calc(100% - 4px) calc(100% - 8px), calc(100% - 8px) calc(100% - 8px), calc(100% - 8px) calc(100% - 4px), calc(100% - 12px) calc(100% - 4px), calc(100% - 12px) 100%,
      12px 100%, 12px calc(100% - 4px), 8px calc(100% - 4px), 8px calc(100% - 8px), 4px calc(100% - 8px), 4px calc(100% - 12px), 0px calc(100% - 12px)
    );
    box-shadow: 0 8px 40px rgba(0, 0, 0, 0.3);
  }

  .filter-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1rem 1.5rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  }

  .filter-title {
    font-family: '8bitwonder', monospace;
    font-size: 0.7rem;
    color: var(--c2);
    letter-spacing: 0.06em;
  }

  .close-btn {
    font-family: '8bitwonder', monospace;
    font-size: 0.55rem;
    color: rgba(255, 255, 255, 0.4);
    background: none;
    border: 1px solid rgba(255, 255, 255, 0.15);
    padding: 0.3rem 0.5rem;
    cursor: pointer;
    transition: all 0.2s;

    &:hover {
      color: var(--c2);
      border-color: rgba(255, 255, 255, 0.3);
    }
  }

  .filter-body {
    padding: 1.5rem;
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
    max-height: 60vh;
    overflow-y: auto;
  }

  .loading {
    text-align: center;
    color: rgba(255, 255, 255, 0.3);
    font-family: '8bitwonder', monospace;
    font-size: 0.6rem;
    padding: 2rem;
  }

  .section {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .section-label {
    font-family: '8bitwonder', monospace;
    font-size: 0.55rem;
    color: #B87333;
    letter-spacing: 0.08em;
  }

  .mode-options {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .mode-option {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.6rem 1rem;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.08);
    cursor: pointer;
    transition: all 0.2s;
    font-family: 'Rubik', sans-serif;
    font-size: 0.85rem;
    color: rgba(255, 255, 255, 0.6);

    input { display: none; }

    &:hover {
      border-color: rgba(184, 115, 51, 0.3);
      background: rgba(184, 115, 51, 0.05);
    }

    &.active {
      border-color: rgba(184, 115, 51, 0.5);
      background: rgba(184, 115, 51, 0.1);
      color: var(--c2);

      .radio-dot {
        background: #B87333;
      }
    }

    &.recommended {
      &.active {
        border-color: rgba(184, 115, 51, 0.6);
        background: rgba(184, 115, 51, 0.15);
      }
    }
  }

  .rec-badge {
    font-family: '8bitwonder', monospace;
    font-size: 0.4rem;
    color: #B87333;
    border: 1px solid rgba(184, 115, 51, 0.4);
    padding: 0.15rem 0.4rem;
    margin-left: auto;
    letter-spacing: 0.05em;
    flex-shrink: 0;
  }

  .radio-dot {
    width: 10px;
    height: 10px;
    border: 2px solid rgba(255, 255, 255, 0.2);
    background: transparent;
    transition: all 0.2s;
    flex-shrink: 0;
  }

  .no-sources {
    font-family: 'Rubik', sans-serif;
    font-size: 0.8rem;
    color: rgba(255, 255, 255, 0.35);
    line-height: 1.6;
    margin: 0;
    padding: 1rem;
    text-align: center;
    border: 1px dashed rgba(255, 255, 255, 0.08);
  }

  .sources-list {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
  }

  .source-item {
    display: flex;
    align-items: flex-start;
    gap: 0.75rem;
    padding: 0.75rem 1rem;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.08);
    cursor: pointer;
    transition: all 0.2s;

    input { display: none; }

    .checkbox-box {
      margin-top: 2px;
    }

    &:hover {
      border-color: rgba(184, 115, 51, 0.3);
    }

    &.checked {
      border-color: rgba(184, 115, 51, 0.5);
      background: rgba(184, 115, 51, 0.08);

      .checkbox-box {
        background: #B87333;
        border-color: #B87333;
      }
    }
  }

  .checkbox-box {
    width: 14px;
    height: 14px;
    border: 2px solid rgba(255, 255, 255, 0.2);
    background: transparent;
    transition: all 0.2s;
    flex-shrink: 0;
  }

  .source-info {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
    overflow: hidden;
    flex: 1;
  }

  .source-name {
    font-family: '8bitwonder', monospace;
    font-size: 0.65rem;
    color: var(--c2);
  }

  .source-media {
    font-family: 'Rubik', sans-serif;
    font-size: 0.75rem;
    color: rgba(255, 255, 255, 0.35);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;

    &.playing {
      color: #B87333;
    }

    &.idle {
      color: rgba(255, 255, 255, 0.2);
      font-style: italic;
    }
  }

  .filter-footer {
    display: flex;
    gap: 1rem;
    padding: 1rem 1.5rem;
    border-top: 1px solid rgba(255, 255, 255, 0.08);
  }

  .btn {
    flex: 1;
    font-family: '8bitwonder', monospace;
    font-size: 0.6rem;
    letter-spacing: 0.06em;
    padding: 0.8rem 1rem;
    border: 1px solid;
    cursor: pointer;
    transition: all 0.2s;
  }

  .btn-save {
    background: rgba(184, 115, 51, 0.2);
    border-color: #B87333;
    color: #B87333;

    &:hover {
      background: rgba(184, 115, 51, 0.35);
      color: #d4944a;
    }
  }

  .btn-cancel {
    background: transparent;
    border-color: rgba(255, 255, 255, 0.2);
    color: rgba(255, 255, 255, 0.5);

    &:hover {
      background: rgba(255, 255, 255, 0.05);
      color: var(--c2);
    }
  }
</style>
