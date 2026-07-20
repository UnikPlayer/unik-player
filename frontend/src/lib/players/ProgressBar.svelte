<script context="module">
    export const meta = {
        name: "ProgressBar",
        defaultCSS: `
        /* === PROGRESSBAR PLAYER === */
/*               Colors: 
   var(--vibrant),      var(--muted),
   var(--lightVibrant), var(--darkVibrant),
   var(--lightMuted),   var(--darkMuted) 
*/

.title > *, .artist > * {
  font-family: "Unbounded", sans-serif;
}

.mainDiv {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.5rem;
  background: var(--darkVibrant);
  border: 0.12rem solid var(--vibrant);
  border-radius: 0.5rem;
  width: 20rem;
  height:6rem;
  box-sizing: border-box;
}

.pic {
  width: 5rem;
  height: 5rem;
  object-fit: cover;
  border-radius: 0.35rem;
  border: 0.1rem solid var(--vibrant);
  flex-shrink: 0;
}

.textDiv {
  display: flex;
  flex-direction: column;
  justify-content: center;
  flex: 1;
  min-width: 0;
  gap: 0.1rem;
}

.title, .artist {
  display: flex;
  align-items: center;
  margin: 0;
  line-height: 1.3;
  color: var(--lightVibrant);
  white-space: nowrap;
  overflow: hidden;
}

.title > * {
  font-size: 1.6rem;
  font-weight: 700;
}

.artist > * {
  font-size: 1.3rem;
  font-weight:300;
}`,
    };
</script>

<script>
    import { title, artist, thumbnail, ShowTrack } from "$lib/stores/stores.js";
    import ProgressBarComponent from "$lib/components/ProgressBar.svelte";
    import { marquee } from "$lib/marquee.js";
    import { fly } from "svelte/transition";

    export let preview = false;
    export let showAlways = false;

    const demoTitle = "Midnight City";
    const demoArtist = "M83";
    const demoThumbnail =
        'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%231a1a2e" width="300" height="300"/%3E%3Ctext x="150" y="160" text-anchor="middle" fill="%23B87333" font-size="40" font-family="sans-serif"%3EDEMO%3C/text%3E%3C/svg%3E';
    const blackPlaceholder =
        'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%23000000" width="300" height="300"/%3E%3C/svg%3E';

    $: hasRealData = $title !== null;
    $: displayTitle = preview ? demoTitle : $title || demoTitle;
    $: displayArtist = preview ? demoArtist : $artist || demoArtist;
    $: displayThumbnail = preview
        ? demoThumbnail
        : hasRealData
          ? $thumbnail || blackPlaceholder
          : demoThumbnail;
    $: shouldShow = preview || showAlways || $ShowTrack;

    const flyIn = { x: -50, duration: 400, opacity: 0 };
    const flyOut = { x: 50, duration: 400, opacity: 0 };
</script>

{#if shouldShow}
    {#key `${displayTitle}-${displayArtist}`}
        <div class="player-ProgressBar">
            <div class="mainDiv" in:fly|global={flyIn} out:fly|global={flyOut}>
                <img class="pic" src={displayThumbnail} alt="" />

                <div class="textDiv">
                    <h2 use:marquee={{ speed: 70 }} class="title">
                        {displayTitle}
                    </h2>
                    <h3 use:marquee={{ speed: 50 }} class="artist">
                        {displayArtist}
                    </h3>
                    <ProgressBarComponent
                        height="3px"
                        borderRadius="2px"
                        showTime={true}
                    />
                </div>
            </div>
        </div>
    {/key}
{/if}
