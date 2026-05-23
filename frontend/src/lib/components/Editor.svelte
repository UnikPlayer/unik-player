<script>
    import { fly, fade } from "svelte/transition";
    import { onMount, tick } from "svelte";
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
    import { startGuide } from '$lib/stores/guide';
    import GuideOverlay from './GuideOverlay.svelte';

    // Backend API base URL - for dev mode
    const isBrowser = typeof window !== "undefined";
    const API_BASE =
        isBrowser && window.location.port === "5173"
            ? "http://localhost:5173"
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

    // Cloud canvas system
    let edCloudCv = null;
    let edCloudWrap = null;
    let edBlobs = null;
    let edMx = -9999, edMy = -9999;

    class EdCloudBlob {
        constructor(x,y,r){
            this.hx=x;this.hy=y;this.r=r;this.dx=0;this.dy=0;this.vx=0;this.vy=0;
            this.wph1=Math.random()*Math.PI*2;this.wph2=Math.random()*Math.PI*2;
            this.wamp=0.15+Math.random()*0.2;
        }
        update(mx,my,t){
            const wx=Math.sin(t*.008+this.wph1)*this.wamp;
            const wy=Math.cos(t*.006+this.wph2)*this.wamp*0.7;
            const px=this.hx+this.dx,py=this.hy+this.dy;
            const ex=px-mx,ey=py-my,d=Math.sqrt(ex*ex+ey*ey)+.001;
            const zone=this.r*4;
            if(d<zone){const f=Math.pow(1-d/zone,1.5)*3.5;this.vx+=(ex/d)*f;this.vy+=(ey/d)*f;}
            this.vx+=-(this.dx-wx)*.08;this.vy+=-(this.dy-wy)*.08;
            this.vx*=.78;this.vy*=.78;this.dx+=this.vx;this.dy+=this.vy;
        }
    }

    function makeEditorBlobs(WS,HS,padS){
        const b=[],cx=WS/2+padS,cy=HS/2+padS;
        // Dense grid — guarantees cloud behind every pixel of content
        const spheres=[];
        // Fill a rectangular grid spanning the full editor area
        const cols=7, rows=7;
        for(let iy=0;iy<rows;iy++){
            for(let ix=0;ix<cols;ix++){
                const gx=(ix/(cols-1)-0.5)*0.88;
                const gy=(iy/(rows-1)-0.5)*0.88;
                // Larger blobs in center, smaller at edges
                const dist=Math.sqrt(gx*gx+gy*gy);
                const r=0.10+0.08*(1-dist);
                const n=8+Math.round(6*(1-dist));
                spheres.push({x:gx,y:gy,r,n});
            }
        }
        // Extra bumps at edges for organic cloud shape
        for(let i=0;i<20;i++){
            const a=Math.PI*2*i/20;
            spheres.push({x:Math.cos(a)*0.46,y:Math.sin(a)*0.46,r:0.08,n:6});
        }
        const SCALE=0.95;
        for(const sp of spheres){
            const scx=cx+sp.x*WS*SCALE,scy=cy+sp.y*HS*SCALE;
            const sr=sp.r*Math.min(WS,HS)*SCALE;
            for(let i=0;i<sp.n;i++){
                const a=Math.random()*Math.PI*2,dist=Math.pow(Math.random(),.55);
                b.push(new EdCloudBlob(scx+Math.cos(a)*sr*dist,scy+Math.sin(a)*sr*dist, 3+Math.random()*5));
            }
        }
        return b;
    }

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
            const soundFiles = [
                "/sounds/pig1.mp3", // Assuming pig sounds are named pig1.mp3, pig2.mp3, etc.
                "/sounds/PigOink1.ogg",
                "/sounds/PigOink2.ogg",
                "/sounds/PigOink3.ogg",
                // Add more pig sound file paths here
            ];
            const randomSound = soundFiles[Math.floor(Math.random() * soundFiles.length)];
            pigAudio = new Audio(randomSound);
            pigAudio.volume = 0.4;
            pigAudio.addEventListener('ended', () => {
                pigAudio = null; // Reset audio object after playing
            });
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

    function speakText(text) {
        if ('speechSynthesis' in window) {
            const utterance = new SpeechSynthesisUtterance(text);
            speechSynthesis.speak(utterance);
        } else {
            console.warn('Text-to-speech not supported in this browser.');
        }
    }
    function setVolume(event) {
        if (pigAudio) {
            pigAudio.volume = event.target.value / 100;
        }
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
        edBlobs = null;
        edCanvasReady = false;
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

    // Rebuild srcdoc only when HTML content changes
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
                vibrant: "#555555",
                lightVibrant: "#cccccc",
                darkVibrant: "#222222",
                muted: "#888888",
                lightMuted: "#aaaaaa",
                darkMuted: "rgba(10, 10, 10, 0.9)",
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
            'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="100"%3E%3Crect fill="%23111111" width="100" height="100"/%3E%3Ctext x="50" y="55" text-anchor="middle" fill="%23ffffff" font-size="14"%3EDEMO%3C/text%3E%3C/svg%3E';

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
            vibrant: "#555555",
            lightVibrant: "#cccccc",
            darkVibrant: "#222222",
            muted: "#888888",
            lightMuted: "#aaaaaa",
            darkMuted: "rgba(10, 10, 10, 0.9)",
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

    // Cloud canvas rendering
    const ED_S = 8;
    const ED_PAD = 200;
    let edCanvasReady = false;
    const _edOff1 = typeof document !== 'undefined' ? document.createElement('canvas') : null;
    const _edOff2 = typeof document !== 'undefined' ? document.createElement('canvas') : null;
    const _edOff3 = typeof document !== 'undefined' ? document.createElement('canvas') : null;

    onMount(() => {
        const cs = getComputedStyle(document.documentElement);
        function parseCSSColor(varName){
            const v=cs.getPropertyValue(varName).trim();
            if(!v)return[0,0,0];
            if(v.startsWith('#')){
                const h=v.replace('#','');
                if(h.length===3)return[parseInt(h[0]+h[0],16),parseInt(h[1]+h[1],16),parseInt(h[2]+h[2],16)];
                return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];
            }
            const m=v.match(/(\d+)/g);
            return m?[+m[0],+m[1],+m[2]]:[0,0,0];
        }
        const cCloud=parseCSSColor('--c-cloud');
        const cOutline=parseCSSColor('--c-cloud-outline');

        function renderEdCloud(canvas, t){
            if(!canvas || !edCloudWrap) return;
            const el = edCloudWrap.querySelector('.editor-container');
            if(!el) return;
            const ew=el.offsetWidth, eh=el.offsetHeight;
            if(ew<2||eh<2) return;

            if(!edCanvasReady){
                canvas.style.left=-ED_PAD+'px';
                canvas.style.top=-ED_PAD+'px';
                canvas.style.width=(ew+ED_PAD*2)+'px';
                canvas.style.height=(eh+ED_PAD*2)+'px';
                edCanvasReady=true;
            }

            const TW=Math.ceil((ew+ED_PAD*2)/ED_S), TH=Math.ceil((eh+ED_PAD*2)/ED_S);
            const WS=Math.ceil(ew/ED_S), HS=Math.ceil(eh/ED_S);
            const padS=Math.ceil(ED_PAD/ED_S);

            if(!edBlobs) edBlobs=makeEditorBlobs(WS,HS,padS);

            if(_edOff1.width!==TW||_edOff1.height!==TH){_edOff1.width=TW;_edOff1.height=TH;}
            if(_edOff2.width!==TW||_edOff2.height!==TH){_edOff2.width=TW;_edOff2.height=TH;}
            if(_edOff3.width!==TW||_edOff3.height!==TH){_edOff3.width=TW;_edOff3.height=TH;}
            if(canvas.width!==TW||canvas.height!==TH){canvas.width=TW;canvas.height=TH;}

            const c1=_edOff1.getContext('2d');
            // Normal pass — blobs at normal size
            c1.clearRect(0,0,TW,TH);
            for(const bl of edBlobs){
                const bx=bl.hx+bl.dx,by=bl.hy+bl.dy;
                const g=c1.createRadialGradient(bx,by,0,bx,by,bl.r);
                g.addColorStop(0,'rgba(255,255,255,1)');
                g.addColorStop(.5,'rgba(255,255,255,.85)');
                g.addColorStop(1,'rgba(255,255,255,0)');
                c1.fillStyle=g;c1.beginPath();c1.arc(bx,by,bl.r,0,Math.PI*2);c1.fill();
            }
            const c2=_edOff2.getContext('2d');
            c2.clearRect(0,0,TW,TH);
            c2.filter='blur(2.5px)';c2.drawImage(_edOff1,0,0);c2.filter='none';
            const imgN=c2.getImageData(0,0,TW,TH);

            // Grown pass — slightly larger blobs for outline
            c1.clearRect(0,0,TW,TH);
            for(const bl of edBlobs){
                const bx=bl.hx+bl.dx,by=bl.hy+bl.dy,br=bl.r+1;
                const g=c1.createRadialGradient(bx,by,0,bx,by,br);
                g.addColorStop(0,'rgba(255,255,255,1)');
                g.addColorStop(.5,'rgba(255,255,255,.85)');
                g.addColorStop(1,'rgba(255,255,255,0)');
                c1.fillStyle=g;c1.beginPath();c1.arc(bx,by,br,0,Math.PI*2);c1.fill();
            }
            const c3=_edOff3.getContext('2d');
            c3.clearRect(0,0,TW,TH);
            c3.filter='blur(2.5px)';c3.drawImage(_edOff1,0,0);c3.filter='none';
            const imgG=c3.getImageData(0,0,TW,TH);

            // Two-pass threshold: outline = grown && !normal
            const dN=imgN.data,dG=imgG.data;
            const out=c2.createImageData(TW,TH);
            const od=out.data;
            for(let i=0;i<dN.length;i+=4){
                const vN=dN[i],vG=dG[i];
                const inNorm=vN>55,inGrown=vG>55;
                if(inGrown&&!inNorm){od[i]=cOutline[0];od[i+1]=cOutline[1];od[i+2]=cOutline[2];od[i+3]=255;}
                else if(inNorm){od[i]=cCloud[0];od[i+1]=cCloud[1];od[i+2]=cCloud[2];od[i+3]=255;}
            }
            c2.putImageData(out,0,0);
            const dctx=canvas.getContext('2d');
            dctx.imageSmoothingEnabled=false;
            dctx.clearRect(0,0,TW,TH);
            dctx.drawImage(_edOff2,0,0,TW,TH);
        }

        function onEdMM(e){
            if(!edCloudCv) return;
            const r=edCloudCv.getBoundingClientRect();
            if(r.width<1) return;
            edMx=(e.clientX-r.left)/r.width*edCloudCv.width;
            edMy=(e.clientY-r.top)/r.height*edCloudCv.height;
        }
        function onEdML(){ edMx=-9999;edMy=-9999; }
        window.addEventListener('mousemove',onEdMM);
        window.addEventListener('mouseleave',onEdML);

        let raf,last=0,edTick=0;
        function frame(ts){
            raf=requestAnimationFrame(frame);
            if(!$editorOpen){ edCanvasReady=false; edBlobs=null; return; }
            if(ts-last<16)return;last=ts;edTick++;
            if(edBlobs) for(const b of edBlobs) b.update(edMx,edMy,edTick);
            renderEdCloud(edCloudCv,edTick);
        }
        raf=requestAnimationFrame(frame);

        return ()=>{
            cancelAnimationFrame(raf);
            window.removeEventListener('mousemove',onEdMM);
            window.removeEventListener('mouseleave',onEdML);
            _edOff1.width = _edOff1.height = 0;
            _edOff2.width = _edOff2.height = 0;
            _edOff3.width = _edOff3.height = 0;
            if (edCloudCv) edCloudCv.width = edCloudCv.height = 0;
            edBlobs = null;
        };
    });
