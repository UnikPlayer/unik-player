<script>
  import { onMount } from 'svelte';
  import { fade, fly, scale } from 'svelte/transition';
  import { getAllPlayers, getAllPlayersAsync, invalidateCustomPlayersCache, getBuiltInPlayerNames, getPlayerMeta } from '$lib/getPlayers.js';
  import PlayerCard from '$lib/components/PlayerCard.svelte';
  import Editor from '$lib/components/Editor.svelte';
  import Notification from '$lib/components/Notification.svelte';
  import CustomPlayerUploader from '$lib/components/CustomPlayerUploader.svelte';
  import MediaFilter from '$lib/components/MediaFilter.svelte';
  import { title, artist, thumbnail, ShowTrack, language } from '$lib/stores/stores.js';
  import { transformCSS, injectCSS, loadCSSFromBackend } from '$lib/utils/playerCSS.js';

  let players = [];
  let hoveredStep = null;
  let showUploader = false;
  let showFilter = false;

  /**
   * Load and inject CSS for all built-in players (preview cards)
   */
  async function loadAllPlayerCSS() {
    const playerNames = getBuiltInPlayerNames();
    let allCSS = '';

    for (const name of playerNames) {
      try {
        let rawCSS = await loadCSSFromBackend(name);

        if (!rawCSS) {
          // No user CSS — use factory default from player meta
          const meta = getPlayerMeta(name);
          rawCSS = meta?.defaultCSS || '';
        }

        if (rawCSS) {
          const transformed = transformCSS(rawCSS, name, '.preview-container');
          allCSS += `/* === ${name} === */\n${transformed}\n\n`;
        }
      } catch (err) {
        console.warn(`[CSS] Failed to load CSS for ${name}:`, err);
      }
    }

    injectCSS(allCSS, 'unik-preview-css');
  }

  async function loadPlayers() {
    players = await getAllPlayersAsync();
    loadAllPlayerCSS();
  }

  async function handleCustomPlayerAdded(name) {
    invalidateCustomPlayersCache();
    await loadPlayers();
  }

  onMount(() => {
    loadPlayers();

    // Listen for CSS refresh events from Editor
    const handleCSSRefresh = () => {
      console.log('[Main] CSS refresh triggered');
      loadAllPlayerCSS();
    };

    // Listen for custom player deletion
    const handlePlayerDeleted = () => {
      console.log('[Main] Player deleted, refreshing list');
      loadPlayers();
    };

    window.addEventListener('unik-css-refresh', handleCSSRefresh);
    window.addEventListener('unik-player-deleted', handlePlayerDeleted);

    return () => {
      window.removeEventListener('unik-css-refresh', handleCSSRefresh);
      window.removeEventListener('unik-player-deleted', handlePlayerDeleted);
    };
  });

  function toggleLanguage() {
    language.update(l => l === 'ru' ? 'en' : 'ru');
  }

  // Translations
  const t = {
    en: {
      widgets: 'WIDGETS',
      docs: 'DOCS',
      step1: 'Select widget',
      step1Desc: 'Choose your style',
      step2: 'Click SELECT',
      step2Desc: 'Copy link to clipboard',
      step3: 'Paste in OBS',
      step3Desc: 'Browser Source → URL',
      addCustom: 'Add Custom',
      addCustomDesc: 'Upload your HTML player',
    },
    ru: {
      widgets: 'ВИДЖЕТЫ',
      docs: 'ДОКИ',
      step1: 'Выбери виджет',
      step1Desc: 'Понравившийся стиль',
      step2: 'Нажми SELECT',
      step2Desc: 'Ссылка скопируется',
      step3: 'Вставь в OBS',
      step3Desc: 'Browser Source → URL',
      addCustom: 'Добавить',
      addCustomDesc: 'Загрузи свой HTML плеер',
    }
  };

  $: texts = t[$language];
</script>

<Notification />
<Editor />

