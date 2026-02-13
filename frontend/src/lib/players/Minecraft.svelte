<script context="module">
    // Метаданные плеера для auto-discovery
    export const meta = {
        name: "Minecraft",
        defaultCSS: `
        /* Colors: var(--vibrant), var(--lightVibrant),
           var(--darkVibrant), var(--muted),
           var(--lightMuted), var(--darkMuted) */

    @font-face {
        font-family: 'minecraft';
        src: url('/fonts/minecraft.ttf') format('truetype');
        font-weight: 400;
        font-style: normal;
        font-display: swap;
	}

        .title > *, .artist > * {
          font-family: minecraft
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
          border-radius: 0rem;
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
          font-size: 2.8rem;
        }

        .artist {
          flex: 2;
          padding: 0.8rem 1rem;
        }

        .artist > * {
          font-size: 2.6rem;
        }`,
    };
</script>

<script>
    //necessary data for player
    import { title, artist, thumbnail, ShowTrack } from "$lib/stores/stores.js";
    import { marquee } from "$lib/marquee.js";

    //styles
    import { fly } from "svelte/transition";

    // Preview mode props
    export let preview = false;
    export let showAlways = false;

    // Demo data for fallback
    const demoTitle = "Midnight City";
    const demoArtist = "M83";
    const demoThumbnail =
        'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%231a1a2e" width="300" height="300"/%3E%3Ctext x="150" y="160" text-anchor="middle" fill="%23B87333" font-size="40" font-family="sans-serif"%3EDEMO%3C/text%3E%3C/svg%3E';
    // Black placeholder when no thumbnail available
    const blackPlaceholder =
        'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%23000000" width="300" height="300"/%3E%3C/svg%3E';

    // Use real data if available, fallback to demo
    $: hasRealData = $title !== null;
    $: displayTitle = preview ? demoTitle : $title || demoTitle;
    $: displayArtist = preview ? demoArtist : $artist || demoArtist;
    // Black placeholder only when real track has no image, DEMO when no data
    $: displayThumbnail = preview
        ? demoThumbnail
        : hasRealData
          ? $thumbnail || blackPlaceholder
          : demoThumbnail;
    $: shouldShow = preview || showAlways || $ShowTrack;

    // Animation config
    const flyIn = { x: -50, duration: 400, opacity: 0 };
    const flyOut = { x: 50, duration: 400, opacity: 0 };
</script>

{#if shouldShow}
    {#key `${displayTitle}-${displayArtist}`}
        <div class="player-Minecraft">
            <div class="mainDiv" in:fly|global={flyIn} out:fly|global={flyOut}>
                <div class="mainDivGlow"></div>
                <!-- background here, becouse i use variable from svelte store. css can't reach scipts -->
                <div
                    class="textDiv"
                    style="background-image: url('{displayThumbnail}');"
                >
                    <h2 use:marquee={{ speed: 70, optGap: 69 }} class="title">
                        {displayTitle}
                    </h2>
                    <h3 use:marquee={{ speed: 50, optGap: 69 }} class="artist">
                        {displayArtist}
                    </h3>

                    <div class="blurDiv"></div>
                </div>
            </div>
        </div>
    {/key}
{/if}
