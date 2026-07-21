<script>
    import { onMount, tick } from "svelte";
    import { fly } from "svelte/transition";
    import {
        trackProgress,
        trackPosition,
        trackDuration,
        isPlaying,
        ShowTrack,
    } from "$lib/stores/stores.js";
    import { marquee } from "$lib/marquee.js";
    import { getInlinePlayerHtml } from "$lib/getPlayers.js";

    function getApiBase() {
        if (typeof window === "undefined") return "http://127.0.0.1:27272";
        const port = window.location.port;
        if (port === "7270" || port === "5173") return "";
        return "";
    }

    export let playerName = "";
    export let showAlways = false;
    export let isExample = false;
    export let title = "";
    export let artist = "";
    export let thumbnail = "";
    export let colors = {
        vibrant: "#D4944A",
        lightVibrant: "#F5DEB3",
        darkVibrant: "#5C4033",
        muted: "#8B6914",
        lightMuted: "#B87333",
        darkMuted: "rgba(20, 15, 10, 0.9)",
    };

    $: safeColors = computeColors(colors, title, artist, colorVersion);
    let colorVersion = 0;

    function handleColorsUpdated() {
        colorVersion++;
    }

    function computeColors(cols, _titleDep, _artistDep, _ver) {
        if (cols) return cols;
        const root = readRootColors();
        return root || {
            vibrant: "#D4944A",
            lightVibrant: "#F5DEB3",
            darkVibrant: "#5C4033",
            muted: "#8B6914",
            lightMuted: "#B87333",
            darkMuted: "rgba(20, 15, 10, 0.9)",
        };
    }

    function readRootColors() {
        if (typeof document === 'undefined') return null;
        const style = getComputedStyle(document.documentElement);
        const vibrant = style.getPropertyValue('--vibrant').trim();
        if (!vibrant || vibrant === '#D4944A') return null;
        return {
            vibrant,
            lightVibrant: style.getPropertyValue('--lightVibrant').trim() || '#F5DEB3',
            darkVibrant: style.getPropertyValue('--darkVibrant').trim() || '#5C4033',
            muted: style.getPropertyValue('--muted').trim() || '#8B6914',
            lightMuted: style.getPropertyValue('--lightMuted').trim() || '#B87333',
            darkMuted: style.getPropertyValue('--darkMuted').trim() || 'rgba(20, 15, 10, 0.9)',
        };
    }
    export let font = "";

    let htmlTemplate = "";
    let loading = true;
    let error = "";
    let shadowHost = null;
    let shadowRoot = null;

    $: trackKey = `${title}||${artist}`;

    function formatTime(seconds) {
        if (!seconds || seconds < 0) return "0:00";
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, "0")}`;
    }

    function escapeHtml(str) {
        if (!str) return "";
        return String(str)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function renderProgressBarHTML(tag) {
        const h = (tag.match(/height="([^"]+)"/) || [])[1] || '4px';
        const br = (tag.match(/border-radius="([^"]+)"/) || [])[1] || (tag.match(/borderRadius="([^"]+)"/) || [])[1] || '2px';
        const showTime = tag.includes('showTime') && !tag.includes('showTime={false}') || tag.includes('show-time') && !tag.includes('show-time="false"');

        return `<div class="progress-container">
  ${showTime ? '<span class="time current" data-bind="currentTime">0:00</span>' : ''}
  <div class="progress-bar" style="height:${h};border-radius:${br};">
    <div class="progress-fill" style="width:0%;border-radius:${br};" data-bind="progress-width"></div>
  </div>
  ${showTime ? '<span class="time total" data-bind="totalTime">0:00</span>' : ''}
