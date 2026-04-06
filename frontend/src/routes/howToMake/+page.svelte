<script>
  import { title, artist, thumbnail, ShowTrack } from '$lib/stores/stores.js';

  // Navigation sections
  const sections = [
    { id: 'getting-started', title: 'Getting Started', titleRu: 'Начало работы' },
    { id: 'obs-setup', title: 'OBS Setup', titleRu: 'Настройка OBS' },
    { id: 'colors', title: 'Dynamic Colors', titleRu: 'Динамические цвета' },
    { id: 'media-info', title: 'Media Info', titleRu: 'Медиа данные' },
    { id: 'customization', title: 'Customization', titleRu: 'Кастомизация' },
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
            UnikPlayer - виджет для OBS. Автоматически определяет музыку из любого приложения:
            Spotify, YouTube Music, VK, браузеры и другие.
          </p>

          <div class="steps-list">
            <div class="step">
            </div>
            <div class="step">
              <span class="step-num">1</span>
              <div class="step-content">
                <h4>Выберите виджет</h4>
                <p>На главной странице выберите понравившийся стиль виджета и нажмите SELECT.</p>
              </div>
            </div>
            <div class="step">
              <span class="step-num">2</span>
              <div class="step-content">
                <h4>Добавьте в OBS</h4>
                <p>Ссылка скопирована в буфер обмена. Создайте Browser Source в OBS и вставьте ссылку.</p>
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
            <p>UnikPlayer работает через локальный сервер на порту 27272. OBS получает данные через Browser Source.</p>
          </div>

          <h3>Настройка Browser Source</h3>
          <div class="code-block">
            <div class="code-header">
              <span>Browser Source Settings</span>
            </div>
            <div class="code-content">
              <p><strong>URL:</strong> http://192.168.1.132:27272/player?Generic</p>
              <p><strong>Width:</strong> 500</p>
              <p><strong>Height:</strong> 200</p>
              <p><strong>Custom CSS:</strong> body &#123; background: transparent; &#125;</p>
            </div>
          </div>

          <h3>Доступные виджеты</h3>
          <ul class="widget-list">
            <li><code>?Generic</code> - Классический горизонтальный</li>
            <li><code>?BigHead</code> - Большая обложка слева</li>
            <li><code>?Separate</code> - Раздельные блоки</li>
            <li><code>?BackPicture</code> - Обложка на фоне</li>
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
            Цвета автоматически извлекаются из обложки альбома с помощью библиотеки node-vibrant.
            Используйте CSS переменные для динамической стилизации.
          </p>

          <h3>CSS Variables</h3>
          <div class="color-grid">
            <div class="color-item">
              <div class="color-swatch" style="background: var(--vibrant, #B87333)"></div>
              <code>--vibrant</code>
              <span class="color-desc">Яркий акцент</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--muted, #8B6914)"></div>
              <code>--muted</code>
              <span class="color-desc">Приглушённый</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--lightVibrant, #F5DEB3)"></div>
              <code>--lightVibrant</code>
              <span class="color-desc">Светлый яркий</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--lightMuted, #B87333)"></div>
              <code>--lightMuted</code>
              <span class="color-desc">Светлый мягкий</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--darkVibrant, #5C4033)"></div>
              <code>--darkVibrant</code>
              <span class="color-desc">Тёмный яркий</span>
            </div>
            <div class="color-item">
              <div class="color-swatch" style="background: var(--darkMuted, rgba(20, 15, 10, 0.9))"></div>
              <code>--darkMuted</code>
              <span class="color-desc">Тёмный мягкий</span>
            </div>
          </div>

          <div class="code-block">
            <div class="code-header">
              <span>Пример использования</span>
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
          <p>Текущие данные о воспроизводимом треке:</p>

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
              <span class="no-media-icon">♪</span>
              <p>Включите музыку чтобы увидеть данные</p>
            </div>
          {/if}

          <h3>WebSocket API</h3>
          <div class="code-block">
            <div class="code-header">
              <span>Подключение</span>
            </div>
            <pre class="code-content">const ws = new WebSocket('ws://192.168.1.132:62727');

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
            Используйте встроенный редактор для кастомизации виджетов.
            Нажмите EDIT на карточке виджета чтобы открыть редактор.
          </p>

          <h3>Возможности редактора</h3>
          <ul class="feature-list">
            <li>
              <span class="feature-icon">A</span>
              <div>
                <strong>Typography</strong>
                <p>Выбор шрифта из системных или Google Fonts</p>
              </div>
            </li>
            <li>
              <span class="feature-icon">◐</span>
              <div>
                <strong>Color Mode</strong>
                <p>Dynamic (из обложки) или Static (ваш выбор)</p>
              </div>
            </li>
            <li>
              <span class="feature-icon">&lt;/&gt;</span>
              <div>
                <strong>CSS Editor</strong>
                <p>Полный контроль над стилями виджета</p>
              </div>
            </li>
          </ul>

          <div class="info-box warning">
            <span class="info-icon">!</span>
            <p>Изменения применяются в реальном времени. Нажмите CONFIRM чтобы сохранить.</p>
          </div>
        </div>
      </section>

    </main>
  </div>

  <!-- Footer -->
  <footer class="docs-footer">
    <span>UnikPlayer Documentation • v0.7</span>
  </footer>
</div>

<style lang="scss">
  .docs-page {
    min-height: 100vh;
    position: relative;
    color: var(--c2);
    background: var(--c1);
  }

  // Background
  .bg-gradient {
    position: fixed;
    inset: 0;
    background:
      radial-gradient(ellipse at 20% 20%, rgba(184, 115, 51, 0.06) 0%, transparent 50%),
      radial-gradient(ellipse at 80% 80%, rgba(184, 115, 51, 0.03) 0%, transparent 50%),
      var(--c1);
    z-index: -2;
  }

  .bg-grid {
    position: fixed;
    inset: 0;
    background-image:
      linear-gradient(rgba(255, 255, 255, 0.015) 1px, transparent 1px),
      linear-gradient(90deg, rgba(255, 255, 255, 0.015) 1px, transparent 1px);
    background-size: 50px 50px;
    z-index: -1;
  }

  // Header
  .docs-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1.5rem 3rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    position: sticky;
    top: 0;
    background: rgba(10, 10, 10, 0.95);
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
    color: #B87333;
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
  }

  .logo-text {
    font-family: '8bitwonder', monospace;
    font-size: 0.7rem;
    color: var(--c2);
    letter-spacing: 0.06em;
  }

  .header-tag {
    font-family: '8bitwonder', monospace;
    font-size: 0.5rem;
    color: #B87333;
    letter-spacing: 0.08em;
    padding: 0.4rem 1rem;
    border: 1px solid rgba(184, 115, 51, 0.3);
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
    border-right: 1px solid rgba(255, 255, 255, 0.06);
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
    color: rgba(255, 255, 255, 0.4);
    font-family: '8bitwonder', monospace;
    font-size: 0.5rem;
    letter-spacing: 0.04em;
    transition: all 0.2s;

    &:hover {
      color: var(--c2);
      background: rgba(255, 255, 255, 0.04);
    }

    &.active {
      color: #B87333;
      background: rgba(184, 115, 51, 0.1);

      .nav-marker {
        background: #B87333;
      }
    }
  }

  .nav-marker {
    width: 4px;
    height: 4px;
    background: rgba(255, 255, 255, 0.2);
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
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);

    h2 {
      font-family: '8bitwonder', monospace;
      font-size: 0.85rem;
      margin: 0;
      color: var(--c2);
      letter-spacing: 0.06em;
    }
  }

  .section-number {
    font-family: '8bitwonder', monospace;
    font-size: 0.55rem;
    color: #B87333;
  }

  .section-body {
    h3 {
      font-family: '8bitwonder', monospace;
      font-size: 0.65rem;
      color: var(--c2);
      margin: 2rem 0 1rem;
      letter-spacing: 0.06em;
    }

    p {
      font-family: 'Rubik', sans-serif;
      font-size: 0.85rem;
      color: rgba(255, 255, 255, 0.55);
      line-height: 1.8;
      margin: 0 0 1rem;
    }
  }

  .intro-text {
    font-size: 0.95rem !important;
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
    background: rgba(184, 115, 51, 0.15);
    border: 1px solid rgba(184, 115, 51, 0.35);
    font-family: '8bitwonder', monospace;
    font-size: 0.65rem;
    color: #B87333;
    flex-shrink: 0;
  }

  .step-content {
    h4 {
      font-family: '8bitwonder', monospace;
      font-size: 0.6rem;
      color: var(--c2);
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
    background: rgba(184, 115, 51, 0.08);
    border: 1px solid rgba(184, 115, 51, 0.25);
    margin: 1.5rem 0;

    &.warning {
      background: rgba(239, 191, 51, 0.08);
      border-color: rgba(239, 191, 51, 0.25);

      .info-icon {
        color: #efbf33;
        background: rgba(239, 191, 51, 0.15);
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
    background: rgba(184, 115, 51, 0.15);
    font-family: '8bitwonder', monospace;
    font-size: 0.5rem;
    color: #B87333;
    flex-shrink: 0;
  }

  // Code Block
  .code-block {
    background: rgba(0, 0, 0, 0.4);
    border: 1px solid rgba(255, 255, 255, 0.08);
    overflow: hidden;
    margin: 1.5rem 0;
  }

  .code-header {
    padding: 0.6rem 1rem;
    background: rgba(0, 0, 0, 0.3);
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    font-family: '8bitwonder', monospace;
    font-size: 0.45rem;
    color: rgba(255, 255, 255, 0.35);
    letter-spacing: 0.04em;
  }

  .code-content {
    padding: 1rem;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    color: #E8D4B8;
    line-height: 1.8;
    margin: 0;
    white-space: pre-wrap;

    p {
      margin: 0.3rem 0;
      color: #E8D4B8;
    }

    strong {
      color: #B87333;
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
      font-size: 0.85rem;
      color: rgba(255, 255, 255, 0.55);

      code {
        background: rgba(184, 115, 51, 0.15);
        padding: 0.2rem 0.5rem;
        color: #B87333;
        margin-right: 0.5rem;
        font-family: '8bitwonder', monospace;
        font-size: 0.5rem;
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
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.08);

    code {
      font-family: '8bitwonder', monospace;
      font-size: 0.45rem;
      color: #B87333;
    }
  }

  .color-swatch {
    width: 100%;
    height: 40px;
    border: 1px solid rgba(255, 255, 255, 0.1);
  }

  .color-desc {
    font-family: 'Rubik', sans-serif;
    font-size: 0.7rem;
    color: rgba(255, 255, 255, 0.4);
  }

  // Media Display
  .media-display {
    display: flex;
    gap: 1.5rem;
    align-items: center;
    padding: 1.5rem;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.08);
    margin: 1.5rem 0;
  }

  .media-thumb {
    width: 80px;
    height: 80px;
    object-fit: cover;
    border: 2px solid rgba(184, 115, 51, 0.3);
  }

  .media-info {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 0.5rem 1rem;
    align-items: center;
  }

  .media-label {
    font-family: '8bitwonder', monospace;
    font-size: 0.45rem;
    color: rgba(255, 255, 255, 0.3);
    letter-spacing: 0.08em;
  }

  .media-value {
    font-family: 'Rubik', sans-serif;
    font-size: 0.9rem;
    color: var(--c2);
  }

  .no-media {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    padding: 3rem;
    background: rgba(255, 255, 255, 0.02);
    border: 1px dashed rgba(255, 255, 255, 0.08);
    margin: 1.5rem 0;

    p {
      margin: 0;
      color: rgba(255, 255, 255, 0.3);
      font-family: 'Rubik', sans-serif;
    }
  }

  .no-media-icon {
    font-size: 2rem;
    color: rgba(255, 255, 255, 0.15);
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
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);

      strong {
        font-family: '8bitwonder', monospace;
        font-size: 0.55rem;
        color: var(--c2);
        display: block;
        margin-bottom: 0.25rem;
        letter-spacing: 0.04em;
      }

      p {
        margin: 0;
        font-size: 0.8rem;
      }
    }
  }

  .feature-icon {
    width: 36px;
    height: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(184, 115, 51, 0.15);
    font-family: '8bitwonder', monospace;
    font-size: 0.55rem;
    color: #B87333;
    flex-shrink: 0;
  }

  // Footer
  .docs-footer {
    padding: 2rem 3rem;
    border-top: 1px solid rgba(255, 255, 255, 0.06);
    text-align: center;
    font-family: '8bitwonder', monospace;
    font-size: 0.45rem;
    color: rgba(255, 255, 255, 0.2);
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
