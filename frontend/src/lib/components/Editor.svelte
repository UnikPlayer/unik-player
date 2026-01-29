<script>
  import { fly, fade } from 'svelte/transition';
  import {
    editorOpen,
    editingPlayer,
    editorCSS,
    playerStyles,
    colorMode,
    staticColor,
    selectedFont
  } from '$lib/stores/stores.js';
  import { getPickedPlayer } from '$lib/getPlayers.js';
  import ColorPicker from './ColorPicker.svelte';
  import FontPicker from './FontPicker.svelte';

  // Backend API base URL - for dev mode
  const isBrowser = typeof window !== 'undefined';
  const API_BASE = isBrowser && window.location.port === '5173'
    ? 'http://localhost:27272'
    : '';

  let playerComponent = null;
  let playerName = '';
  let cssText = '';
  let originalCSS = '';
  let localColorMode = 'dynamic';
  let localStaticColor = '#B87333';
  let localFont = 'Rubik';
  let lastAppliedFont = 'Rubik';
  let localPreviewScale = 0.5;
  let cssLoaded = false;  // Flag to prevent CSS reload on every change

  // Update CSS with font-family when font changes
  function updateFontInCSS(fontName) {
    const fontRule = `.title > *, .artist > * {\n  font-family: "${fontName}", sans-serif;\n}`;
    const fontRegex = /\.title\s*>\s*\*,\s*\.artist\s*>\s*\*\s*\{\s*font-family:\s*[^}]+\}/;

    if (fontRegex.test(cssText)) {
      // Replace existing font rule
      cssText = cssText.replace(fontRegex, fontRule);
    } else {
      // Add font rule at the beginning after comments
      const commentEndMatch = cssText.match(/^(\/\*[\s\S]*?\*\/\s*)+/);
      if (commentEndMatch) {
        const comments = commentEndMatch[0];
        const rest = cssText.slice(comments.length);
        cssText = comments + fontRule + '\n\n' + rest;
      } else {
        cssText = fontRule + '\n\n' + cssText;
      }
    }
  }

  // CSS templates for each player type (exact copy from component styles)
  const playerCSS = {
    Generic: `/* === GENERIC PLAYER === */
/* Colors: var(--vibrant), var(--lightVibrant),
   var(--darkVibrant), var(--muted),
   var(--lightMuted), var(--darkMuted) */

.title > *, .artist > * {
  font-family: "Rubik", sans-serif;
}

.mainDiv {
  display: flex;
  flex-direction: row;
  align-items: center;
  gap: 0;
}

.picDiv {
  overflow: hidden;
  z-index: 2;
}

.pic {
  width: 8rem;
  height: 8rem;
  object-fit: cover;
  border-radius: 1rem;
  border: 0.2rem solid var(--lightMuted);
}

.textDiv {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  width: 20rem;
  height: 8rem;
  margin-left: -1rem;
  border-radius: 0 1rem 1rem 0;
  border: 0.2rem solid var(--vibrant);
  border-left: none;
  background-color: var(--darkMuted);
  z-index: 1;
}

.title, .artist {
  display: flex;
  justify-content: center;
  align-items: center;
  margin: 0;
  line-height: 1.2;
  color: var(--lightVibrant);
  white-space: nowrap;
  overflow: hidden;
}

.title {
  margin-bottom: 0.3rem;
}

.title > * {
  font-size: 1.8rem;
}

.artist > * {
  font-size: 1.7rem;
}`,

    BackPicture: `/* === BACKPICTURE PLAYER === */
/* Colors: var(--vibrant), var(--lightVibrant),
   var(--darkVibrant), var(--muted),
   var(--lightMuted), var(--darkMuted) */

.title > *, .artist > * {
  font-family: "Rubik", sans-serif;
}

.mainDiv {
  position: relative;
  display: flex;
  align-items: stretch;
  width: 18rem;
  height: 7.5rem;
}

.mainDivGlow {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 95%;
  height: 95%;
  border-radius: 1rem;
  box-shadow: 0 0 35px 5px var(--lightMuted);
  pointer-events: none;
  z-index: 0;
}

.textDiv {
  position: relative;
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
  background-size: cover;
  background-position: center;
  border-radius: 1rem;
  border: 0.2rem solid var(--vibrant);
  z-index: 1;
}

.blurDiv {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  backdrop-filter: blur(8px);
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 1;
}

.title, .artist {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0;
  line-height: 1.2;
  color: var(--lightVibrant);
  white-space: nowrap;
  overflow: hidden;
  z-index: 2;
}

.title {
  flex: 3;
  padding: 0.5rem 1rem;
}

.title > * {
  font-size: 1.8rem;
}

.artist {
  flex: 2;
  padding: 0.8rem 1rem;
}

.artist > * {
  font-size: 1.6rem;
}`,

    BigHead: `/* === BIGHEAD PLAYER === */
/* Colors: var(--vibrant), var(--lightVibrant),
   var(--darkVibrant), var(--muted),
   var(--lightMuted), var(--darkMuted) */

.title > *, .artist > * {
  font-family: "Rubik", sans-serif;
}

.mainDiv {
  display: flex;
  align-items: center;
  gap: 0;
}

.picDiv {
  width: 9.5rem;
  height: 9.5rem;
  flex: 0 0 9.5rem;
  overflow: hidden;
  border: 0.2rem solid var(--lightMuted);
  border-radius: 1rem;
  z-index: 2;
}

.pic {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.textDiv {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  width: 20rem;
  height: 6.5rem;
  padding: 0.8rem 1rem;
  background: var(--darkMuted);
  border-radius: 0 1rem 1rem 0;
  border: 0.2rem solid var(--vibrant);
  border-left: none;
  margin-left: -0.2rem;
  z-index: 1;
}

.title, .artist {
  display: flex;
  justify-content: center;
  align-items: center;
  margin: 0;
  line-height: 1.2;
  color: var(--lightVibrant);
  white-space: nowrap;
  overflow: hidden;
}

.title {
  margin-bottom: 0.3rem;
}

.title > * {
  font-size: 1.8rem;
}

.artist > * {
  font-size: 1.3rem;
}`,

    Separate: `/* === SEPARATE PLAYER === */
/* Colors: var(--vibrant), var(--lightVibrant),
   var(--darkVibrant), var(--muted),
   var(--lightMuted), var(--darkMuted) */

.title > *, .artist > * {
  font-family: "Rubik", sans-serif;
}

.mainDiv {
  display: flex;
  align-items: stretch;
  gap: 0.6rem;
}

.pic {
  display: block;
  border-radius: 1rem;
  border: 0.2rem solid var(--lightMuted);
  width: 10rem;
  height: 10rem;
  object-fit: cover;
  flex: 0 0 10rem;
}

.textDiv {
  display: flex;
  flex-direction: column;
  flex: 1;
  width: 20rem;
  gap: 0.6rem;
}

.titleDiv {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  border: 0.2rem solid var(--vibrant);
  background-color: var(--darkMuted);
  flex: 4;
  padding: 0.8rem 1rem;
}

.artistDiv {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  border: 0.2rem solid var(--vibrant);
  background-color: var(--darkMuted);
  flex: 2;
  padding: 0.6rem 1rem;
}

.title, .artist {
  display: flex;
  justify-content: center;
  align-items: center;
  white-space: nowrap;
  overflow: hidden;
  color: var(--lightVibrant);
  margin: 0;
  line-height: 1.2;
}

.title > * {
  font-size: 1.8rem;
}

.artist > * {
  font-size: 1.3rem;
}`
  };

  // Get default CSS for current player
  function getDefaultCSS(pName) {
    return playerCSS[pName] || playerCSS.Generic;
  }

  let pigAudio;

  function playPigSound() {
    if (!pigAudio) {
      pigAudio = new Audio('/pig.mp3');
      pigAudio.volume = 0.4;
    }
    pigAudio.currentTime = 0;
    pigAudio.play();
  }

  // Transform CSS for preview - scope to .preview-frame .player-{name}
  function transformCSS(rawCSS, pName) {
    if (!rawCSS || !rawCSS.trim()) return '';

    const scope = `.preview-frame .player-${pName}`;

    // Remove comments
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
        if (s.includes('.preview-frame')) return s;
        if (s === '*') return `${scope} *`;
        return `${scope} ${s}`;
      }).filter(s => s);

      if (selectors.length === 0) continue;

      // Add !important
      body = body.replace(/:\s*([^;!]+);/g, ': $1 !important;');
      if (body && !body.endsWith(';') && !body.endsWith('!important')) {
        body = body.replace(/:\s*([^;!{}]+)$/, ': $1 !important');
      }

      transformed.push(`${selectors.join(', ')} { ${body} }`);
    }

    return transformed.join('\n');
  }

  // Inject CSS into document head for live preview
  function injectPreviewCSS(css) {
    const id = 'editor-preview-css';
    let existing = document.getElementById(id);
    if (existing) existing.remove();

    if (!css || !css.trim()) return;

    const style = document.createElement('style');
    style.id = id;
    style.textContent = css;
    document.head.appendChild(style);
  }

  // Remove preview CSS when editor closes
  function cleanupPreviewCSS() {
    const el = document.getElementById('editor-preview-css');
    if (el) el.remove();
  }

  // Live preview: transform and inject CSS when cssText changes
  $: if (cssLoaded && playerName && cssText) {
    const transformed = transformCSS(cssText, playerName);
    injectPreviewCSS(transformed);
  }

  // Load CSS from backend
  async function loadCSSFromBackend(player) {
    try {
      const res = await fetch(`${API_BASE}/api/css/${player}`);
      if (res.ok) {
        const css = await res.text();
        if (css && css.trim()) {
          return css;
        }
      }
    } catch (err) {
      console.log('[Editor] Failed to load CSS from backend:', err);
    }
    return null;
  }

  // Save CSS to backend
  async function saveCSSToBackend(player, css) {
    try {
      const res = await fetch(`${API_BASE}/api/css/${player}`, {
        method: 'POST',
        headers: { 'Content-Type': 'text/css' },
        body: css
      });
      if (res.ok) {
        console.log(`[Editor] CSS saved for ${player}`);
        return true;
      }
    } catch (err) {
      console.log('[Editor] Failed to save CSS:', err);
    }
    return false;
  }

  // Load editor when player changes - only once per open
  $: if ($editingPlayer && !cssLoaded) {
    const players = getPickedPlayer($editingPlayer);
    if (players.length > 0) {
      playerComponent = players[0].component;
      playerName = players[0].name;
    }
    // Load saved settings from store
    localColorMode = $playerStyles[$editingPlayer]?.colorMode || 'dynamic';
    localStaticColor = $playerStyles[$editingPlayer]?.staticColor || '#B87333';
    localFont = $playerStyles[$editingPlayer]?.font || 'Rubik';
    localPreviewScale = $playerStyles[$editingPlayer]?.previewScale || 0.5;
    lastAppliedFont = localFont;

    // Load CSS from backend file (only once)
    loadCSSFromBackend($editingPlayer).then(css => {
      cssText = css || $playerStyles[$editingPlayer]?.css || getDefaultCSS($editingPlayer);
      originalCSS = cssText;
      cssLoaded = true;  // Prevent reload on every change
    });
  }

  // Watch for font changes and update CSS
  $: if (localFont && localFont !== lastAppliedFont) {
    updateFontInCSS(localFont);
    lastAppliedFont = localFont;
  }

  async function handleConfirm() {
    // Save CSS to backend file
    await saveCSSToBackend($editingPlayer, cssText);

    // Save settings to backend /api/styles (JSON)
    try {
      const allStyles = await fetch(`${API_BASE}/api/styles`).then(r => r.json()).catch(() => ({}));
      allStyles[$editingPlayer] = {
        colorMode: localColorMode,
        staticColor: localStaticColor,
        font: localFont,
        previewScale: localPreviewScale
      };
      await fetch(`${API_BASE}/api/styles`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(allStyles)
      });
      console.log('[Editor] Settings saved to backend');
    } catch (err) {
      console.log('[Editor] Failed to save settings to backend:', err);
    }

    // Update local store
    playerStyles.update(styles => ({
      ...styles,
      [$editingPlayer]: {
        css: cssText,
        colorMode: localColorMode,
        staticColor: localStaticColor,
        font: localFont,
        previewScale: localPreviewScale
      }
    }));
    colorMode.set(localColorMode);
    staticColor.set(localStaticColor);
    selectedFont.set(localFont);

    // Dispatch event to refresh CSS on main page
    window.dispatchEvent(new CustomEvent('unik-css-refresh'));

    closeEditor();
  }

  function handleCancel() {
    closeEditor();
  }

  function handleReset() {
    cssText = getDefaultCSS(playerName);
    localColorMode = 'dynamic';
    localStaticColor = '#B87333';
    localFont = 'Rubik';
    localPreviewScale = 0.5;
  }

  async function showStylesPath() {
    try {
      // First save current CSS
      await saveCSSToBackend($editingPlayer, cssText);

      // Open the CSS file in default editor
      const res = await fetch(`${API_BASE}/api/open-css/${$editingPlayer}`);
      if (!res.ok) {
        alert(`CSS file: %LocalAppData%\\UnikPlayer\\css\\${$editingPlayer}.css`);
      }
    } catch (err) {
      alert(`CSS file: %LocalAppData%\\UnikPlayer\\css\\${$editingPlayer}.css`);
    }
  }

  function closeEditor() {
    cleanupPreviewCSS();  // Remove live preview styles
    cssLoaded = false;  // Reset flag for next editor open
    editorOpen.set(false);
    editingPlayer.set(null);
  }

  // Generate color variables based on static color
  function generateColorVars(hex) {
    const base = hex || '#B87333';
    return {
      vibrant: base,
      lightVibrant: lightenColor(base, 30),
      darkVibrant: darkenColor(base, 30),
      muted: desaturateColor(base, 30),
      lightMuted: lightenColor(base, 20),
      darkMuted: darkenColor(base, 40)
    };
  }

  function lightenColor(hex, percent) {
    const num = parseInt(hex.slice(1), 16);
    const r = Math.min(255, (num >> 16) + Math.round(255 * percent / 100));
    const g = Math.min(255, ((num >> 8) & 0x00FF) + Math.round(255 * percent / 100));
    const b = Math.min(255, (num & 0x0000FF) + Math.round(255 * percent / 100));
    return `#${(1 << 24 | r << 16 | g << 8 | b).toString(16).slice(1)}`;
  }

  function darkenColor(hex, percent) {
    const num = parseInt(hex.slice(1), 16);
    const r = Math.max(0, (num >> 16) - Math.round(255 * percent / 100));
    const g = Math.max(0, ((num >> 8) & 0x00FF) - Math.round(255 * percent / 100));
    const b = Math.max(0, (num & 0x0000FF) - Math.round(255 * percent / 100));
    return `#${(1 << 24 | r << 16 | g << 8 | b).toString(16).slice(1)}`;
  }

  function desaturateColor(hex, percent) {
    const num = parseInt(hex.slice(1), 16);
    const r = (num >> 16);
    const g = ((num >> 8) & 0x00FF);
    const b = (num & 0x0000FF);
    const gray = (r + g + b) / 3;
    const nr = Math.round(r + (gray - r) * percent / 100);
    const ng = Math.round(g + (gray - g) * percent / 100);
    const nb = Math.round(b + (gray - b) * percent / 100);
    return `#${(1 << 24 | nr << 16 | ng << 8 | nb).toString(16).slice(1)}`;
  }

  // Generate inline style for preview
  // For dynamic mode: don't set colors, inherit from :root (set by Vibrant.js)
  // For static mode: generate colors from selected color
  $: previewColors = localColorMode === 'static' ? generateColorVars(localStaticColor) : null;

  $: previewStyle = previewColors ? `
    --vibrant: ${previewColors.vibrant};
    --lightVibrant: ${previewColors.lightVibrant};
    --darkVibrant: ${previewColors.darkVibrant};
    --muted: ${previewColors.muted};
    --lightMuted: ${previewColors.lightMuted};
    --darkMuted: ${previewColors.darkMuted};
    font-family: "${localFont}", sans-serif;
  ` : `font-family: "${localFont}", sans-serif;`;

  // Note: Custom CSS is NOT applied in preview to avoid conflicts with component styles
  // Preview shows the player "as-is" with only font and color variables applied
  // Custom CSS is applied only on the actual player page (player/+page.svelte)