</div>`;
    }

    function replaceProgressBar(html) {
        return html.replace(/<ProgressBarComponent\s*[^>]*\/>/gi, (match) => renderProgressBarHTML(match));
    }

    function renderToShadow(template) {
        if (!shadowHost) return;
        let html = template
            .replace(/\{\{title\}\}/g, escapeHtml(title || ""))
            .replace(/\{\{artist\}\}/g, escapeHtml(artist || ""))
            .replace(/\{\{thumbnail\}\}/g, thumbnail || "")
            .replace(/\{\{progress\}\}/g, "0")
            .replace(/\{\{position\}\}/g, "0")
            .replace(/\{\{duration\}\}/g, "0")
            .replace(/\{\{currentTime\}\}/g, "0:00")
            .replace(/\{\{totalTime\}\}/g, "0:00");
        html = replaceProgressBar(html);

        shadowRoot = shadowHost.attachShadow({ mode: 'open' });
        shadowRoot.innerHTML = html;
        updateShadowColors(safeColors, font);
    }

    function updateShadowColors(colVars, fontName) {
        if (!shadowRoot) return;
        const cssVars = `:root {
  --vibrant: ${colVars.vibrant || "#D4944A"};
  --lightVibrant: ${colVars.lightVibrant || "#F5DEB3"};
  --darkVibrant: ${colVars.darkVibrant || "#5C4033"};
  --muted: ${colVars.muted || "#8B6914"};
  --lightMuted: ${colVars.lightMuted || "#B87333"};
  --darkMuted: ${colVars.darkMuted || "rgba(20, 15, 10, 0.9)"};
  --font: "${fontName || "Rubik"}", sans-serif;
}
body { font-family: var(--font); margin:0; padding:0; background:transparent; }
.progress-container { display:flex; align-items:center; gap:0; width:100%; }
.progress-container .time {font-size:0.75rem; color:var(--lightVibrant,rgba(255,255,255,0.7)); min-width:2.2rem; }
.progress-container .time.current { text-align:right; padding-right:0.3rem; }
.progress-container .time.total { text-align:left; opacity:0.6; padding-left:0.3rem; }
.progress-container .progress-bar { flex:1; position:relative; background:var(--darkVibrant,rgba(255,255,255,0.01)); overflow:hidden; }
.progress-container .progress-fill { height:100%; background:linear-gradient(90deg,var(--vibrant,#B87333) 0%,var(--lightVibrant,#D4944A) 100%); transition:width 0.3s linear; }
`;
        let style = shadowRoot.getElementById('_unik-styles');
        if (!style) {
            style = document.createElement('style');
            style.id = '_unik-styles';
            shadowRoot.prepend(style);
        }
        style.textContent = cssVars;
    }

    // Update colors/font > inject into shadowRoot
    $: if (shadowRoot) {
        updateShadowColors(safeColors, font);
    }

    // Update progress/timeline data-bind elements inside shadowRoot (no iframe)
    $: if (shadowRoot) {
        const els = shadowRoot.querySelectorAll('[data-bind]');
        for (let i = 0; i < els.length; i++) {
            const el = els[i];
            const bind = el.getAttribute('data-bind');
            if (bind === 'progress-width') { el.style.width = ($trackProgress || 0) + '%'; }
            else if (bind === 'currentTime') { el.textContent = formatTime($trackPosition); }
            else if (bind === 'totalTime') { el.textContent = formatTime($trackDuration); }
            else if (bind === 'position') { el.textContent = Math.floor($trackPosition || 0); }
            else if (bind === 'duration') { el.textContent = Math.floor($trackDuration || 0); }
            else if (bind === 'progress') { el.textContent = ($trackProgress || 0).toFixed(1); }
            else if (bind === 'playing') { el.setAttribute('data-playing', $isPlaying ? 'true' : 'false'); }
        }
    }

    // Marquee via `use:marquee` on .title / .artist inside shadowRoot
    let marqueeCleanups = [];
    let lastMarqueeKey = '';
    $: {
        if (shadowRoot && trackKey !== lastMarqueeKey) {
            lastMarqueeKey = trackKey;
            marqueeCleanups.forEach(fn => { try { fn.destroy(); } catch(e) {} });
            marqueeCleanups = [];
            shadowRoot.querySelectorAll('.title, .artist').forEach(el => {
                marqueeCleanups.push(marquee(el, { speed: 70, optGap: 69 }));
            });
        }
    }

    async function loadTemplate() {
        if (!playerName) return;
        loading = true;
        error = "";

        // Always try custom-players API first (user might have saved a custom version)
        try {
            const customRes = await fetch(`${getApiBase()}/api/custom-players/${playerName}`);
            if (customRes.ok) {
                const text = await customRes.text();
                if (!text.includes("<!doctype html>") || !text.includes("<title>UnikPlayer</title>")) {
                    htmlTemplate = text;
                    loading = false;
                    return;
                }
            }
        } catch (e) {
            // custom API failed, fall through to inline/example
        }

        // Fall back to inline HTML (bundled .html in src/lib/players/)
        const inlineHtml = getInlinePlayerHtml(playerName);
        if (inlineHtml) {
            htmlTemplate = inlineHtml;
            loading = false;
            return;
        }

        // Fall back to example players API
        try {
            const res = await fetch(`${getApiBase()}/api/players/${playerName}`);
            if (res.ok) {
                const text = await res.text();
                if (
                    text.includes("<!doctype html>") &&
                    text.includes("<title>UnikPlayer</title>")
                ) {
                    error = "Player not found";
                } else {
                    htmlTemplate = text;
                }
            } else {
                error = "Failed to load player";
            }
        } catch (e) {
            console.error("Load template error:", e);
            error = "Connection error";
        }
        loading = false;
    }

    // Render to shadow when template is ready
    $: if (htmlTemplate && shadowHost) {
        renderToShadow(htmlTemplate);
    }

    onMount(() => {
        loadTemplate();
        window.addEventListener("unik-colors-updated", handleColorsUpdated);
        return () => {
            window.removeEventListener("unik-colors-updated", handleColorsUpdated);
        };
    });

    $: if (playerName) {
        loadTemplate();
    }

    // Reset shadowRoot when track changes (new host created by {#key})
    $: if (trackKey) {
        shadowRoot = null;
    }

    $: shouldShow = showAlways || $ShowTrack;
</script>

{#if shouldShow}
    {#key trackKey}
        <div
            class="custom-player-wrapper"
            in:fly|global={{ x: -50, duration: 400, opacity: 0 }}
            out:fly|global={{ x: 50, duration: 400, opacity: 0 }}
        >
            {#if loading}
                <div class="loading">Loading...</div>
            {:else if error}
                <div class="error">{error}</div>
            {:else}
                <div
                    bind:this={shadowHost}
                    class="custom-player-shadow-host"
                ></div>
            {/if}
        </div>
    {/key}
{/if}

<style lang="scss">
    .custom-player-wrapper {
        width: 100%;
        height: 100%;
        position: relative;
        overflow: hidden;
    }

    .custom-player-shadow-host {
        width: 100%;
        height: 100%;
        overflow: hidden;
        display: flex;
        align-items: center;
        justify-content: center;
    }

    .loading,
    .error {
        display: flex;
        align-items: center;
        justify-content: center;
        height: 100%;
        font-family: "JetBrains Mono", monospace;
        font-size: 0.9rem;
        letter-spacing: 0.1em;
    }

    .loading {
        color: rgba(255, 255, 255, 0.5);
    }

    .error {
        color: #ff6b6b;
    }
</style>