<div class="page">
  <!-- Background effects -->
  <div class="bg-gradient"></div>
  <div class="bg-grid"></div>

  <!-- Overlay for hover -->
  {#if hoveredStep !== null}
    <div class="overlay" transition:fade={{ duration: 200 }}></div>
  {/if}

  <!-- Hover images -->
  {#if hoveredStep === 1}
    <div class="hover-image" transition:scale={{ duration: 300, start: 0.8 }}>
      <img src="/obs1.png" alt="Step 1" />
    </div>
  {/if}
  {#if hoveredStep === 2}
    <div class="hover-image" transition:scale={{ duration: 300, start: 0.8 }}>
      <img src="/obs2.png" alt="Step 2" />
    </div>
  {/if}
  {#if hoveredStep === 3}
    <div class="hover-image" transition:scale={{ duration: 300, start: 0.8 }}>
      <img src="/obs3.png" alt="Step 3" />
    </div>
  {/if}

  <!-- Header -->
  <header class="page-header">
    <div class="header-spacer"></div>

    <div class="header-center">
      <span class="logo-icon">[ ]</span>
      <span class="logo-text">UnikPlayer</span>
      <nav class="nav-links">
        <a href="#library" class="nav-link">{texts.widgets}</a>
        <a href="/howToMake" class="nav-link">{texts.docs}</a>
        <button class="nav-link filter-btn" on:click={() => showFilter = true}>FILTER</button>
      </nav>
    </div>

    <button class="lang-toggle" on:click={toggleLanguage}>
      {$language === 'ru' ? 'EN' : 'RU'}
    </button>
  </header>

  <!-- Guideline Section -->
  <section class="guideline">
    <div
      class="guide-step"
      on:mouseenter={() => hoveredStep = 1}
      on:mouseleave={() => hoveredStep = null}
    >
      <span class="step-num">[01]</span>
      <div class="step-text">
        <span class="step-title">{texts.step1}</span>
        <span class="step-desc">{texts.step1Desc}</span>
      </div>
    </div>

    <span class="guide-arrow">→</span>

    <div
      class="guide-step"
      on:mouseenter={() => hoveredStep = 2}
      on:mouseleave={() => hoveredStep = null}
    >
      <span class="step-num">[02]</span>
      <div class="step-text">
        <span class="step-title">{texts.step2}</span>
        <span class="step-desc">{texts.step2Desc}</span>
      </div>
    </div>

    <span class="guide-arrow">→</span>

    <div
      class="guide-step"
      on:mouseenter={() => hoveredStep = 3}
      on:mouseleave={() => hoveredStep = null}
    >
      <span class="step-num">[03]</span>
      <div class="step-text">
        <span class="step-title">{texts.step3}</span>
        <span class="step-desc">{texts.step3Desc}</span>
      </div>
    </div>
  </section>

  <!-- Player Grid -->
  <section class="players-section" id="library">
    <div class="players-grid">
      {#each players as player}
        <PlayerCard
          component={player.component}
          name={player.name}
          isCustom={player.isCustom || false}
          error={player.error || null}
        />
      {/each}

      <!-- Add Custom Card -->
      <button class="add-custom-card" on:click={() => showUploader = true}>
        <div class="add-icon">&lt;/&gt;</div>
        <span class="add-title">{texts.addCustom}</span>
        <span class="add-desc">{texts.addCustomDesc}</span>
      </button>
    </div>
  </section>

  <!-- Custom Player Uploader -->
  <CustomPlayerUploader
    visible={showUploader}
    onClose={() => showUploader = false}
    onSuccess={handleCustomPlayerAdded}
  />

  <!-- Media Filter -->
  <MediaFilter
    visible={showFilter}
    onClose={() => showFilter = false}
  />

  <!-- Footer -->
  <footer class="page-footer">
    <a href="https://github.com/UNIKNOW0/unik-player" target="_blank" rel="noopener" class="footer-link">
      GITHUB
    </a>
    <a href="https://www.donationalerts.com/r/unikn0w" target="_blank" rel="noopener" class="footer-btn-donate">
      DONATE
    </a>
    <span class="footer-text">v0.7</span>
  </footer>
</div>

<style lang="scss">
  .page {
    height: 100vh;
    display: flex;
    flex-direction: column;
    position: relative;
    overflow: hidden;
  }

  // Background Effects
  .bg-gradient {
    position: fixed;
    inset: 0;
    background:
      radial-gradient(ellipse at 20% 20%, rgba(184, 115, 51, 0.08) 0%, transparent 50%),
      radial-gradient(ellipse at 80% 80%, rgba(99, 102, 241, 0.06) 0%, transparent 50%),
      radial-gradient(ellipse at 50% 50%, rgba(20, 20, 30, 1) 0%, rgba(5, 5, 10, 1) 100%);
    z-index: -3;
  }

  .bg-grid {
    position: fixed;
    inset: 0;
    background-image:
      linear-gradient(rgba(255, 255, 255, 0.02) 1px, transparent 1px),
      linear-gradient(90deg, rgba(255, 255, 255, 0.02) 1px, transparent 1px);
    background-size: 50px 50px;
    z-index: -2;
  }

  // Overlay for hover
  .overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.7);
    z-index: 100;
    pointer-events: none;
  }

  // Hover images
  .hover-image {
    position: fixed;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    z-index: 101;
    pointer-events: none;

    img {
      width: 600px;
      height: 600px;
      object-fit: contain;
      border-radius: 12px;
      box-shadow: 0 0 60px rgba(184, 115, 51, 0.3);
    }
  }

  // Header
  .page-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1.2rem 2rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    flex-shrink: 0;
  }

  .header-spacer {
    width: 60px; // Same as lang-toggle width for balance
  }

  .header-center {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .logo-icon {
    color: #B87333;
    font-family: 'Press Start 2P', monospace;
    font-size: 0.9rem;
    padding-left: 1.5rem;
  }

  .logo-text {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.8rem;
    font-weight: 400;
    color: white;
    letter-spacing: 0.02em;
    padding: 0rem 0.5rem;
  }

  .nav-links {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 1.5rem;

    
  }

  .nav-link {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.5rem;
    font-weight: 400;
    color: rgba(255, 255, 255, 0.5);
    text-decoration: none;
    letter-spacing: 0.05em;
    transition: color 0.2s;

    &:hover {
      color: white;
    }
  }

  .filter-btn {
    background: none;
    border: none;
    cursor: pointer;
    padding: 0;
  }

  .lang-toggle {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.45rem;
    font-weight: 400;
    color: #B87333;
    background: rgba(184, 115, 51, 0.1);
    border: 1px solid rgba(184, 115, 51, 0.4);
    border-radius: 4px;
    padding: 0.3rem 0.6rem;
    cursor: pointer;
    letter-spacing: 0.05em;
    transition: all 0.2s;

    &:hover {
      background: rgba(184, 115, 51, 0.2);
      border-color: #B87333;
    }
  }

  // Guideline
  .guideline {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 2rem;
    padding: 1.5rem 2rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    flex-shrink: 0;
  }

  .guide-step {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.75rem 1.25rem;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 6px;
    cursor: pointer;
    transition: all 0.2s;

    &:hover {
      border-color: rgba(184, 115, 51, 0.5);
      background: rgba(184, 115, 51, 0.05);
    }
  }

  .step-num {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.45rem;
    color: #B87333;
    letter-spacing: 0.02em;
  }

  .step-text {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }

  .step-title {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.5rem;
    font-weight: 400;
    color: white;
    letter-spacing: 0.02em;
  }

  .step-desc {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.35rem;
    color: rgba(255, 255, 255, 0.4);
  }

  .guide-arrow {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.8rem;
    color: rgba(255, 255, 255, 0.2);
  }

  // Players Section
  .players-section {
    flex: 1;
    padding: 1.5rem 2rem;
    overflow-y: auto;
    display: flex;
    align-items: center;
  }

  .players-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 1.25rem;
    width: 100%;
    max-width: 1400px;
    margin: 0 auto;
  }

  // Footer
  .page-footer {
    padding: 1rem 2rem;
    border-top: 1px solid rgba(255, 255, 255, 0.05);
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 1.5rem;
    flex-shrink: 0;
  }

  .footer-link {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.45rem;
    font-weight: 400;
    color: rgba(255, 255, 255, 0.5);
    text-decoration: none;
    letter-spacing: 0.05em;
    transition: color 0.2s;

    &:hover {
      color: white;
    }
  }

  .footer-btn-donate {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.45rem;
    font-weight: 400;
    color: #B87333;
    text-decoration: none;
    letter-spacing: 0.05em;
    padding: 0.4rem 1rem;
    border: 1px solid rgba(184, 115, 51, 0.4);
    border-radius: 4px;
    background: rgba(184, 115, 51, 0.1);
    transition: all 0.2s;

    &:hover {
      background: rgba(184, 115, 51, 0.2);
      border-color: #B87333;
    }
  }

  .footer-text {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.4rem;
    color: rgba(255, 255, 255, 0.3);
    letter-spacing: 0.1em;
  }

  // Add Custom Card
  .add-custom-card {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.75rem;
    min-height: 200px;
    background: rgba(184, 115, 51, 0.03);
    border: 2px dashed rgba(184, 115, 51, 0.3);
    border-radius: 8px;
    cursor: pointer;
    transition: all 0.2s;

    &:hover {
      border-color: rgba(184, 115, 51, 0.6);
      background: rgba(184, 115, 51, 0.08);

      .add-icon {
        color: #B87333;
        transform: scale(1.1);
      }

      .add-title {
        color: #B87333;
      }
    }

    &:active {
      transform: scale(0.98);
    }
  }

  .add-icon {
    font-family: 'JetBrains Mono', monospace;
    font-size: 1.5rem;
    color: rgba(184, 115, 51, 0.6);
    font-weight: 600;
    line-height: 1;
    transition: all 0.2s;
  }

  .add-title {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.5rem;
    font-weight: 400;
    color: rgba(255, 255, 255, 0.7);
    letter-spacing: 0.02em;
    transition: color 0.2s;
  }

  .add-desc {
    font-family: 'Press Start 2P', monospace;
    font-size: 0.35rem;
    color: rgba(255, 255, 255, 0.4);
  }

  // Responsive
  @media (max-width: 768px) {
    .guideline {
      flex-wrap: wrap;
      gap: 1rem;
    }

    .guide-arrow {
      display: none;
    }
  }
</style>
