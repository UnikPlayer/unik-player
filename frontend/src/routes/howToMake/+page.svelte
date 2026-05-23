<script>
  import { title, artist, thumbnail, ShowTrack } from '$lib/stores/stores.js';

  // Navigation sections
  const sections = [
    { id: 'getting-started', title: 'Getting Started', titleRu: 'РќР°С‡Р°Р»Рѕ СЂР°Р±РѕС‚С‹' },
    { id: 'obs-setup', title: 'OBS Setup', titleRu: 'РќР°СЃС‚СЂРѕР№РєР° OBS' },
    { id: 'colors', title: 'Dynamic Colors', titleRu: 'Р”РёРЅР°РјРёС‡РµСЃРєРёРµ С†РІРµС‚Р°' },
    { id: 'media-info', title: 'Media Info', titleRu: 'РњРµРґРёР° РґР°РЅРЅС‹Рµ' },
    { id: 'customization', title: 'Customization', titleRu: 'РљР°СЃС‚РѕРјРёР·Р°С†РёСЏ' },
  ];

  let activeSection = 'getting-started';
</script>

<div class="docs-page">
  <!-- Background -->
  <div class="bg-gradient"></div>
  <div class="bg-grid"></div>

  <!-- Header -->
  <header class="docs-header">
    <a href="/" class="logo">
      <span class="logo-icon">[ ]</span>
      <span class="logo-text">UnikPlayer</span>
    </a>
    <span class="header-tag">DOCUMENTATION</span>
  </header>

  <div class="docs-layout">
    <!-- Sidebar Navigation -->
    <aside class="docs-sidebar">
      <nav class="sidebar-nav">
        {#each sections as section}
          <a
            href="#{section.id}"
            class="nav-item"
            class:active={activeSection === section.id}
            on:click={() => activeSection = section.id}
          >
            <span class="nav-marker"></span>
            <span class="nav-text">{section.title}</span>
          </a>
        {/each}
      </nav>
    </aside>

    <!-- Main Content -->
    <main class="docs-content">

      <!-- Getting Started -->
      <section id="getting-started" class="doc-section">
        <div class="section-header">
          <span class="section-number">[01]</span>
          <h2>Getting Started</h2>
        </div>
        <div class="section-body">
          <p class="intro-text">
            UnikPlayer - РІРёРґР¶РµС‚ РґР»СЏ OBS. РђРІС‚РѕРјР°С‚РёС‡РµСЃРєРё РѕРїСЂРµРґРµР»СЏРµС‚ РјСѓР·С‹РєСѓ РёР· Р»СЋР±РѕРіРѕ РїСЂРёР»РѕР¶РµРЅРёСЏ:
            Spotify, YouTube Music, VK, Р±СЂР°СѓР·РµСЂС‹ Рё РґСЂСѓРіРёРµ.
          </p>

          <div class="steps-list">
            <div class="step">
            </div>
            <div class="step">
              <span class="step-num">1</span>
              <div class="step-content">
                <h4>Р’С‹Р±РµСЂРёС‚Рµ РІРёРґР¶РµС‚</h4>
                <p>РќР° РіР»Р°РІРЅРѕР№ СЃС‚СЂР°РЅРёС†Рµ РІС‹Р±РµСЂРёС‚Рµ РїРѕРЅСЂР°РІРёРІС€РёР№СЃСЏ СЃС‚РёР»СЊ РІРёРґР¶РµС‚Р° Рё РЅР°Р¶РјРёС‚Рµ SELECT.</p>
              </div>
            </div>
            <div class="step">
              <span class="step-num">2</span>
              <div class="step-content">
                <h4>Р”РѕР±Р°РІСЊС‚Рµ РІ OBS</h4>
                <p>РЎСЃС‹Р»РєР° СЃРєРѕРїРёСЂРѕРІР°РЅР° РІ Р±СѓС„РµСЂ РѕР±РјРµРЅР°. РЎРѕР·РґР°Р№С‚Рµ Browser Source РІ OBS Рё РІСЃС‚Р°РІСЊС‚Рµ СЃСЃС‹Р»РєСѓ.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- OBS Setup -->
      <section id="obs-setup" class="doc-section">
        <div class="section-header">
          <span class="section-number">[02]</span>
          <h2>OBS Setup</h2>
        </div>
        <div class="section-body">
          <div class="info-box">
            <span class="info-icon">i</span>
            <p>UnikPlayer СЂР°Р±РѕС‚Р°РµС‚ С‡РµСЂРµР· Р»РѕРєР°Р»СЊРЅС‹Р№ СЃРµСЂРІРµСЂ РЅР° РїРѕСЂС‚Сѓ 27272. OBS РїРѕР»СѓС‡Р°РµС‚ РґР°РЅРЅС‹Рµ С‡РµСЂРµР· Browser Source.</p>
          </div>

          <h3>РќР°СЃС‚СЂРѕР№РєР° Browser Source</h3>
          <div class="code-block">
            <div class="code-header">
              <span>Browser Source Settings</span>
            </div>
            <div class="code-content">
              <p><strong>URL:</strong> http://127.0.0.1:27272/player?Generic</p>
              <p><strong>Width:</strong> 500</p>
              <p><strong>Height:</strong> 200</p>
              <p><strong>Custom CSS:</strong> body &#123; background: transparent; &#125;</p>
            </div>
          </div>

          <h3>Р”РѕСЃС‚СѓРїРЅС‹Рµ РІРёРґР¶РµС‚С‹</h3>
          <ul class="widget-list">
            <li><code>?Generic</code> - РљР»Р°СЃСЃРёС‡РµСЃРєРёР№ РіРѕСЂРёР·РѕРЅС‚Р°Р»СЊРЅС‹Р№</li>
            <li><code>?BigHead</code> - Р‘РѕР»СЊС€Р°СЏ РѕР±Р»РѕР¶РєР° СЃР»РµРІР°</li>
            <li><code>?Separate</code> - Р Р°Р·РґРµР»СЊРЅС‹Рµ Р±Р»РѕРєРё</li>
            <li><code>?BackPicture</code> - РћР±Р»РѕР¶РєР° РЅР° С„РѕРЅРµ</li>
          </ul>
        </div>
      </section>

      <!-- Dynamic Colors -->
      <section id="colors" class="doc-section">
        <div class="section-header">
          <span class="section-number">[03]</span>
          <h2>Dynamic Colors</h2>
        </div>
        <div class="section-body">
          <p>
            Р¦РІРµС‚Р° Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё РёР·РІР»РµРєР°СЋС‚СЃСЏ РёР· РѕР±Р»РѕР¶РєРё Р°Р»СЊР±РѕРјР° СЃ РїРѕРјРѕС‰СЊСЋ Р±РёР±Р»РёРѕС‚РµРєРё node-vibrant.
            РСЃРїРѕР»СЊР·СѓР№С‚Рµ CSS РїРµСЂРµРјРµРЅРЅС‹Рµ РґР»СЏ РґРёРЅР°РјРёС‡РµСЃРєРѕР№ СЃС‚РёР»РёР·Р°С†РёРё.
          </p>

          <h3>CSS Variables</h3>
          <div class="color-grid">
            <div class="color-item">
              <div class="color-swatch" style="background: var(--vibrant, var(--c1))"></div>
              <code>--vibrant</code>
              <span class="color-desc">РЇСЂРєРёР№ Р°РєС†РµРЅС‚</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--muted, #8B6914)"></div>
              <code>--muted</code>
              <span class="color-desc">РџСЂРёРіР»СѓС€С‘РЅРЅС‹Р№</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--lightVibrant, #F5DEB3)"></div>
              <code>--lightVibrant</code>
              <span class="color-desc">РЎРІРµС‚Р»С‹Р№ СЏСЂРєРёР№</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--lightMuted, var(--c1))"></div>
              <code>--lightMuted</code>
              <span class="color-desc">РЎРІРµС‚Р»С‹Р№ РјСЏРіРєРёР№</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--darkVibrant, #5C4033)"></div>
              <code>--darkVibrant</code>
              <span class="color-desc">РўС‘РјРЅС‹Р№ СЏСЂРєРёР№</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--darkMuted, rgba(20, 15, 10, 0.9))"></div>
              <code>--darkMuted</code>
              <span class="color-desc">РўС‘РјРЅС‹Р№ РјСЏРіРєРёР№</span>
            </div>
          </div>

          <div class="code-block">
            <div class="code-header">
              <span>РџСЂРёРјРµСЂ РёСЃРїРѕР»СЊР·РѕРІР°РЅРёСЏ</span>
            </div>
            <pre class="code-content">.my-widget &#123;
  background-color: var(--darkMuted);
  border-color: var(--vibrant);
  color: var(--lightVibrant);