</script>

{#if $editorOpen}
    <div class="editor-overlay" transition:fade={{ duration: 200 }}>
        <div class="editor-cloud-wrap" bind:this={edCloudWrap}>
            <canvas bind:this={edCloudCv} class="editor-cloud-cv"></canvas>
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
                        <span>PREVIEW</span>
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
                                <span class="control-icon code-icon">&lt;/&gt;</span>
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
                            <span class="control-icon code-icon">&lt;/&gt;</span>
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
                            {:else}
                                <button
                                    class="file-info-btn"
                                    on:click={showStylesPath}
                                    title="Open styles file location"
                                >
                                    [EDIT FILE]
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
                <button class="footer-btn" on:click={() => speakText('Hello, world!')}>
                    TTS
                </button>
                <input type="range" min="0" max="100" on:input={setVolume}>
                <button class="footer-btn" on:click={() => speakText('Hello, world!')}>
                    TTS
                </button>
                <button class="footer-btn" on:click={() => {
                    speakText('Starting the guide.');
                    // TODO: Implement guide start
                }}>
                    Guide
                </button>
            </footer>
        </div>
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
        background: var(--c-backdrop, rgba(0, 0, 0, 0.65));
        backdrop-filter: blur(8px);
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 2rem;
    }

    .editor-cloud-wrap {
        position: relative;
        width: 100%;
        max-width: 1400px;
        height: 90vh;
        max-height: 900px;
    }

    .editor-cloud-cv {
        position: absolute;
        image-rendering: pixelated;
        display: block;
        pointer-events: none;
        z-index: 0;
    }

    .editor-container {
        position: relative;
        z-index: 1;
        width: 100%;
        height: 100%;
        background: transparent;
        display: flex;
        flex-direction: column;
        overflow: hidden;
    }

    // Header
    .editor-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1rem 1.5rem;
    }

    .header-left {
        display: flex;
        align-items: center;
        gap: 0.75rem;
    }

    .header-icon {
        color: var(--c1);
        font-size: 1rem;
    }

    .header-title {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        font-weight: 400;
        color: var(--c1);
        letter-spacing: 0.04em;
    }

    .header-subtitle {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: rgba(0, 0, 0, 0.7);
        letter-spacing: 0.06em;
    }

    .header-right {
        display: flex;
        align-items: center;
        gap: 1.5rem;
    }

    .header-tab {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: rgba(0, 0, 0, 0.6);
        cursor: pointer;
        transition: color 0.2s;

        &:hover,
        &.active {
            color: var(--c1);
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
        border: 3px solid var(--c1);
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: rgba(0, 0, 0, 0.7);
    }

    .status-dot {
        width: 6px;
        height: 6px;
        background: var(--c1);
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
        background: transparent;
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
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
        letter-spacing: 0.04em;
    }

    .panel-badge {
        color: var(--c1);
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
    }

    .preview-area {
        flex: 1;
        position: relative;
        overflow: hidden;
        background: transparent;
        padding: 2rem;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 2rem;
    }

    .preview-frame {
        position: relative;
        z-index: 2;
        transform: scale(1);
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
    }

    // Controls Panel
    .controls-panel {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
        scrollbar-width: none;

        &::-webkit-scrollbar { display: none; }
    }

    .top-controls-row {
        display: flex;
        gap: 1rem;
        align-items: stretch;

        .control-group {
            flex: 1;
            display: flex;
            flex-direction: column;
            max-width:300px;
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
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
        background: transparent;
        border: 3px solid var(--c1);
        cursor: pointer;
        transition:
            color 0.1s,
            background 0.1s,
            border-color 0.1s;
        letter-spacing: 0.1em;
        transform-origin: center center;

        &:hover {
            color: var(--c1);
            background: rgba(0, 0, 0, 0.06);
            border-color: var(--c1);
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
        background: transparent;
        border: 3px solid var(--c1);
        padding: 1rem;
    }

    .control-header {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
        letter-spacing: 0.06em;
    }

    .control-icon {
        width: 24px;
        height: 24px;
        background: rgba(0, 0, 0, 0.08);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1rem;
        color: var(--c1);
    }

    .control-icon.code-icon {
        font-family: 'Press Start 2P', monospace;
        font-size: 0.55rem;
        letter-spacing: 0;
    }

    .scale-value {
        margin-left: auto;
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
    }

    .scale-slider {
        padding: 0.5rem 0;

        input[type="range"] {
            width: 100%;
            height: 4px;
            appearance: none;
            background: rgba(0, 0, 0, 0.1);
            cursor: pointer;

            &::-webkit-slider-thumb {
                appearance: none;
                width: 14px;
                height: 14px;
                background: var(--c1);
                cursor: pointer;
                transition: all 0.2s;

                &:hover {
                    background: #333;
                    transform: scale(1.1);
                }
            }

            &::-moz-range-thumb {
                width: 14px;
                height: 14px;
                background: var(--c1);
                border: none;
                cursor: pointer;
            }
        }
    }

    .edit-external-btn {
        margin-left: auto !important;
        background: transparent !important;
        border: 3px solid var(--c1) !important;
        padding: 0.3rem 0.7rem !important;
        font-family: '8bitwonder', monospace !important;
        font-size: 1rem !important;
        color: var(--c1) !important;
        cursor: pointer;
        letter-spacing: 0.05em;
        transition: all 0.2s;

        &:hover {
            background: rgba(0, 0, 0, 0.06) !important;
            border-color: var(--c1) !important;
        }
    }

    .file-info-btn {
        margin-left: auto;
        background: none;
        border: 3px solid var(--c1);
        padding: 0.2rem 0.5rem;
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
        cursor: pointer;
        transition: all 0.2s;

        &:hover {
            background: rgba(0, 0, 0, 0.06);
            border-color: var(--c1);
        }
    }

    .custom-player-info {
        background: transparent;
        border: 3px solid var(--c1);
        padding: 1rem;
    }

    .info-header {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
        letter-spacing: 0.06em;
    }

    .snippets-hint {
        margin-left: auto;
        font-family: 'Rubik', sans-serif;
        font-size: 1rem;
        color: rgba(0, 0, 0, 0.5);
        font-weight: 400;
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
        background: transparent;
        border: 3px solid var(--c1);
        cursor: pointer;
        transition: all 0.15s;
        text-align: left;

        &:hover {
            background: rgba(0, 0, 0, 0.06);
            border-color: var(--c1);
        }

        &.copied {
            background: rgba(0, 0, 0, 0.06);
            border-color: var(--c1);
        }
    }

    .snippet-label {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: rgba(0, 0, 0, 0.6);
        letter-spacing: 0.04em;

        .snippet-btn.copied & {
            color: var(--c1);
        }
    }

    .snippet-code {
        font-family: 'JetBrains Mono', monospace;
        font-size: 1rem;
        color: var(--c1);
    }

    .custom-badge-small {
        margin-left: auto;
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: var(--c1);
        background: rgba(0, 0, 0, 0.06);
        border: 3px solid var(--c1);
        padding: 0.15rem 0.4rem;
        letter-spacing: 0.05em;
    }

    // CSS Editor
    .css-editor {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-height: 900px;
    }

    .css-control-header {
        margin-bottom: 0.75rem;
    }

    .css-content {
        flex: 1;
        display: flex;
        position: relative;
        overflow: hidden;
        background: #000;
        border: 3px solid var(--c1);
    }

    .line-numbers {
        position: relative;
        z-index: 2;
        padding: 1rem 0.75rem;
        background: #000;
        border-right: 3px solid var(--c1);
        display: flex;
        flex-direction: column;
        font-family: 'JetBrains Mono', monospace;
        font-size: 1rem;
        line-height: 1.6;
        color: #555;
        user-select: none;
    }

    .css-textarea {
        position: relative;
        z-index: 2;
        flex: 1;
        padding: 1rem;
        background: #000;
        border: none;
        resize: none;
        font-family: 'JetBrains Mono', monospace;
        font-size: 1rem;
        line-height: 1.6;
        color: #fff;
        outline: none;

        &::selection {
            background: #444;
        }
    }

    // Footer — transparent, no bar
    .editor-footer {
        display: grid;
        grid-template-columns: 1fr auto 1fr;
        align-items: center;
        padding: 1rem 1.5rem;

        > .pig-btn { justify-self: start; }
        > .footer-actions { justify-self: center; }
        > .reset { justify-self: end; }
    }

    .footer-btn {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        letter-spacing: 0.06em;
        padding: 0.8rem 1.5rem;
        border: 3px solid;
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
        border-color: var(--c1);
        font-size: 1.5rem;
        padding: 0.5rem 1rem;

        &:hover {
            border-color: var(--c1);
            background: rgba(0, 0, 0, 0.04);
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
        border-color: var(--c1);
        color: var(--c1);

        &:hover {
            background: rgba(0, 0, 0, 0.04);
            border-color: var(--c1);
        }
    }

    .footer-btn.confirm {
        background: rgba(0, 0, 0, 0.06);
        border-color: var(--c1);
        color: var(--c1);

        &:hover {
            background: rgba(0, 0, 0, 0.12);
            color: #000;
        }
    }

    .footer-btn.reset {
        background: transparent;
        border-color: var(--c1);
        color: var(--c1);

        &:hover {
            background: rgba(0, 0, 0, 0.04);
            border-color: var(--c1);
        }
    }
</style>
