<script>
    import { onMount, tick } from "svelte";
    import { fly } from "svelte/transition";
    import {
        trackProgress,
        trackPosition,
        trackDuration,
        isPlaying,
    } from "$lib/stores/stores.js";

    function getApiBase() {
        if (typeof window === "undefined") return "http://127.0.0.1:27272";
        const port = window.location.port;
        if (port === "7270" || port === "5173") return "";
        return "";
    }

    export let playerName = "";
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
    export let font = "";
    export let visible = true;

    let htmlTemplate = "";
    let loading = true;
    let error = "";
    let iframeEl = null;
    let iframeReady = false;

    // Track key вЂ” only recreate iframe on track change, NOT on progress
    $: trackKey = `${title}||${artist}`;

    // Runtime script injected into iframe вЂ” handles postMessage updates
    const RUNTIME_SCRIPT = `
<script>
  window.addEventListener('message', function(e) {
    if (!e.data || e.data.type !== 'unik-update') return;
    var d = e.data;

    // Update data-bind elements
    document.querySelectorAll('[data-bind]').forEach(function(el) {
      var bind = el.getAttribute('data-bind');
      if (bind === 'progress-width' && d.progress !== undefined) {
        el.style.width = d.progress + '%';
      } else if (bind === 'currentTime' && d.currentTime !== undefined) {
        el.textContent = d.currentTime;
      } else if (bind === 'totalTime' && d.totalTime !== undefined) {
        el.textContent = d.totalTime;
      } else if (bind === 'progress' && d.progress !== undefined) {
        el.textContent = d.progress.toFixed(1);
      } else if (bind === 'position' && d.position !== undefined) {
        el.textContent = Math.floor(d.position);
      } else if (bind === 'duration' && d.duration !== undefined) {
        el.textContent = Math.floor(d.duration);
      } else if (bind === 'playing') {
        el.setAttribute('data-playing', d.isPlaying ? 'true' : 'false');
      }
    });

    // Update CSS color variables
    if (d.colors) {
      var r = document.documentElement.style;
      if (d.colors.vibrant) r.setProperty('--vibrant', d.colors.vibrant);
      if (d.colors.lightVibrant) r.setProperty('--lightVibrant', d.colors.lightVibrant);
      if (d.colors.darkVibrant) r.setProperty('--darkVibrant', d.colors.darkVibrant);
      if (d.colors.muted) r.setProperty('--muted', d.colors.muted);
      if (d.colors.lightMuted) r.setProperty('--lightMuted', d.colors.lightMuted);
      if (d.colors.darkMuted) r.setProperty('--darkMuted', d.colors.darkMuted);
    }

    // Update font
    if (d.font) {
      document.body.style.fontFamily = '"' + d.font + '", sans-serif';
    }
  });

  // Signal parent that iframe is ready
  window.parent.postMessage({ type: 'unik-ready' }, '*');
<\/script>`;

    function escapeHtml(str) {
        if (!str) return "";
        return String(str)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function formatTime(seconds) {
        if (!seconds || seconds < 0) return "0:00";
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, "0")}`;
    }

    // Process template once with track data + initial colors/font. No progress vars.
    function buildSrcdoc(template, data, colorVars, fontFamily) {
        if (!template) return "";

        // Substitute track variables (static per track)
        let html = template
            .replace(/\{\{title\}\}/g, escapeHtml(data.title || ""))
            .replace(/\{\{artist\}\}/g, escapeHtml(data.artist || ""))
            .replace(/\{\{thumbnail\}\}/g, data.thumbnail || "");

        // Remove old {{progress}} etc placeholders вЂ” set initial values
        html = html
            .replace(/\{\{progress\}\}/g, "0")
            .replace(/\{\{position\}\}/g, "0")
            .replace(/\{\{duration\}\}/g, "0")
            .replace(/\{\{currentTime\}\}/g, "0:00")
            .replace(/\{\{totalTime\}\}/g, "0:00");

        // Build injected styles: colors + font + base reset
        const fontCSS = fontFamily
            ? `font-family: "${fontFamily}", sans-serif;`
            : "";
        const injectedCSS = `
      :root {
        --vibrant: ${colorVars.vibrant || "#D4944A"};
        --lightVibrant: ${colorVars.lightVibrant || "#F5DEB3"};
        --darkVibrant: ${colorVars.darkVibrant || "#5C4033"};
        --muted: ${colorVars.muted || "#8B6914"};
        --lightMuted: ${colorVars.lightMuted || "#B87333"};
        --darkMuted: ${colorVars.darkMuted || "rgba(20, 15, 10, 0.9)"};
      }
      html, body {
        margin: 0;
        padding: 0;
        overflow: hidden;
        background: transparent;
        ${fontCSS}
      }
      body {
        width: 100%;
        height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
      }
    `;

        // Inject CSS + runtime script before </head> or </body> or at end
        const injection = `<style>${injectedCSS}</style>${RUNTIME_SCRIPT}`;
        if (html.includes("</head>")) {
            html = html.replace("</head>", `${injection}</head>`);
        } else if (html.includes("</body>")) {
            html = html.replace("</body>", `${injection}</body>`);
        } else {
            html += injection;
        }

        return html;
    }

    // Build srcdoc only on track change (not progress)
    $: srcdoc = buildSrcdoc(
        htmlTemplate,
        { title, artist, thumbnail },
        colors,
        font,
    );

    // Send progress updates via postMessage (no iframe recreation!)
    $: if (iframeReady && iframeEl) {
        try {
            iframeEl.contentWindow.postMessage(
                {
                    type: "unik-update",
                    progress: $trackProgress,
                    position: $trackPosition,
                    duration: $trackDuration,
                    currentTime: formatTime($trackPosition),
                    totalTime: formatTime($trackDuration),
                    isPlaying: $isPlaying,
                },
                "*",
            );
        } catch (e) {
            /* iframe not ready */
        }
    }

    // Send color updates via postMessage
    $: if (iframeReady && iframeEl && colors) {
        try {
            iframeEl.contentWindow.postMessage(
                {
                    type: "unik-update",
                    colors,
                },
                "*",
            );
        } catch (e) {
            /* iframe not ready */
        }
    }

    // Send font updates via postMessage
    $: if (iframeReady && iframeEl && font) {
        try {
            iframeEl.contentWindow.postMessage(
                {
                    type: "unik-update",
                    font,
                },
                "*",
            );
        } catch (e) {
            /* iframe not ready */
        }
    }

    // Listen for iframe ready signal
    function onMessage(e) {
        if (e.data && e.data.type === "unik-ready") {
            iframeReady = true;
            // Send initial data immediately
            sendFullUpdate();
        }
    }

    function sendFullUpdate() {
        if (!iframeEl) return;
        try {
            iframeEl.contentWindow.postMessage(
                {
                    type: "unik-update",
                    progress: $trackProgress,
                    position: $trackPosition,
                    duration: $trackDuration,
                    currentTime: formatTime($trackPosition),
                    totalTime: formatTime($trackDuration),
                    isPlaying: $isPlaying,
                    colors,
                    font,
                },
                "*",
            );
        } catch (e) {
            /* iframe not ready */
        }
    }

    async function loadTemplate() {
        if (!playerName) return;
        loading = true;
        error = "";

        try {
            const res = await fetch(
                `${getApiBase()}/api/custom-players/${playerName}`,
            );
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

    // Reset iframe ready state when track changes (new iframe created by {#key})
    $: if (trackKey) {
        iframeReady = false;
    }

    onMount(() => {
        loadTemplate();
        window.addEventListener("message", onMessage);
        return () => window.removeEventListener("message", onMessage);
    });

    $: if (playerName) {
        loadTemplate();
    }
</script>

{#if visible}
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
                <iframe
                    bind:this={iframeEl}
                    title="Custom Player: {playerName}"
                    {srcdoc}
                    sandbox="allow-scripts allow-same-origin"
                    class="custom-iframe"
                ></iframe>
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

    .custom-iframe {
        width: 100%;
        height: 100%;
        border: none;
        background: transparent;
        display: block;
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