&#125;</pre>
          </div>
        </div>
      </section>

      <!-- Media Info -->
      <section id="media-info" class="doc-section">
        <div class="section-header">
          <span class="section-number">[04]</span>
          <h2>Media Info</h2>
        </div>
        <div class="section-body">
          <p>РўРµРєСѓС‰РёРµ РґР°РЅРЅС‹Рµ Рѕ РІРѕСЃРїСЂРѕРёР·РІРѕРґРёРјРѕРј С‚СЂРµРєРµ:</p>

          {#if $ShowTrack && $title}
            <div class="media-display">
              <img src={$thumbnail} alt="Album" class="media-thumb" />
              <div class="media-info">
                <span class="media-label">TITLE</span>
                <span class="media-value">{$title}</span>
                <span class="media-label">ARTIST</span>
                <span class="media-value">{$artist}</span>
              </div>
            </div>
          {:else}
            <div class="no-media">
              <span class="no-media-icon">в™Є</span>
              <p>Р’РєР»СЋС‡РёС‚Рµ РјСѓР·С‹РєСѓ С‡С‚РѕР±С‹ СѓРІРёРґРµС‚СЊ РґР°РЅРЅС‹Рµ</p>
            </div>
          {/if}

          <h3>WebSocket API</h3>
          <div class="code-block">
            <div class="code-header">
              <span>РџРѕРґРєР»СЋС‡РµРЅРёРµ</span>
            </div>
            <pre class="code-content">const ws = new WebSocket('ws://127.0.0.1:62727');

ws.onmessage = (event) => &#123;
  const data = JSON.parse(event.data);
  // data.title, data.artist, data.thumbnail
&#125;;</pre>
          </div>
        </div>
      </section>

      <!-- Customization -->
      <section id="customization" class="doc-section">
        <div class="section-header">
          <span class="section-number">[05]</span>
          <h2>Customization</h2>
        </div>
        <div class="section-body">
          <p>
            РСЃРїРѕР»СЊР·СѓР№С‚Рµ РІСЃС‚СЂРѕРµРЅРЅС‹Р№ СЂРµРґР°РєС‚РѕСЂ РґР»СЏ РєР°СЃС‚РѕРјРёР·Р°С†РёРё РІРёРґР¶РµС‚РѕРІ.
            РќР°Р¶РјРёС‚Рµ EDIT РЅР° РєР°СЂС‚РѕС‡РєРµ РІРёРґР¶РµС‚Р° С‡С‚РѕР±С‹ РѕС‚РєСЂС‹С‚СЊ СЂРµРґР°РєС‚РѕСЂ.
          </p>

          <h3>Р’РѕР·РјРѕР¶РЅРѕСЃС‚Рё СЂРµРґР°РєС‚РѕСЂР°</h3>
          <ul class="feature-list">
            <li>
              <span class="feature-icon">A</span>
              <div>
                <strong>Typography</strong>
                <p>Р’С‹Р±РѕСЂ С€СЂРёС„С‚Р° РёР· СЃРёСЃС‚РµРјРЅС‹С… РёР»Рё Google Fonts</p>
              </div>
            </li>
            <li>
              <span class="feature-icon">в—ђ</span>
              <div>
                <strong>Color Mode</strong>
                <p>Dynamic (РёР· РѕР±Р»РѕР¶РєРё) РёР»Рё Static (РІР°С€ РІС‹Р±РѕСЂ)</p>
              </div>
            </li>
            <li>
              <span class="feature-icon">&lt;/&gt;</span>
              <div>
                <strong>CSS Editor</strong>
                <p>РџРѕР»РЅС‹Р№ РєРѕРЅС‚СЂРѕР»СЊ РЅР°Рґ СЃС‚РёР»СЏРјРё РІРёРґР¶РµС‚Р°</p>
              </div>
            </li>
          </ul>

          <div class="info-box warning">
            <span class="info-icon">!</span>
            <p>РР·РјРµРЅРµРЅРёСЏ РїСЂРёРјРµРЅСЏСЋС‚СЃСЏ РІ СЂРµР°Р»СЊРЅРѕРј РІСЂРµРјРµРЅРё. РќР°Р¶РјРёС‚Рµ CONFIRM С‡С‚РѕР±С‹ СЃРѕС…СЂР°РЅРёС‚СЊ.</p>
          </div>
        </div>
      </section>

    </main>
  </div>

  <!-- Footer -->
  <footer class="docs-footer">
    <span>UnikPlayer Documentation вЂў v0.7</span>
  </footer>
</div>

<style lang="scss">
  .docs-page {
    min-height: 100vh;
    position: relative;
    color: var(--c-text);
    background: var(--c2);
  }

  // Background
  .bg-gradient {
    position: fixed;
    inset: 0;
    background:
      radial-gradient(ellipse at 20% 20%, rgba(0, 0, 0, 0.03) 0%, transparent 50%),
      radial-gradient(ellipse at 80% 80%, rgba(0, 0, 0, 0.02) 0%, transparent 50%),
      var(--c2);
    z-index: -2;
  }

  .bg-grid {
    position: fixed;
    inset: 0;
    background-image:
      linear-gradient(color-mix(in srgb, var(--c-text) 1.5%, transparent) 1px, transparent 1px),
      linear-gradient(90deg, color-mix(in srgb, var(--c-text) 1.5%, transparent) 1px, transparent 1px);
    background-size: 50px 50px;
    z-index: -1;
  }

  // Header
  .docs-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1.5rem 3rem;
    border-bottom: 1px solid color-mix(in srgb, var(--c-text) 6%, transparent);
    position: sticky;
    top: 0;
    background: color-mix(in srgb, var(--c2) 95%, transparent);
    backdrop-filter: blur(10px);
    z-index: 100;
  }

  .logo {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    text-decoration: none;
  }

  .logo-icon {
    color: var(--c1);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
  }

  .logo-text {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c-text);
    letter-spacing: 0.06em;
  }

  .header-tag {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
    letter-spacing: 0.08em;
    padding: 0.4rem 1rem;
    border: 1px solid color-mix(in srgb, var(--c1) 30%, transparent);
  }

  // Layout
  .docs-layout {
    display: grid;
    grid-template-columns: 250px 1fr;
    max-width: 1400px;
    margin: 0 auto;
    min-height: calc(100vh - 80px);
  }

  // Sidebar
  .docs-sidebar {
    padding: 2rem 1.5rem;
    border-right: 1px solid color-mix(in srgb, var(--c-text) 6%, transparent);
    position: sticky;
    top: 80px;
    height: fit-content;
  }

  .sidebar-nav {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .nav-item {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.75rem 1rem;
    text-decoration: none;
    color: color-mix(in srgb, var(--c-text) 40%, transparent);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    letter-spacing: 0.04em;
    transition: all 0.2s;

    &:hover {
      color: var(--c-text);
      background: color-mix(in srgb, var(--c-text) 4%, transparent);
    }

    &.active {
      color: var(--c1);
      background: color-mix(in srgb, var(--c1) 10%, transparent);

      .nav-marker {
        background: var(--c1);
      }
    }
  }

  .nav-marker {
    width: 4px;
    height: 4px;
    background: color-mix(in srgb, var(--c-text) 20%, transparent);
  }

  // Content
  .docs-content {
    padding: 2rem 3rem 4rem;
  }

  .doc-section {
    margin-bottom: 4rem;
    scroll-margin-top: 100px;
  }

  .section-header {
    display: flex;
    align-items: center;
    gap: 1rem;
    margin-bottom: 1.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid color-mix(in srgb, var(--c-text) 8%, transparent);

    h2 {
      font-family: '8bitwonder', monospace;
      font-size: 1rem;
      margin: 0;
      color: var(--c-text);
      letter-spacing: 0.06em;
    }
  }

  .section-number {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
  }

  .section-body {
    h3 {
      font-family: '8bitwonder', monospace;
      font-size: 1rem;
      color: var(--c-text);
      margin: 2rem 0 1rem;
      letter-spacing: 0.06em;
    }

    p {
      font-family: 'Rubik', sans-serif;
      font-size: 1rem;
      color: color-mix(in srgb, var(--c-text) 55%, transparent);
      line-height: 1.8;
      margin: 0 0 1rem;
    }
  }

  .intro-text {
    font-size: 1rem !important;
    max-width: 600px;
  }

  // Steps
  .steps-list {
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
    margin-top: 2rem;
  }

  .step {
    display: flex;
    gap: 1.5rem;
    align-items: flex-start;
  }

  .step-num {
    width: 40px;
    height: 40px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: color-mix(in srgb, var(--c1) 15%, transparent);
    border: 1px solid color-mix(in srgb, var(--c1) 35%, transparent);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
    flex-shrink: 0;
  }

  .step-content {
    h4 {
      font-family: '8bitwonder', monospace;
      font-size: 1rem;
      color: var(--c-text);
      margin: 0 0 0.5rem;
      letter-spacing: 0.04em;
    }

    p {
      margin: 0;
    }
  }

  // Info Box
  .info-box {
    display: flex;
    gap: 1rem;
    padding: 1rem 1.5rem;
    background: color-mix(in srgb, var(--c1) 8%, transparent);
    border: 1px solid color-mix(in srgb, var(--c1) 25%, transparent);
    margin: 1.5rem 0;

    &.warning {
      background: color-mix(in srgb, var(--c1) 8%, transparent);
      border-color: color-mix(in srgb, var(--c1) 25%, transparent);

      .info-icon {
        color: var(--c1);
        background: color-mix(in srgb, var(--c1) 15%, transparent);
      }
    }

    p {
      margin: 0;
    }
  }

  .info-icon {
    width: 24px;
    height: 24px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: color-mix(in srgb, var(--c1) 15%, transparent);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
    flex-shrink: 0;
  }

  // Code Block
  .code-block {
    background: color-mix(in srgb, var(--c1) 40%, transparent);
    border: 1px solid color-mix(in srgb, var(--c-text) 8%, transparent);
    overflow: hidden;
    margin: 1.5rem 0;
  }

  .code-header {
    padding: 0.6rem 1rem;
    background: color-mix(in srgb, var(--c1) 30%, transparent);
    border-bottom: 1px solid color-mix(in srgb, var(--c-text) 8%, transparent);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: color-mix(in srgb, var(--c-text) 35%, transparent);
    letter-spacing: 0.04em;
  }

  .code-content {
    padding: 1rem;
    font-family: 'JetBrains Mono', monospace;
    font-size: 1rem;
    color: var(--c-text);
    line-height: 1.8;
    margin: 0;
    white-space: pre-wrap;

    p {
      margin: 0.3rem 0;
      color: var(--c-text);
    }

    strong {
      color: var(--c1);
    }
  }

  // Widget List
  .widget-list {
    list-style: none;
    padding: 0;
    margin: 1rem 0;

    li {
      padding: 0.5rem 0;
      font-family: 'Rubik', sans-serif;
      font-size: 1rem;
      color: color-mix(in srgb, var(--c-text) 55%, transparent);

      code {
        background: color-mix(in srgb, var(--c1) 15%, transparent);
        padding: 0.2rem 0.5rem;
        color: var(--c1);
        margin-right: 0.5rem;
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
      }
    }
  }

  // Color Grid
  .color-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 1rem;
    margin: 1.5rem 0;
  }

  .color-item {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 1rem;
    background: color-mix(in srgb, var(--c-text) 3%, transparent);
    border: 1px solid color-mix(in srgb, var(--c-text) 8%, transparent);

    code {
      font-family: '8bitwonder', monospace;
      font-size: 1rem;
      color: var(--c1);
    }
  }

  .color-swatch {
    width: 100%;
    height: 40px;
    border: 1px solid color-mix(in srgb, var(--c-text) 10%, transparent);
  }

  .color-desc {
    font-family: 'Rubik', sans-serif;
    font-size: 1rem;
    color: color-mix(in srgb, var(--c-text) 40%, transparent);
  }

  // Media Display
  .media-display {
    display: flex;
    gap: 1.5rem;
    align-items: center;
    padding: 1.5rem;
    background: color-mix(in srgb, var(--c-text) 3%, transparent);
    border: 1px solid color-mix(in srgb, var(--c-text) 8%, transparent);
    margin: 1.5rem 0;
  }

  .media-thumb {
    width: 80px;
    height: 80px;
    object-fit: cover;
    border: 2px solid color-mix(in srgb, var(--c1) 30%, transparent);
  }

  .media-info {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 0.5rem 1rem;
    align-items: center;
  }

  .media-label {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: color-mix(in srgb, var(--c-text) 30%, transparent);
    letter-spacing: 0.08em;
  }

  .media-value {
    font-family: 'Rubik', sans-serif;
    font-size: 1rem;
    color: var(--c-text);
  }

  .no-media {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    padding: 3rem;
    background: color-mix(in srgb, var(--c-text) 2%, transparent);
    border: 1px dashed color-mix(in srgb, var(--c-text) 8%, transparent);
    margin: 1.5rem 0;

    p {
      margin: 0;
      color: color-mix(in srgb, var(--c-text) 30%, transparent);
      font-family: 'Rubik', sans-serif;
    }
  }

  .no-media-icon {
    font-size: 2rem;
    color: color-mix(in srgb, var(--c-text) 15%, transparent);
  }

  // Feature List
  .feature-list {
    list-style: none;
    padding: 0;
    margin: 1.5rem 0;
    display: flex;
    flex-direction: column;
    gap: 1rem;

    li {
      display: flex;
      gap: 1rem;
      padding: 1rem;
      background: color-mix(in srgb, var(--c-text) 3%, transparent);
      border: 1px solid color-mix(in srgb, var(--c-text) 8%, transparent);

      strong {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c-text);
        display: block;
        margin-bottom: 0.25rem;
        letter-spacing: 0.04em;
      }

      p {
        margin: 0;
        font-size: 1rem;
      }
    }
  }

  .feature-icon {
    width: 36px;
    height: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: color-mix(in srgb, var(--c1) 15%, transparent);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1);
    flex-shrink: 0;
  }

  // Footer
  .docs-footer {
    padding: 2rem 3rem;
    border-top: 1px solid color-mix(in srgb, var(--c-text) 6%, transparent);
    text-align: center;
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: color-mix(in srgb, var(--c-text) 20%, transparent);
    letter-spacing: 0.04em;
  }

  // Responsive
  @media (max-width: 900px) {
    .docs-layout {
      grid-template-columns: 1fr;
    }

    .docs-sidebar {
      display: none;
    }

    .docs-content {
      padding: 2rem 1.5rem;
    }
  }
</style>
