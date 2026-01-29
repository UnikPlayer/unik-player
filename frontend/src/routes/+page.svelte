<script>
  import { onMount } from 'svelte';
  import { fade, fly, scale } from 'svelte/transition';
  import { getAllPlayers } from '$lib/getPlayers.js';
  import PlayerCard from '$lib/components/PlayerCard.svelte';
  import Editor from '$lib/components/Editor.svelte';
  import Notification from '$lib/components/Notification.svelte';
  import { title, artist, thumbnail, ShowTrack, language } from '$lib/stores/stores.js';

  let players = [];
  let hoveredStep = null;

  // Backend API base URL
  const isBrowser = typeof window !== 'undefined';
  const API_BASE = isBrowser && window.location.port === '5173'
    ? 'http://localhost:27272'
    : '';

  /**
   * Transform user CSS to be scoped to player preview.
   * User writes: .mainDiv { background: red; }
   * We output: .preview-container .player-Generic .mainDiv { background: red !important; }
   */
  function transformCSSForPreview(rawCSS, playerName) {
    if (!rawCSS || !rawCSS.trim()) return '';

    const scope = `.preview-container .player-${playerName}`;
    let css = rawCSS.replace(/\/\*[\s\S]*?\*\//g, '');
    const parts = css.split('}');
    const transformed = [];

    for (let part of parts) {
      part = part.trim();
      if (!part) continue;

      const braceIdx = part.indexOf('{');
      if (braceIdx === -1) continue;

      let selector = part.substring(0, braceIdx).trim();
      let body = part.substring(braceIdx + 1).trim();

      if (!selector || !body) continue;
      if (selector.startsWith('@')) {
        transformed.push(part + '}');
        continue;
      }

      const selectors = selector.split(',').map(s => {
        s = s.trim();
        if (!s) return '';
        if (s.includes('.preview-container')) return s;
        if (s === '*') return `${scope} *`;
        return `${scope} ${s}`;
      }).filter(s => s);

      if (selectors.length === 0) continue;

      body = body.replace(/:\s*([^;!]+);/g, ': $1 !important;');
      if (body && !body.endsWith(';') && !body.endsWith('!important')) {
        body = body.replace(/:\s*([^;!{}]+)$/, ': $1 !important');
      }

      transformed.push(`${selectors.join(', ')} { ${body} }`);
    }

    return transformed.join('\n');
  }

  /**
   * Load and inject CSS for all players
   */
  async function loadAllPlayerCSS() {
    const playerNames = ['BackPicture', 'BigHead', 'Generic', 'Separate'];
    let allCSS = '';

    for (const name of playerNames) {
      try {
        const res = await fetch(`${API_BASE}/api/css/${name}`);
        if (res.ok) {
          const rawCSS = await res.text();
          if (rawCSS && rawCSS.trim()) {
            const transformed = transformCSSForPreview(rawCSS, name);
            allCSS += `/* === ${name} === */\n${transformed}\n\n`;
          }
        }
      } catch (err) {
        console.warn(`[CSS] Failed to load CSS for ${name}:`, err);
      }
    }

    // Inject all CSS into head
    if (allCSS.trim()) {
      const existing = document.getElementById('unik-preview-css');
      if (existing) existing.remove();

      const style = document.createElement('style');
      style.id = 'unik-preview-css';
      style.textContent = allCSS;
      document.head.appendChild(style);
      console.log('[CSS] Injected preview CSS for all players');
    }
  }

  onMount(() => {
    players = getAllPlayers();
    loadAllPlayerCSS();

    // Listen for CSS refresh events from Editor
    const handleCSSRefresh = () => {
      console.log('[Main] CSS refresh triggered');
      loadAllPlayerCSS();
    };
    window.addEventListener('unik-css-refresh', handleCSSRefresh);

    return () => {
      window.removeEventListener('unik-css-refresh', handleCSSRefresh);
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
        <PlayerCard component={player.component} name={player.name} />
      {/each}

      <!-- Add Custom Card -->
      <div class="add-custom-card">
        <div class="add-icon">+</div>
        <span class="add-title">Add Custom</span>
        <span class="add-desc">Import your own .svelte file</span>
        <span class="add-soon">coming soon</span>
      </div>
    </div>
  </section>

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
    font-family: monospace;
    font-size: 1.8rem;
    padding-left: 1.5rem;
  }

  .logo-text {
    font-family: 'JetBrains Mono', monospace;
    font-size: 1.5rem;
    font-weight: 700;
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
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    font-weight: 600;
    color: rgba(255, 255, 255, 0.5);
    text-decoration: none;
    letter-spacing: 0.1em;
    transition: color 0.2s;

    &:hover {
      color: white;
    }
  }

  .lang-toggle {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    font-weight: 600;
    color: #B87333;
    background: rgba(184, 115, 51, 0.1);
    border: 1px solid rgba(184, 115, 51, 0.4);
    border-radius: 4px;
    padding: 0.3rem 0.6rem;
    cursor: pointer;
    letter-spacing: 0.1em;
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
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    color: #B87333;
    letter-spacing: 0.05em;
  }

  .step-text {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }

  .step-title {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    font-weight: 600;
    color: white;
    letter-spacing: 0.02em;
  }

  .step-desc {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.65rem;
    color: rgba(255, 255, 255, 0.4);
  }

  .guide-arrow {
    font-family: monospace;
    font-size: 1.2rem;
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
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    font-weight: 600;
    color: rgba(255, 255, 255, 0.5);
    text-decoration: none;
    letter-spacing: 0.1em;
    transition: color 0.2s;

    &:hover {
      color: white;
    }
  }

  .footer-btn-donate {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    font-weight: 600;
    color: #B87333;
    text-decoration: none;
    letter-spacing: 0.1em;
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
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.65rem;
    color: rgba(255, 255, 255, 0.3);
    letter-spacing: 0.2em;
  }

  // Add Custom Card
  .add-custom-card {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    min-height: 200px;
    background: rgba(255, 255, 255, 0.02);
    border: 2px dashed rgba(255, 255, 255, 0.15);
    border-radius: 8px;
    cursor: not-allowed;
    transition: all 0.2s;
    opacity: 0.6;

    &:hover {
      border-color: rgba(184, 115, 51, 0.3);
      background: rgba(184, 115, 51, 0.03);
    }
  }

  .add-icon {
    font-size: 2.5rem;
    color: rgba(255, 255, 255, 0.3);
    font-weight: 300;
    line-height: 1;
  }

  .add-title {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.85rem;
    font-weight: 600;
    color: rgba(255, 255, 255, 0.5);
    letter-spacing: 0.05em;
  }

  .add-desc {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.65rem;
    color: rgba(255, 255, 255, 0.3);
  }

  .add-soon {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.6rem;
    color: #B87333;
    background: rgba(184, 115, 51, 0.15);
    padding: 0.2rem 0.5rem;
    border-radius: 4px;
    margin-top: 0.5rem;
    text-transform: uppercase;
    letter-spacing: 0.1em;
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
