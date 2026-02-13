<script>
    import { fly, fade } from "svelte/transition";
    import {
        editorOpen,
        editingPlayer,
        editingPlayerIsCustom,
        editorCSS,
        editorHTML,
        playerStyles,
        colorMode,
        staticColor,
        selectedFont,
        ShowNotification,
        notificationText,
        title as trackTitle,
        artist as trackArtist,
        thumbnail as trackThumbnail,
        trackProgress,
        trackPosition,
        trackDuration,
        isPlaying,
        language,
    } from "$lib/stores/stores.js";
    import { getPickedPlayer, getPlayerMeta } from "$lib/getPlayers.js";
    import ColorPicker from "./ColorPicker.svelte";
    import FontPicker from "./FontPicker.svelte";
    import ValidationErrorDialog from "./ValidationErrorDialog.svelte";
    import { generateColorVars } from "$lib/utils/colors.js";
    import {
        transformCSS as sharedTransformCSS,
        injectCSS as sharedInjectCSS,
        removeCSS,
        loadCSSFromBackend as sharedLoadCSS,
        saveCSSToBackend as sharedSaveCSS,
        deleteCSSFromBackend,
    } from "$lib/utils/playerCSS.js";

    // Backend API base URL - for dev mode
    const isBrowser = typeof window !== "undefined";
    const API_BASE =
        isBrowser && window.location.port === "5173"
            ? "http://localhost:27272"
            : "";

    let playerComponent = null;
    let playerName = "";
    let cssText = "";
    let originalCSS = "";
    let htmlText = "";
    let originalHTML = "";
    let localColorMode = "dynamic";
    let localStaticColor = "#B87333";
    let localFont = "Rubik";
    let lastAppliedFont = "Rubik";
    let localPreviewScale = 0.5;
    let cssLoaded = false; // Flag to prevent CSS reload on every change
    let isCustomPlayer = false;
    let editorIframeEl = null;
    let iframeSrcdoc = ""; // Set once on htmlText change, not on every color/font change

    // Validation error dialog state
    let showValidationErrors = false;
    let validationErrors = [];

    // Snippets for custom player editor
    let copiedSnippet = null;

    const snippets = [
        {
            id: "title",
            label: "Title",
            preview: "{{title}}",
            desc: "Track title",
            code: "{{title}}",
        },
        {
            id: "artist",
            label: "Artist",
            preview: "{{artist}}",
            desc: "Artist name",
            code: "{{artist}}",
        },
        {
            id: "thumb",
            label: "Thumbnail",
            preview: "{{thumbnail}}",
            desc: "Cover art URL",
            code: '<img src="{{thumbnail}}" alt="cover" />',
        },
        {
            id: "progress",
            label: "Progress bar",
            preview: "data-bind",
            desc: "Animated progress bar",
            code: '<div style="height:4px;background:rgba(255,255,255,.1);border-radius:2px;overflow:hidden">\n  <div data-bind="progress-width" style="height:100%;background:var(--vibrant);width:0%;transition:width .5s linear"></div>\n</div>',
        },
        {
            id: "curTime",
            label: "Current time",
            preview: "currentTime",
            desc: "Current position e.g. 2:34",
            code: '<span data-bind="currentTime">0:00</span>',
        },
        {
            id: "totTime",
            label: "Total time",
            preview: "totalTime",
            desc: "Total duration e.g. 4:12",
            code: '<span data-bind="totalTime">0:00</span>',
        },
        {
            id: "vibrant",
            label: "--vibrant",
            preview: "var(--vibrant)",
            desc: "Main accent color from cover",
            code: "var(--vibrant)",
        },
        {
            id: "darkMuted",
            label: "--darkMuted",
            preview: "var(--darkMuted)",
            desc: "Dark background color",
            code: "var(--darkMuted)",
        },
    ];

    function copySnippet(s) {
        navigator.clipboard.writeText(s.code).then(() => {
            copiedSnippet = s.id;
            notificationText.set($language === "ru" ? "Скопировано" : "Copied");
            ShowNotification.set(true);
            setTimeout(() => {
                copiedSnippet = null;
            }, 1200);
        });
    }

    // Update CSS with font-family when font changes
    function updateFontInCSS(fontName) {
        const fontRule = `.title > *, .artist > * {\n  font-family: "${fontName}", sans-serif;\n}`;
        const fontRegex =
            /\.title\s*>\s*\*,\s*\.artist\s*>\s*\*\s*\{\s*font-family:\s*[^}]+\}/;

        if (fontRegex.test(cssText)) {
            // Replace existing font rule
            cssText = cssText.replace(fontRegex, fontRule);
        } else {
            // Add font rule at the beginning after comments
            const commentEndMatch = cssText.match(/^(\/\*[\s\S]*?\*\/\s*)+/);
            if (commentEndMatch) {
                const comments = commentEndMatch[0];
                const rest = cssText.slice(comments.length);
                cssText = comments + fontRule + "\n\n" + rest;
            } else {
                cssText = fontRule + "\n\n" + cssText;
            }
        }
    }

    // Get default CSS for current player from player metadata
    function getDefaultCSS(pName) {
        const meta = getPlayerMeta(pName);
        return (
            meta?.defaultCSS ||
            `/* === ${pName} PLAYER === */
/* No default CSS template available */
/* Colors: var(--vibrant), var(--lightVibrant),
   var(--darkVibrant), var(--muted),
   var(--lightMuted), var(--darkMuted) */
`
        );
    }

    // Available fonts for random selection
    const availableFonts = [
        "Rubik",
        "EB Garamond",
        "Old Standard TT",
        "Yeseva One",
        "JetBrains Mono",
        "Arial",
        "Calibri",
        "Georgia",
        "Times New Roman",
        "Verdana",
        "Segoe UI",
        "Consolas",
        "Trebuchet MS",
        "Palatino Linotype",
        "Garamond",
    ];

    function randomizeFont(e) {
        const randomIndex = Math.floor(Math.random() * availableFonts.length);
        localFont = availableFonts[randomIndex];
        triggerBtnAnim(e);
    }

    // Button press animation helper
    function triggerBtnAnim(e) {
        const btn = e.currentTarget;
        btn.classList.remove("pressing");
        void btn.offsetWidth;
        btn.classList.add("pressing");
        setTimeout(() => btn.classList.remove("pressing"), 200);
    }

    let pigAudio;
    let pigBouncing = false;

    function playPigSound() {
        if (!pigAudio) {
            pigAudio = new Audio("/pig.mp3");
            pigAudio.volume = 0.4;
        }
        pigAudio.currentTime = 0;
        pigAudio.play();

        // Trigger bounce animation
        pigBouncing = false;
        requestAnimationFrame(() => {
            pigBouncing = true;
            setTimeout(() => (pigBouncing = false), 300);
        });
    }

    // Live preview: transform and inject CSS when cssText changes
    $: if (cssLoaded && playerName && cssText) {
        const transformed = sharedTransformCSS(
            cssText,
            playerName,
            ".preview-frame",
        );
        sharedInjectCSS(transformed, "editor-preview-css");
    }

    // Load HTML for custom player
    async function loadHTMLFromBackend(player) {
        try {
            const res = await fetch(`${API_BASE}/api/custom-players/${player}`);
            if (res.ok) {
                return await res.text();
            }
        } catch (err) {
            console.log("[Editor] Failed to load HTML from backend:", err);
        }
        return null;
    }

    // Save HTML for custom player (with validation)
    async function saveHTMLToBackend(player, html) {
        try {
            const res = await fetch(
                `${API_BASE}/api/custom-players/${player}`,
                {
                    method: "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ html }),
                },
            );
            const data = await res.json();

            if (res.ok && data.success) {
                console.log(`[Editor] HTML saved for ${player}`);
                return { success: true };
            } else if (data.validation) {
                // Validation errors
                return { success: false, errors: data.validation.errors };
            } else {
                return { success: false, error: data.error };
            }
        } catch (err) {
            console.log("[Editor] Failed to save HTML:", err);
            return { success: false, error: "Connection error" };
        }
    }

    // Reset custom player to backup
    async function resetCustomPlayer(player) {
        try {
            const res = await fetch(
                `${API_BASE}/api/custom-players/${player}/reset`,
                {
                    method: "POST",
                },
            );
            if (res.ok) {
                // Reload HTML after reset
                const html = await loadHTMLFromBackend(player);
                if (html) {
                    htmlText = html;
                    originalHTML = html;
                }
                return true;
            }
        } catch (err) {
            console.log("[Editor] Failed to reset custom player:", err);
        }
        return false;
    }

    // Load editor when player changes - only once per open
    $: if ($editingPlayer && !cssLoaded) {
        const players = getPickedPlayer($editingPlayer);
        if (players.length > 0) {
            playerComponent = players[0].component;
            playerName = players[0].name;
            isCustomPlayer =
                players[0].isCustom || $editingPlayerIsCustom || false;
        }
        // Load saved settings from store
        localColorMode = $playerStyles[$editingPlayer]?.colorMode || "dynamic";
        localStaticColor =
            $playerStyles[$editingPlayer]?.staticColor || "#B87333";
        localFont = $playerStyles[$editingPlayer]?.font || "Rubik";
        const metaScale = getPlayerMeta($editingPlayer)?.defaultScale ?? 0.5;
        localPreviewScale =
            $playerStyles[$editingPlayer]?.previewScale ?? metaScale;
        lastAppliedFont = localFont;

        if (isCustomPlayer) {
            // Load HTML for custom player
            loadHTMLFromBackend($editingPlayer).then((html) => {
                htmlText = html || "";
                originalHTML = htmlText;
                cssLoaded = true;
            });
        } else {
            // Load CSS from backend file (only once)
            sharedLoadCSS($editingPlayer).then((css) => {
                cssText = css || getDefaultCSS($editingPlayer);
                originalCSS = cssText;
                cssLoaded = true;
            });
        }
    }

    // Update font-family in custom HTML (* selector)
    function updateFontInHTML(fontName) {
        if (!htmlText) return;
        const fontRule = `* { font-family: "${fontName}", sans-serif; }`;
        // Replace existing * { font-family: ... } rule
        const regex = /\*\s*\{[^}]*font-family:[^}]*\}/;
        if (regex.test(htmlText)) {
            htmlText = htmlText.replace(regex, fontRule);
        } else {
            // Inject before </style>
            if (htmlText.includes("</style>")) {
                htmlText = htmlText.replace(
                    "</style>",
                    `  ${fontRule}
  </style>`,
                );
            }
        }
    }

    // Watch for font changes and update CSS/HTML
    $: if (localFont && localFont !== lastAppliedFont) {
        if (isCustomPlayer) {
            updateFontInHTML(localFont);
        } else {
            updateFontInCSS(localFont);
        }
        lastAppliedFont = localFont;
    }

    async function handleConfirm() {
        if (isCustomPlayer) {
            // Save HTML for custom player
            const result = await saveHTMLToBackend($editingPlayer, htmlText);
            if (!result.success) {
                if (result.errors) {
                    validationErrors = result.errors;
                    showValidationErrors = true;
                } else {
                    notificationText.set(result.error || "Save failed");
                    ShowNotification.set(true);
                }
                return; // Don't close editor on error
            }
        } else {
            // Save CSS to backend file
            await sharedSaveCSS($editingPlayer, cssText);
        }

        // Save settings to backend /api/styles (JSON)
        try {
            const allStyles = await fetch(`${API_BASE}/api/styles`)
                .then((r) => r.json())
                .catch(() => ({}));
            allStyles[$editingPlayer] = {
                colorMode: localColorMode,
                staticColor: localStaticColor,
                font: localFont,
                previewScale: localPreviewScale,
            };
            await fetch(`${API_BASE}/api/styles`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(allStyles),
            });
            console.log("[Editor] Settings saved to backend");
        } catch (err) {
            console.log("[Editor] Failed to save settings to backend:", err);
        }

        // Update local store
        playerStyles.update((styles) => ({
            ...styles,
            [$editingPlayer]: {
                colorMode: localColorMode,
                staticColor: localStaticColor,
                font: localFont,
                previewScale: localPreviewScale,
            },
        }));
        colorMode.set(localColorMode);
        staticColor.set(localStaticColor);
        selectedFont.set(localFont);

        // Dispatch event to refresh CSS on main page
        window.dispatchEvent(new CustomEvent("unik-css-refresh"));

        notificationText.set("Saved");
        ShowNotification.set(true);
        closeEditor();
    }

    function handleCancel() {
        closeEditor();
    }

    async function handleReset() {
        if (isCustomPlayer) {
            // Reset to backup for custom player
            const success = await resetCustomPlayer($editingPlayer);
            if (success) {
                notificationText.set("Reset to original");
                ShowNotification.set(true);
            } else {
                notificationText.set("Reset failed");
                ShowNotification.set(true);
            }
        } else {
            // Reset built-in player: delete user CSS on backend, revert to factory default
            await deleteCSSFromBackend($editingPlayer);
            cssText = getDefaultCSS(playerName);
            notificationText.set("Reset to factory defaults");
            ShowNotification.set(true);
        }
        localColorMode = "dynamic";
        localStaticColor = "#B87333";
        localFont = "Rubik";
        localPreviewScale = getPlayerMeta($editingPlayer)?.defaultScale ?? 0.5;
    }

    async function openHTMLExternal() {
        try {
            await saveHTMLToBackend($editingPlayer, htmlText);
            const res = await fetch(
                `${API_BASE}/api/open-html/${$editingPlayer}`,
            );
            if (!res.ok) {
                notificationText.set("Could not open file");
                ShowNotification.set(true);
            }
        } catch (err) {
            notificationText.set("Connection error");
            ShowNotification.set(true);
        }
    }

    async function showStylesPath() {
        try {
            // First save current CSS
            await sharedSaveCSS($editingPlayer, cssText);

            // Open the CSS file in default editor
            const res = await fetch(
                `${API_BASE}/api/open-css/${$editingPlayer}`,
            );
            if (!res.ok) {
                alert(
                    `CSS file: %LocalAppData%\\UnikPlayer\\css\\${$editingPlayer}.css`,
                );
            }
        } catch (err) {
            alert(
                `CSS file: %LocalAppData%\\UnikPlayer\\css\\${$editingPlayer}.css`,
            );
        }
    }

    function closeEditor() {
        removeCSS("editor-preview-css"); // Remove live preview styles
        cssLoaded = false; // Reset flag for next editor open
        isCustomPlayer = false;
        htmlText = "";
        originalHTML = "";
        showValidationErrors = false;
        validationErrors = [];
        editorIframeEl = null;
        editorOpen.set(false);
        editingPlayer.set(null);
        editingPlayerIsCustom.set(false);
    }

    // Generate inline style for preview
    // For dynamic mode: don't set colors, inherit from :root (set by Vibrant.js)
    // For static mode: generate colors from selected color
    $: previewColors =
        localColorMode === "static"
            ? generateColorVars(localStaticColor)
            : null;

    $: previewStyle = previewColors
        ? `
    --vibrant: ${previewColors.vibrant};
    --lightVibrant: ${previewColors.lightVibrant};
    --darkVibrant: ${previewColors.darkVibrant};
    --muted: ${previewColors.muted};
    --lightMuted: ${previewColors.lightMuted};
    --darkMuted: ${previewColors.darkMuted};
    font-family: "${localFont}", sans-serif;
  `
        : `font-family: "${localFont}", sans-serif;`;

    // Rebuild srcdoc only when HTML content changes (not on color/font changes)
    $: if (isCustomPlayer && htmlText) {
        iframeSrcdoc = processCustomHTML(
            htmlText,
            previewColors,
            localFont,
            $trackTitle,
            $trackArtist,
            $trackThumbnail,
            $trackPosition,
            $trackDuration,
            $trackProgress,
        );
    }

    // Send color/font updates via postMessage (no iframe reload)
    function sendColorsAndFont() {
        if (!editorIframeEl || !isCustomPlayer) return;
        try {
            const colors = previewColors || {
                vibrant: "#D4944A",
                lightVibrant: "#F5DEB3",
                darkVibrant: "#5C4033",
                muted: "#8B6914",
                lightMuted: "#B87333",
                darkMuted: "rgba(20,15,10,0.9)",
            };
            editorIframeEl.contentWindow.postMessage(
                { type: "unik-update", colors, font: localFont },
                "*",
            );
        } catch (e) {
            /* iframe not ready */
        }
    }

    $: if (localColorMode || localStaticColor) sendColorsAndFont();
    $: if (localFont) sendColorsAndFont();

    // Send live progress updates to editor iframe via postMessage
    $: if (editorIframeEl && isCustomPlayer) {
        try {
            editorIframeEl.contentWindow.postMessage(
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

    // Note: Custom CSS is NOT applied in preview to avoid conflicts with component styles
    // Preview shows the player "as-is" with only font and color variables applied
    // Custom CSS is applied only on the actual player page (player/+page.svelte)

    // Runtime script for iframe — listens for postMessage updates
    const PREVIEW_RUNTIME = `<script>
window.addEventListener('message', function(e) {
  if (!e.data || e.data.type !== 'unik-update') return;
  var d = e.data;
  document.querySelectorAll('[data-bind]').forEach(function(el) {
    var bind = el.getAttribute('data-bind');
    if (bind === 'progress-width' && d.progress !== undefined) el.style.width = d.progress + '%';
    else if (bind === 'currentTime' && d.currentTime) el.textContent = d.currentTime;
    else if (bind === 'totalTime' && d.totalTime) el.textContent = d.totalTime;
    else if (bind === 'progress' && d.progress !== undefined) el.textContent = d.progress.toFixed(1);
    else if (bind === 'playing') el.setAttribute('data-playing', d.isPlaying ? 'true' : 'false');
  });
  if (d.colors) {
    var r = document.documentElement.style;
    if (d.colors.vibrant) r.setProperty('--vibrant', d.colors.vibrant);
    if (d.colors.lightVibrant) r.setProperty('--lightVibrant', d.colors.lightVibrant);
    if (d.colors.darkVibrant) r.setProperty('--darkVibrant', d.colors.darkVibrant);
    if (d.colors.muted) r.setProperty('--muted', d.colors.muted);
    if (d.colors.lightMuted) r.setProperty('--lightMuted', d.colors.lightMuted);
    if (d.colors.darkMuted) r.setProperty('--darkMuted', d.colors.darkMuted);
  }
  if (d.font) document.body.style.fontFamily = '"' + d.font + '", sans-serif';
});
<\/script>`;

    function formatTime(seconds) {
        if (!seconds || seconds < 0) return "0:00";
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, "0")}`;
    }

    // Process custom player HTML for preview — uses live data with demo fallback
    function processCustomHTML(
        html,
        colors,
        fontFamily,
        liveTitle,
        liveArtist,
        liveThumb,
        livePos,
        liveDur,
        liveProgress,
    ) {
        if (!html) return "";

        const demoThumb =
            'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="100"%3E%3Crect fill="%231a1a2e" width="100" height="100"/%3E%3Ctext x="50" y="55" text-anchor="middle" fill="%23B87333" font-size="14"%3EDEMO%3C/text%3E%3C/svg%3E';

        const title = liveTitle || "Demo Track Title";
        const artist = liveArtist || "Demo Artist";
        const thumb = liveThumb || demoThumb;
        const pos = livePos || 0;
        const dur = liveDur || 0;
        const prog = liveProgress || 0;

        // Substitute track variables only
        let processed = html
            .replace(/\{\{title\}\}/g, title)
            .replace(/\{\{artist\}\}/g, artist)
            .replace(/\{\{thumbnail\}\}/g, thumb)
            .replace(/\{\{progress\}\}/g, prog.toFixed(1))
            .replace(/\{\{position\}\}/g, Math.floor(pos))
            .replace(/\{\{duration\}\}/g, Math.floor(dur))
            .replace(/\{\{currentTime\}\}/g, formatTime(pos))
            .replace(/\{\{totalTime\}\}/g, formatTime(dur));

        // Generate color CSS
        const colorVars = colors || {
            vibrant: "#D4944A",
            lightVibrant: "#F5DEB3",
            darkVibrant: "#5C4033",
            muted: "#8B6914",
            lightMuted: "#B87333",
            darkMuted: "rgba(20, 15, 10, 0.9)",
        };

        const fontCSS = fontFamily
            ? `font-family: "${fontFamily}", sans-serif;`
            : "";
        const colorCSS = `
      :root {
        --vibrant: ${colorVars.vibrant};
        --lightVibrant: ${colorVars.lightVibrant};
        --darkVibrant: ${colorVars.darkVibrant};
        --muted: ${colorVars.muted};
        --lightMuted: ${colorVars.lightMuted};
        --darkMuted: ${colorVars.darkMuted};
      }
      html, body {
        margin: 0;
        padding: 0;
        background: transparent;
        overflow: hidden;
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

        const injection = `<style>${colorCSS}</style>${PREVIEW_RUNTIME}`;
        if (processed.includes("</head>")) {
            processed = processed.replace("</head>", `${injection}</head>`);
        } else if (processed.includes("</body>")) {
            processed = processed.replace("</body>", `${injection}</body>`);
        } else {
            processed += injection;
        }

        return processed;
    }
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
                <span class="header-subtitle"
                    >PIXEL-GLASS CUSTOMIZER // V.2.0</span
                >
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
                        <span class="panel-badge"
                            >{playerName.toUpperCase()}</span
                        >
                    </div>
                    <div class="preview-area">
                        <div class="preview-frame" style={previewStyle}>
                            {#if isCustomPlayer && htmlText}
                                <!-- Custom player preview via iframe -->
                                <iframe
                                    bind:this={editorIframeEl}
                                    title="Custom Player Preview"
                                    srcdoc={iframeSrcdoc}
                                    class="custom-preview-iframe"
                                    sandbox="allow-scripts allow-same-origin"
                                    on:load={sendColorsAndFont}
                                ></iframe>
                            {:else if playerComponent}
                                <svelte:component
                                    this={playerComponent}
                                    preview={false}
                                    showAlways={true}
                                />
                            {/if}
                        </div>
                    </div>
                </section>

                <!-- Right Panel: Controls -->
                <section class="controls-panel">
                    <!-- Top Row: Typography + Color — for ALL players -->
                    <div class="top-controls-row">
                        <!-- Typography -->
                        <div class="control-group typography-group">
                            <div class="control-header">
                                <span class="control-icon">A</span>
                                <span>TYPOGRAPHY</span>
                            </div>
                            <FontPicker bind:value={localFont} />
                            <button class="random-btn" on:click={randomizeFont}>
                                RANDOM
                            </button>
                        </div>

                        <!-- Color Mode -->
                        <div class="control-group">
                            <div class="control-header">
                                <span class="control-icon"></span>
                                <span>COLOR_SYNC</span>
                                {#if !isCustomPlayer}
                                    <button
                                        class="file-info-btn"
                                        on:click={showStylesPath}
                                        title="Open styles file location"
                                    >
                                        [EDIT FILE]
                                    </button>
                                {/if}
                            </div>
                            <ColorPicker
                                bind:mode={localColorMode}
                                bind:color={localStaticColor}
                            />
                        </div>
                    </div>

                    {#if isCustomPlayer}
                        <!-- Custom Player Snippets -->
                        <div class="custom-player-info">
                            <div class="info-header">
                                <span class="control-icon">&lt;/&gt;</span>
                                <span>SNIPPETS</span>
                                <span class="snippets-hint">click to copy</span>
                            </div>
                            <div class="snippets-grid">
                                {#each snippets as s}
                                    <button
                                        class="snippet-btn"
                                        class:copied={copiedSnippet === s.id}
                                        on:click={() => copySnippet(s)}
                                        title={s.desc}
                                    >
                                        <span class="snippet-label"
                                            >{s.label}</span
                                        >
                                        <span class="snippet-code"
                                            >{s.preview}</span
                                        >
                                    </button>
                                {/each}
                            </div>
                        </div>
                    {/if}

                    <!-- CSS/HTML Editor -->
                    <div class="control-group css-editor">
                        <div class="control-header css-control-header">
                            <span class="control-icon">&lt;/&gt;</span>
                            <span
                                >{isCustomPlayer
                                    ? "PLAYER.HTML"
                                    : "CONFIG.CSS"}</span
                            >
                            {#if isCustomPlayer}
                                <span class="custom-badge-small">CUSTOM</span>
                                <button
                                    class="file-info-btn edit-external-btn"
                                    on:click={openHTMLExternal}
                                    title="Open in external editor"
                                >
                                    [EDIT EXTERNAL]
                                </button>
                            {/if}
                        </div>
                        <div class="css-content">
                            <div class="line-numbers">
                                {#each (isCustomPlayer ? htmlText : cssText).split("\n") as _, i}
                                    <span>{String(i + 1).padStart(2, "0")}</span
                                    >
                                {/each}
                            </div>
                            {#if isCustomPlayer}
                                <textarea
                                    class="css-textarea"
                                    bind:value={htmlText}
                                    spellcheck="false"
                                    placeholder="<!-- Your custom HTML player code -->"
                                ></textarea>
                            {:else}
                                <textarea
                                    class="css-textarea"
                                    bind:value={cssText}
                                    spellcheck="false"
                                ></textarea>
                            {/if}
                        </div>
                    </div>

                    <!-- Preview Scale (Menu Size) -->
                    <div class="control-group scale-group">
                        <div class="control-header">
                            <span class="control-icon">⊡</span>
                            <span>MENU_SIZE</span>
                            <span class="scale-value"
                                >{Math.round(localPreviewScale * 100)}%</span
                            >
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
                </section>
            </main>

            <!-- Footer -->
            <footer class="editor-footer">
                <button class="footer-btn pig-btn" on:click={playPigSound}>
                    <span class="pig-emoji" class:bouncing={pigBouncing}
                        >🐷</span
                    >
                </button>

                <div class="footer-actions">
                    <button
                        class="footer-btn confirm"
                        on:click={(e) => {
                            triggerBtnAnim(e);
                            handleConfirm();
                        }}
                    >
                        CONFIRM
                    </button>
                    <button
                        class="footer-btn cancel"
                        on:click={(e) => {
                            triggerBtnAnim(e);
                            handleCancel();
                        }}
                    >
                        CANCEL
                    </button>
                </div>

                <button
                    class="footer-btn reset"
                    on:click={(e) => {
                        triggerBtnAnim(e);
                        handleReset();
                    }}
                >
                    Reset
                </button>
            </footer>
        </div>
    </div>
{/if}

<!-- Validation Error Dialog for Custom Players -->
<ValidationErrorDialog
    visible={showValidationErrors}
    errors={validationErrors}
    html={htmlText}
    onClose={() => {
        showValidationErrors = false;
        validationErrors = [];
    }}
/>

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
        background: linear-gradient(
            180deg,
            rgba(15, 15, 20, 0.98) 0%,
            rgba(10, 10, 15, 0.98) 100%
        );
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
        color: #b87333;
        font-family: "Press Start 2P", monospace;
        font-size: 1rem;
    }

    .header-title {
        font-family: "Press Start 2P", monospace;
        font-size: 0.7rem;
        font-weight: 400;
        color: white;
        letter-spacing: 0.02em;
    }

    .header-subtitle {
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: #b87333;
        letter-spacing: 0.05em;
    }

    .header-right {
        display: flex;
        align-items: center;
        gap: 1.5rem;
    }

    .header-tab {
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: rgba(255, 255, 255, 0.5);
        cursor: pointer;
        transition: color 0.2s;

        &:hover,
        &.active {
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
        font-family: "Press Start 2P", monospace;
        font-size: 0.45rem;
        color: #b87333;
    }

    .status-dot {
        width: 6px;
        height: 6px;
        background: #b87333;
        border-radius: 50%;
        animation: pulse 2s infinite;
    }

    @keyframes pulse {
        0%,
        100% {
            opacity: 1;
        }
        50% {
            opacity: 0.5;
        }
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
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: rgba(255, 255, 255, 0.5);
        letter-spacing: 0.02em;
    }

    .panel-badge {
        color: #b87333;
        font-family: "Press Start 2P", monospace;
        font-size: 0.45rem;
    }

    .preview-area {
        flex: 1;
        position: relative;
        overflow: hidden;
        background: rgba(10, 10, 15, 0.8);
        border: 1px solid rgba(255, 255, 255, 0.1);
        border-radius: 4px;
        padding: 2rem;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 2rem;

        // Color blobs with movement
        &::before {
            content: "";
            position: absolute;
            inset: -50%;
            z-index: 0;
            pointer-events: none;
            background:
                radial-gradient(
                    ellipse 150px 120px at 20% 30%,
                    var(--vibrant, #b87333) 0%,
                    transparent 70%
                ),
                radial-gradient(
                    ellipse 120px 150px at 80% 25%,
                    var(--lightVibrant, #d4944a) 0%,
                    transparent 70%
                ),
                radial-gradient(
                    ellipse 180px 100px at 70% 75%,
                    var(--site-accent, #b87333) 0%,
                    transparent 70%
                ),
                radial-gradient(
                    ellipse 100px 130px at 25% 70%,
                    var(--muted, #8b6914) 0%,
                    transparent 70%
                );
            opacity: 0.2;
            filter: blur(40px);
            animation: blobsMove 15s ease-in-out infinite alternate;
        }

        // Scanlines overlay
        &::after {
            content: "";
            position: absolute;
            inset: 0;
            z-index: 1;
            pointer-events: none;
            background: repeating-linear-gradient(
                0deg,
                transparent,
                transparent 2px,
                rgba(255, 255, 255, 0.01) 2px,
                rgba(255, 255, 255, 0.01) 4px
            );
        }
    }

    @keyframes blobsMove {
        0% {
            transform: translate(0, 0) rotate(0deg);
        }
        33% {
            transform: translate(10%, -5%) rotate(5deg);
        }
        66% {
            transform: translate(-5%, 10%) rotate(-3deg);
        }
        100% {
            transform: translate(5%, 5%) rotate(2deg);
        }
    }

    .preview-frame {
        position: relative;
        z-index: 2;
        transform: scale(1);
        /* Colors inherited from :root (Vibrant.js) or set via inline style (static mode) */
    }

    /* Center all direct children (both Svelte players and custom iframes) */
    .preview-frame :global(> *) {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
    }

    .custom-preview-iframe {
        width: 500px;
        height: 300px;
        border: none;
        background: transparent;
        border-radius: 8px;
    }

    // Controls Panel
    .controls-panel {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
    }

    .top-controls-row {
        display: flex;
        gap: 1rem;

        .control-group {
            flex: 1;
        }
    }

    .typography-group {
        display: flex;
        flex-direction: column;
    }

    .random-btn {
        width: 100%;
        flex: 1;
        margin-top: 0.5rem;
        padding: 0.5rem 1rem;
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: rgba(255, 255, 255, 0.5);
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.1);
        border-radius: 4px;
        cursor: pointer;
        transition:
            color 0.1s,
            background 0.1s,
            border-color 0.1s;
        letter-spacing: 0.15em;
        transform-origin: center center;

        &:hover {
            color: #b87333;
            background: rgba(184, 115, 51, 0.1);
            border-color: rgba(184, 115, 51, 0.3);
        }

        &:global(.pressing) {
            animation: btnSquish 0.2s ease-out forwards;
        }
    }

    @keyframes btnSquish {
        0% {
            transform: scale(1, 1);
        }
        35% {
            transform: scale(1.08, 0.85);
        }
        65% {
            transform: scale(0.92, 1.08);
        }
        100% {
            transform: scale(1, 1);
        }
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
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: rgba(255, 255, 255, 0.6);
        letter-spacing: 0.05em;
    }

    .control-icon {
        width: 20px;
        height: 20px;
        background: rgba(184, 115, 51, 0.2);
        border-radius: 2px;
        display: flex;
        align-items: center;
        justify-content: center;
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: #b87333;
    }

    .scale-value {
        margin-left: auto;
        font-family: "Press Start 2P", monospace;
        font-size: 0.45rem;
        color: #b87333;
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
                background: #b87333;
                border-radius: 2px;
                cursor: pointer;
                transition: all 0.2s;

                &:hover {
                    background: #d4944a;
                    transform: scale(1.1);
                }
            }

            &::-moz-range-thumb {
                width: 14px;
                height: 14px;
                background: #b87333;
                border: none;
                border-radius: 2px;
                cursor: pointer;
            }
        }
    }

    .edit-external-btn {
        margin-left: auto !important;
        background: rgba(184, 115, 51, 0.12) !important;
        border: 1px solid rgba(184, 115, 51, 0.6) !important;
        border-radius: 3px !important;
        padding: 0.3rem 0.7rem !important;
        font-family: "JetBrains Mono", monospace !important;
        font-size: 0.65rem !important;
        color: #d4944a !important;
        cursor: pointer;
        letter-spacing: 0.05em;
        transition: all 0.2s;

        &:hover {
            background: rgba(184, 115, 51, 0.25) !important;
            border-color: #b87333 !important;
            color: #f0b060 !important;
        }
    }

    .file-info-btn {
        margin-left: auto;
        background: none;
        border: 1px solid rgba(184, 115, 51, 0.3);
        border-radius: 2px;
        padding: 0.2rem 0.5rem;
        font-family: "Press Start 2P", monospace;
        font-size: 0.4rem;
        color: #b87333;
        cursor: pointer;
        transition: all 0.2s;

        &:hover {
            background: rgba(184, 115, 51, 0.15);
            border-color: #b87333;
        }
    }

    // Custom player info section
    .custom-player-info {
        background: rgba(184, 115, 51, 0.05);
        border: 1px solid rgba(184, 115, 51, 0.2);
        border-radius: 4px;
        padding: 1rem;
    }

    .info-header {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        color: #b87333;
        letter-spacing: 0.05em;
    }

    .snippets-hint {
        margin-left: auto;
        font-family: "JetBrains Mono", monospace;
        font-size: 0.6rem;
        color: rgba(255, 255, 255, 0.3);
        font-weight: 400;
        letter-spacing: 0;
    }

    .snippets-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 0.4rem;
    }

    .snippet-btn {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: 0.2rem;
        padding: 0.45rem 0.6rem;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 3px;
        cursor: pointer;
        transition: all 0.15s;
        text-align: left;

        &:hover {
            background: rgba(184, 115, 51, 0.1);
            border-color: rgba(184, 115, 51, 0.4);
        }

        &.copied {
            background: rgba(184, 115, 51, 0.2);
            border-color: #b87333;
        }
    }

    .snippet-label {
        font-family: "Press Start 2P", monospace;
        font-size: 0.38rem;
        color: rgba(255, 255, 255, 0.5);
        letter-spacing: 0.04em;

        .snippet-btn.copied & {
            color: #b87333;
        }
    }

    .snippet-code {
        font-family: "JetBrains Mono", monospace;
        font-size: 0.65rem;
        color: #b87333;
        opacity: 0.85;
    }

    .custom-badge-small {
        margin-left: auto;
        font-family: "JetBrains Mono", monospace;
        font-size: 0.5rem;
        font-weight: 600;
        color: #b87333;
        background: rgba(184, 115, 51, 0.15);
        border: 1px solid rgba(184, 115, 51, 0.3);
        padding: 0.15rem 0.4rem;
        border-radius: 2px;
        letter-spacing: 0.05em;
    }

    // CSS Editor
    .css-editor {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-height: 300px;
    }

    .css-control-header {
        margin-bottom: 0.75rem;
    }

    .css-content {
        flex: 1;
        display: flex;
        position: relative;
        border-radius: 8px;
        overflow: hidden;
        background: rgba(10, 10, 15, 0.6);
        backdrop-filter: blur(20px);
        border: 1px solid rgba(255, 255, 255, 0.08);

        &::before {
            content: "";
            position: absolute;
            inset: 0;
            z-index: 0;
            pointer-events: none;
            background:
                radial-gradient(
                    ellipse 120px 100px at 15% 20%,
                    var(--vibrant, #b87333) 0%,
                    transparent 70%
                ),
                radial-gradient(
                    ellipse 100px 120px at 85% 30%,
                    var(--lightVibrant, #d4944a) 0%,
                    transparent 70%
                ),
                radial-gradient(
                    ellipse 140px 80px at 60% 80%,
                    var(--site-accent, #b87333) 0%,
                    transparent 70%
                ),
                radial-gradient(
                    ellipse 80px 100px at 30% 70%,
                    var(--muted, #8b6914) 0%,
                    transparent 70%
                );
            opacity: 0.15;
            filter: blur(30px);
        }

        &::after {
            content: "";
            position: absolute;
            inset: 0;
            z-index: 1;
            pointer-events: none;
            background: linear-gradient(
                135deg,
                rgba(255, 255, 255, 0.03) 0%,
                transparent 50%,
                rgba(0, 0, 0, 0.05) 100%
            );
        }
    }

    .line-numbers {
        position: relative;
        z-index: 2;
        padding: 1rem 0.75rem;
        background: rgba(0, 0, 0, 0.2);
        border-right: 1px solid rgba(255, 255, 255, 0.08);
        display: flex;
        flex-direction: column;
        font-family: "JetBrains Mono", monospace;
        font-size: 0.7rem;
        line-height: 1.6;
        color: rgba(255, 255, 255, 0.25);
        user-select: none;
    }

    .css-textarea {
        position: relative;
        z-index: 2;
        flex: 1;
        padding: 1rem;
        background: transparent;
        border: none;
        resize: none;
        font-family: "JetBrains Mono", monospace;
        font-size: 0.8rem;
        line-height: 1.6;
        color: rgba(232, 212, 184, 0.85);
        text-shadow: 0 0 15px rgba(184, 115, 51, 0.15);
        outline: none;

        &::selection {
            background: rgba(184, 115, 51, 0.4);
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
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        font-weight: 400;
        letter-spacing: 0.05em;
        padding: 0.8rem 1.5rem;
        border: 1px solid;
        border-radius: 2px;
        cursor: pointer;
        transition:
            color 0.2s,
            background 0.2s,
            border-color 0.2s;
        text-transform: uppercase;
        transform-origin: center center;

        &:global(.pressing) {
            animation: btnSquish 0.2s ease-out forwards;
        }
    }

    .footer-btn.pig-btn {
        background: transparent;
        border-color: rgba(255, 182, 193, 0.3);
        font-size: 1.5rem;
        padding: 0.5rem 1rem;

        &:hover {
            border-color: rgba(255, 182, 193, 0.6);
            background: rgba(255, 182, 193, 0.1);
        }
    }

    .pig-emoji {
        display: inline-block;

        &.bouncing {
            animation: pigBounce 0.3s ease-out;
        }
    }

    @keyframes pigBounce {
        0% {
            transform: translateY(0) scale(1);
        }
        40% {
            transform: translateY(-15px) scale(1.3);
        }
        100% {
            transform: translateY(0) scale(1);
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
        border-color: #b87333;
        color: #b87333;

        &:hover {
            background: rgba(184, 115, 51, 0.4);
            color: #d4944a;
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
