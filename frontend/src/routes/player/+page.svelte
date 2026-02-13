<script>
    import { getPickedPlayer, getPlayerMeta } from "$lib/getPlayers.js";
    import { onMount } from "svelte";
    import {
        title as trackTitle,
        artist as trackArtist,
        thumbnail as trackThumbnail,
    } from "$lib/stores/stores.js";
    import { generateColorVars } from "$lib/utils/colors.js";
    import {
        transformCSS,
        injectCSS,
        loadCSSFromBackend,
    } from "$lib/utils/playerCSS.js";

    let pickedPlayer = [];
    let playerName = "";
    let savedStyle = {};
    let dynamicColors = {};

    // Read current CSS variables from :root (set by Vibrant.js in convertToHex.js)
    function readDynamicColors() {
        if (typeof document === "undefined") return {};
        const s = getComputedStyle(document.documentElement);
        return {
            vibrant: s.getPropertyValue("--vibrant").trim() || "#D4944A",
            lightVibrant:
                s.getPropertyValue("--lightVibrant").trim() || "#F5DEB3",
            darkVibrant:
                s.getPropertyValue("--darkVibrant").trim() || "#5C4033",
            muted: s.getPropertyValue("--muted").trim() || "#8B6914",
            lightMuted: s.getPropertyValue("--lightMuted").trim() || "#B87333",
            darkMuted:
                s.getPropertyValue("--darkMuted").trim() ||
                "rgba(20, 15, 10, 0.9)",
        };
    }

    // Re-read dynamic colors when thumbnail changes (Vibrant.js extracts colors from it)
    // Small delay to let Vibrant.js finish setting CSS vars
    $: if ($trackThumbnail) {
        setTimeout(() => {
            dynamicColors = readDynamicColors();
        }, 300);
    }

    // Reactive: compute inline styles from savedStyle
    $: useStaticColor = savedStyle.colorMode === "static";
    $: staticColorValue = savedStyle.staticColor || "#B87333";
    $: colors = useStaticColor
        ? generateColorVars(staticColorValue)
        : dynamicColors;
    $: fontFamily = savedStyle.font || "Rubik";

    $: inlineStyle =
        useStaticColor && colors
            ? `
            --vibrant: ${colors.vibrant};
            --lightVibrant: ${colors.lightVibrant};
            --darkVibrant: ${colors.darkVibrant};
            --muted: ${colors.muted};
            --lightMuted: ${colors.lightMuted};
            --darkMuted: ${colors.darkMuted};
            font-family: "${fontFamily}", sans-serif;
      `
            : `font-family: "${fontFamily}", sans-serif;`;
    // Note: in dynamic mode, CSS vars on :root are set by convertToHex.js (Vibrant.js)
    // and inherited by Svelte player components. Custom players in iframes get colors
    // via the `colors` prop + postMessage.

    onMount(async () => {
        playerName = location.search.slice(1);
        console.log("[Player] Loading:", playerName);

        pickedPlayer = getPickedPlayer(playerName);

        // Transparent background for OBS
        document.documentElement.style.background = "transparent";
        document.body.style.background = "transparent";

        // Step 1: Load settings (colorMode, font, etc)
        try {
            const res = await fetch("/api/styles");
            if (res.ok) {
                const allStyles = await res.json();
                savedStyle = allStyles[playerName] || {};
            }
        } catch (err) {
            console.warn("[Player] Failed to load settings:", err);
        }

        // Step 2: Load CSS — user custom from backend, fallback to meta.defaultCSS
        try {
            let rawCSS = await loadCSSFromBackend(playerName);

            if (!rawCSS) {
                // No user CSS — use factory default from player meta
                const meta = getPlayerMeta(playerName);
                rawCSS = meta?.defaultCSS || "";
            }

            if (rawCSS) {
                const scoped = transformCSS(rawCSS, playerName);
                injectCSS(scoped, "unik-player-custom-css");
            }
        } catch (err) {
            console.error("[CSS] Error loading CSS:", err);
        }
    });
</script>

<!-- Inject fonts -->
<svelte:head>
    <!-- Google Fonts preconnect -->
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link
        rel="preconnect"
        href="https://fonts.gstatic.com"
        crossorigin="anonymous"
    />

    <!-- Load all available Google Fonts for player -->
    <link
        href="https://fonts.googleapis.com/css2?family=EB+Garamond:ital,wght@0,400..800;1,400..800&family=JetBrains+Mono:wght@400;500;600;700&family=Old+Standard+TT:ital,wght@0,400;0,700;1,400&family=Rubik:ital,wght@0,300..900;1,300..900&family=Yeseva+One&display=swap"
        rel="stylesheet"
    />
</svelte:head>

<div class="player-page">
    {#each pickedPlayer as { component, name, isCustom }}
        <div
            id="unik-player"
            class="player-container player-{name}"
            style={inlineStyle}
        >
            {#if isCustom}
                <svelte:component
                    this={component}
                    playerName={name}
                    title={$trackTitle || "Unknown Track"}
                    artist={$trackArtist || "Unknown Artist"}
                    thumbnail={$trackThumbnail || ""}
                    {colors}
                    font={fontFamily}
                />
            {:else}
                <svelte:component this={component} />
            {/if}
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
    }

    .player-container {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
    }

    /* Center all direct children (both Svelte players and custom wrappers) */
    .player-container :global(> *) {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
    }

    /* Apply font globally to player */
    .player-container :global(*) {
        font-family: inherit;
    }

    /* Custom player wrapper — explicit size so iframe has dimensions to fill */
    .player-container :global(.custom-player-wrapper) {
        width: 900px;
        height: 400px;
    }
</style>