</script>

{#if $editorOpen}
  <div class="editor-overlay" transition:fade={{ duration: 200 }}>
    <div class="editor-container" transition:fly={{ y: 50, duration: 300 }}>

      <!-- Header -->
      <header class="editor-header">
        <div class="header-left">
          <span class="header-icon">[ ]</span>
          <span class="header-title">UNIKPLAYER</span>
        </div>
        <span class="header-subtitle">PIXEL-GLASS CUSTOMIZER // V.2.0</span>
        <div class="header-right">
          <span class="header-tab">LIB</span>
          <span class="header-tab active">EDIT</span>
          <span class="header-tab">AST</span>
          <div class="status-indicator">
            <span>SYS_RUN: 0xFF1A</span>
            <span class="status-dot"></span>
          </div>
        </div>
      </header>

      <!-- Main Content -->
      <main class="editor-main">

        <!-- Left Panel: Preview -->
        <section class="preview-panel">
          <div class="panel-header">
            <span>MONITOR_01 // PREVIEW</span>
            <span class="panel-badge">60 FPS // HD</span>
          </div>
          <div class="preview-area">
            <div class="preview-frame" style={previewStyle}>
              {#if playerComponent}
                <svelte:component this={playerComponent} preview={false} showAlways={true} />
              {/if}
            </div>
          </div>
        </section>

        <!-- Right Panel: Controls -->
        <section class="controls-panel">

          <!-- Typography -->
          <div class="control-group">
            <div class="control-header">
              <span class="control-icon">A</span>
              <span>TYPOGRAPHY</span>
            </div>
            <FontPicker bind:value={localFont} />
          </div>

          <!-- Color Mode -->
          <div class="control-group">
            <div class="control-header">
              <span class="control-icon"></span>
              <span>COLOR_SYNC</span>
              <button class="file-info-btn" on:click={showStylesPath} title="Open styles file location">
                [EDIT FILE]
              </button>
            </div>
            <ColorPicker
              bind:mode={localColorMode}
              bind:color={localStaticColor}
            />
          </div>

          <!-- Preview Scale -->
          <div class="control-group">
            <div class="control-header">
              <span class="control-icon">⊡</span>
              <span>MENU_SIZE</span>
              <span class="scale-value">{Math.round(localPreviewScale * 100)}%</span>
            </div>
            <div class="scale-slider">
              <input
                type="range"
                min="0.2"
                max="1"
                step="0.05"
                bind:value={localPreviewScale}
              />
            </div>
          </div>

          <!-- CSS Editor -->
          <div class="control-group css-editor">
            <div class="css-header">
              <div class="window-dots">
                <span class="dot red"></span>
                <span class="dot yellow"></span>
                <span class="dot green"></span>
              </div>
              <span class="css-filename">CONFIG.CSS</span>
              <button class="expand-btn">[ ]</button>
            </div>
            <div class="css-content">
              <div class="line-numbers">
                {#each cssText.split('\n') as _, i}
                  <span>{String(i + 1).padStart(2, '0')}</span>
                {/each}
              </div>
              <textarea
                class="css-textarea"
                bind:value={cssText}
                spellcheck="false"
              ></textarea>
            </div>
          </div>

        </section>
      </main>

      <!-- Footer -->
      <footer class="editor-footer">
        <button class="footer-btn pig-btn" on:click={playPigSound}>
          🐷
        </button>

        <div class="footer-actions">
          <button class="footer-btn confirm" on:click={handleConfirm}>
            CONFIRM
          </button>
          <button class="footer-btn cancel" on:click={handleCancel}>
            CANCEL
          </button>
        </div>

        <button class="footer-btn reset" on:click={handleReset}>
          Reset
        </button>
      </footer>

    </div>
  </div>
{/if}

<style lang="scss">
  .editor-overlay {
    position: fixed;
    inset: 0;
    z-index: 1000;
    background: rgba(5, 5, 10, 0.95);
    backdrop-filter: blur(20px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 2rem;
  }

  .editor-container {
    width: 100%;
    max-width: 1400px;
    height: 90vh;
    max-height: 900px;
    background: linear-gradient(180deg, rgba(15, 15, 20, 0.98) 0%, rgba(10, 10, 15, 0.98) 100%);
    border: 1px solid rgba(184, 115, 51, 0.3);
    border-radius: 4px;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    box-shadow:
      0 0 100px rgba(184, 115, 51, 0.1),
      inset 0 1px 0 rgba(255, 255, 255, 0.05);
  }

  // Header
  .editor-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1rem 1.5rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    background: rgba(0, 0, 0, 0.3);
  }

  .header-left {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .header-icon {
    color: #B87333;
    font-family: monospace;
    font-size: 1.5rem;
  }

  .header-title {
    font-family: 'JetBrains Mono', monospace;
    font-size: 1.1rem;
    font-weight: 700;
    color: white;
    letter-spacing: 0.05em;
  }

  .header-subtitle {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.75rem;
    color: #B87333;
    letter-spacing: 0.1em;
  }

  .header-right {
    display: flex;
    align-items: center;
    gap: 1.5rem;
  }

  .header-tab {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    color: rgba(255, 255, 255, 0.5);
    cursor: pointer;
    transition: color 0.2s;

    &:hover, &.active {
      color: white;
    }

    &.active {
      text-decoration: underline;
      text-underline-offset: 4px;
    }
  }

  .status-indicator {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.4rem 0.8rem;
    border: 1px solid rgba(184, 115, 51, 0.5);
    border-radius: 4px;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    color: #B87333;
  }

  .status-dot {
    width: 6px;
    height: 6px;
    background: #B87333;
    border-radius: 50%;
    animation: pulse 2s infinite;
  }

  @keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.5; }
  }

  // Main content
  .editor-main {
    flex: 1;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1.5rem;
    padding: 1.5rem;
    overflow: hidden;
  }

  // Preview Panel
  .preview-panel {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  .panel-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.75rem;
    color: rgba(255, 255, 255, 0.5);
    letter-spacing: 0.05em;
  }

  .panel-badge {
    color: #B87333;
  }

  .preview-area {
    flex: 1;
    background:
      linear-gradient(180deg, rgba(20, 20, 25, 0.5) 0%, rgba(10, 10, 15, 0.8) 100%),
      repeating-linear-gradient(
        0deg,
        transparent,
        transparent 2px,
        rgba(255, 255, 255, 0.01) 2px,
        rgba(255, 255, 255, 0.01) 4px
      );
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 4px;
    padding: 2rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2rem;
  }

  .preview-frame {
    transform: scale(1);
    /* Colors inherited from :root (Vibrant.js) or set via inline style (static mode) */
  }

  // Controls Panel
  .controls-panel {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    overflow-y: auto;
  }

  .control-group {
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: 4px;
    padding: 1rem;
  }

  .control-header {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    margin-bottom: 0.75rem;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.75rem;
    color: rgba(255, 255, 255, 0.6);
    letter-spacing: 0.1em;
  }

  .control-icon {
    width: 20px;
    height: 20px;
    background: rgba(184, 115, 51, 0.2);
    border-radius: 2px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.7rem;
    color: #B87333;
  }

  .scale-value {
    margin-left: auto;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.7rem;
    color: #B87333;
  }

  .scale-slider {
    padding: 0.5rem 0;

    input[type="range"] {
      width: 100%;
      height: 4px;
      appearance: none;
      background: rgba(255, 255, 255, 0.1);
      border-radius: 2px;
      cursor: pointer;

      &::-webkit-slider-thumb {
        appearance: none;
        width: 14px;
        height: 14px;
        background: #B87333;
        border-radius: 2px;
        cursor: pointer;
        transition: all 0.2s;

        &:hover {
          background: #D4944A;
          transform: scale(1.1);
        }
      }

      &::-moz-range-thumb {
        width: 14px;
        height: 14px;
        background: #B87333;
        border: none;
        border-radius: 2px;
        cursor: pointer;
      }
    }
  }

  .file-info-btn {
    margin-left: auto;
    background: none;
    border: 1px solid rgba(184, 115, 51, 0.3);
    border-radius: 2px;
    padding: 0.2rem 0.5rem;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.6rem;
    color: #B87333;
    cursor: pointer;
    transition: all 0.2s;

    &:hover {
      background: rgba(184, 115, 51, 0.15);
      border-color: #B87333;
    }
  }

  // CSS Editor
  .css-editor {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 300px;
  }

  .css-header {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.6rem 0.8rem;
    background: rgba(0, 0, 0, 0.4);
    border-radius: 4px 4px 0 0;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  }

  .window-dots {
    display: flex;
    gap: 6px;
  }

  .dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;

    &.red { background: #ff5f57; }
    &.yellow { background: #febc2e; }
    &.green { background: #28c840; }
  }

  .css-filename {
    flex: 1;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.75rem;
    color: rgba(255, 255, 255, 0.6);
    letter-spacing: 0.05em;
  }

  .expand-btn {
    background: none;
    border: none;
    color: rgba(255, 255, 255, 0.4);
    cursor: pointer;
    font-family: monospace;

    &:hover {
      color: white;
    }
  }

  .css-content {
    flex: 1;
    display: flex;
    background: rgba(0, 0, 0, 0.6);
    border-radius: 0 0 4px 4px;
    overflow: hidden;
    backdrop-filter: blur(10px);
  }

  .line-numbers {
    padding: 1rem 0.75rem;
    background: rgba(0, 0, 0, 0.3);
    border-right: 1px solid rgba(255, 255, 255, 0.1);
    display: flex;
    flex-direction: column;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    line-height: 1.6;
    color: rgba(255, 255, 255, 0.3);
    user-select: none;
  }

  .css-textarea {
    flex: 1;
    padding: 1rem;
    background: transparent;
    border: none;
    resize: none;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    line-height: 1.6;
    color: #E8D4B8;
    outline: none;

    &::selection {
      background: rgba(184, 115, 51, 0.3);
    }
  }

  // Footer
  .editor-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1rem 1.5rem;
    border-top: 1px solid rgba(255, 255, 255, 0.1);
    background: rgba(0, 0, 0, 0.3);
  }

  .footer-btn {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
    font-weight: 600;
    letter-spacing: 0.1em;
    padding: 0.8rem 2rem;
    border: 1px solid;
    border-radius: 2px;
    cursor: pointer;
    transition: all 0.2s ease;
    text-transform: uppercase;
  }

  .footer-btn.pig-btn {
    background: transparent;
    border-color: rgba(255, 182, 193, 0.3);
    font-size: 1.5rem;
    padding: 0.5rem 1rem;

    &:hover {
      border-color: rgba(255, 182, 193, 0.6);
      background: rgba(255, 182, 193, 0.1);
      transform: scale(1.1);
    }
  }

  .footer-actions {
    display: flex;
    gap: 1rem;
  }

  .footer-btn.cancel {
    background: transparent;
    border-color: rgba(255, 255, 255, 0.3);
    color: rgba(255, 255, 255, 0.7);

    &:hover {
      background: rgba(255, 255, 255, 0.1);
      color: white;
    }
  }

  .footer-btn.confirm {
    background: rgba(184, 115, 51, 0.2);
    border-color: #B87333;
    color: #B87333;

    &:hover {
      background: rgba(184, 115, 51, 0.4);
      color: #D4944A;
    }
  }

  .footer-btn.reset {
    background: transparent;
    border-color: rgba(239, 68, 68, 0.3);
    color: #ef4444;

    &:hover {
      background: rgba(239, 68, 68, 0.1);
      border-color: #ef4444;
    }
  }
</style>
