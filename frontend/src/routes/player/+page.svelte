<script>
      import { getPickedPlayer } from '$lib/getPlayers.js'
      import { onMount } from 'svelte';

      let pickedPlayer = []
      let playerName = ''
      let savedStyle = {}

      // Color manipulation functions
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

      /**
       * Transform user CSS to be scoped to this player.
       * User writes: .mainDiv { background: red; }
       * We output: #unik-player .player-Generic .mainDiv { background: red !important; }
       */
      function transformCSS(rawCSS, playerName) {
            if (!rawCSS || !rawCSS.trim()) {
                  return '';
            }

            // Scope prefix for this player
            const scope = `#unik-player .player-${playerName}`;

            // Step 1: Remove all comments /* ... */
            let css = rawCSS.replace(/\/\*[\s\S]*?\*\//g, '');

            // Step 2: Split by closing brace to process each rule
            const parts = css.split('}');
            const transformed = [];

            for (let part of parts) {
                  part = part.trim();
                  if (!part) continue;

                  const braceIdx = part.indexOf('{');
                  if (braceIdx === -1) continue;

                  let selector = part.substring(0, braceIdx).trim();
                  let body = part.substring(braceIdx + 1).trim();

                  // Skip empty selectors or bodies
                  if (!selector || !body) continue;

                  // Skip @rules (media queries, keyframes, etc.)
                  if (selector.startsWith('@')) {
                        transformed.push(part + '}');
                        continue;
                  }

                  // Prefix each selector (handle comma-separated selectors)
                  const selectors = selector.split(',').map(s => {
                        s = s.trim();
                        if (!s) return '';
                        // Already has our prefix - skip
                        if (s.includes('#unik-player')) return s;
                        // Universal selector * -> scope *
                        if (s === '*') return `${scope} *`;
                        // Regular selectors
                        return `${scope} ${s}`;
                  }).filter(s => s);

                  if (selectors.length === 0) continue;

                  // Add !important to all property values
                  body = body.replace(/:\s*([^;!]+);/g, ': $1 !important;');
                  // Handle last property without semicolon
                  if (body && !body.endsWith(';') && !body.endsWith('!important')) {
                        body = body.replace(/:\s*([^;!{}]+)$/, ': $1 !important');
                  }

                  transformed.push(`${selectors.join(', ')} { ${body} }`);
            }

            return transformed.join('\n');
      }

      /**
       * Inject CSS into document <head>
       */
      function injectCSS(css, id = 'unik-player-custom-css') {
            // Remove existing
            const existing = document.getElementById(id);
            if (existing) {
                  existing.remove();
            }

            if (!css || !css.trim()) {
                  console.log('[CSS] Nothing to inject');
                  return;
            }

            const style = document.createElement('style');
            style.id = id;
            style.textContent = css;
            document.head.appendChild(style);

            console.log('[CSS] Injected style:', id);
            console.log('[CSS] Content:\n' + css);
      }

      // Reactive: compute inline styles from savedStyle
      $: useStaticColor = savedStyle.colorMode === 'static';
      $: staticColorValue = savedStyle.staticColor || '#B87333';
      $: colors = useStaticColor ? generateColorVars(staticColorValue) : null;
      $: fontFamily = savedStyle.font || 'Rubik';

      $: inlineStyle = useStaticColor && colors ? `
            --vibrant: ${colors.vibrant};
            --lightVibrant: ${colors.lightVibrant};
            --darkVibrant: ${colors.darkVibrant};
            --muted: ${colors.muted};
            --lightMuted: ${colors.lightMuted};
            --darkMuted: ${colors.darkMuted};
            font-family: "${fontFamily}", sans-serif;
      ` : `font-family: "${fontFamily}", sans-serif;`;

      onMount(async () => {
            // Get player name from URL query: /player?Generic -> "Generic"
            playerName = location.search.slice(1);
            console.log('=== PLAYER PAGE LOADING ===');
            console.log('[Player] Name:', playerName);

            // Load player component
            pickedPlayer = getPickedPlayer(playerName);
            console.log('[Player] Component loaded:', pickedPlayer.length > 0);

            // Transparent background for OBS
            document.documentElement.style.background = 'transparent';
            document.body.style.background = 'transparent';

            // Step 1: Load settings (colorMode, font, etc)
            try {
                  const res = await fetch('/api/styles');
                  if (res.ok) {
                        const allStyles = await res.json();
                        savedStyle = allStyles[playerName] || {};
                        console.log('[Player] Settings loaded:', savedStyle);
                  }
            } catch (err) {
                  console.warn('[Player] Failed to load settings:', err);
            }

            // Step 2: Load CSS from file
            try {
                  console.log('[CSS] Fetching /api/css/' + playerName);
                  const cssRes = await fetch(`/api/css/${playerName}`);
                  console.log('[CSS] Response status:', cssRes.status);

                  if (cssRes.ok) {
                        const rawCSS = await cssRes.text();
                        console.log('[CSS] Raw CSS received:');
                        console.log(rawCSS);

                        if (rawCSS && rawCSS.trim()) {
                              // Transform and inject CSS
                              const transformedCSS = transformCSS(rawCSS, playerName);
                              console.log('[CSS] Transformed CSS:');
                              console.log(transformedCSS);

                              injectCSS(transformedCSS);
                        } else {
                              console.log('[CSS] No CSS content (empty file)');
                        }
                  } else {
                        console.log('[CSS] Fetch failed:', cssRes.status);
                  }
            } catch (err) {
                  console.error('[CSS] Error loading CSS:', err);
            }

            console.log('=== PLAYER PAGE READY ===');
      });
</script>

<!-- Inject fonts -->
<svelte:head>
      <!-- Google Fonts preconnect -->
      <link rel="preconnect" href="https://fonts.googleapis.com">
      <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">

      <!-- Load all available Google Fonts for player -->
      <link href="https://fonts.googleapis.com/css2?family=EB+Garamond:ital,wght@0,400..800;1,400..800&family=JetBrains+Mono:wght@400;500;600;700&family=Old+Standard+TT:ital,wght@0,400;0,700;1,400&family=Rubik:ital,wght@0,300..900;1,300..900&family=Yeseva+One&display=swap" rel="stylesheet">
</svelte:head>

<div class="player-page">
      {#each pickedPlayer as { component }}
            <div id="unik-player" class="player-container" style={inlineStyle}>
                  <svelte:component this={component} />
            </div>
      {/each}
</div>

<style>
      :global(html),
      :global(body) {
            background: transparent !important;
            margin: 0;
            padding: 0;
      }

      .player-page {
            width: 100vw;
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            background: transparent;
            /* Colors are set dynamically by Vibrant.js on :root */
      }

      .player-container {
            display: flex;
            justify-content: center;
            align-items: center;
      }

      /* Apply font globally to player */
      .player-container :global(*) {
            font-family: inherit;
      }
</style>
