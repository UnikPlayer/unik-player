<script context="module">
  // Метаданные плеера для auto-discovery
  export const meta = {
    name: 'Generic',
    defaultCSS: `
    /* === GENERIC PLAYER === */
/*               Colors: 
   var(--vibrant),      var(--muted),
   var(--lightVibrant), var(--darkVibrant),
   var(--lightMuted),   var(--darkMuted) 
*/

.title > *, .artist > * {
  font-family: "Comfortaa", sans-serif;
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
  width: 10rem;
  height: 10rem;
  object-fit: cover;
  border-radius: 1rem;
  border: 0.2rem solid var(--vibrant);
}

.textDiv {
  display: flex;
  flex-direction: column;
  justify-content: space-around;

  width: 26rem;
  height: 10rem;

  margin:1.8rem 0 2rem -1rem;
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
  font-size: 2rem;
  font-weight:700
}

.artist > * {
  font-size: 1.6rem;
  font-weight:400
}`
  };
</script>

<script>
  //necessary data for player
  import { title, artist, thumbnail, ShowTrack} from '$lib/stores/stores.js';
  import { marquee } from '$lib/marquee.js';

  //styles
  import { fly } from 'svelte/transition';

  // Preview mode props
  export let preview = false;
  export let showAlways = false;

  // Demo data for fallback
  const demoTitle = 'Midnight City';
  const demoArtist = 'M83';
  const demoThumbnail = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%23111111" width="300" height="300"/%3E%3Ctext x="150" y="160" text-anchor="middle" fill="%23ffffff" font-size="40" font-family="sans-serif"%3EDEMO%3C/text%3E%3C/svg%3E';
  // Black placeholder when no thumbnail available
  const blackPlaceholder = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%23000000" width="300" height="300"/%3E%3C/svg%3E';

  // Use real data if available, fallback to demo
  $: hasRealData = $title !== null;
  $: displayTitle = preview ? demoTitle : ($title || demoTitle);
  $: displayArtist = preview ? demoArtist : ($artist || demoArtist);
  // Black placeholder only when real track has no image, DEMO when no data
  $: displayThumbnail = preview ? demoThumbnail : (hasRealData ? ($thumbnail || blackPlaceholder) : demoThumbnail);
  $: shouldShow = preview || showAlways || $ShowTrack;

  // Animation config
  const flyIn = { x: -50, duration: 400, opacity: 0 };
  const flyOut = { x: 50, duration: 400, opacity: 0 };
</script>

{#if shouldShow}
  {#key `${displayTitle}-${displayArtist}`}
    <div class="player-Generic">
      <div class="mainDiv"
        in:fly|global={flyIn}
        out:fly|global={flyOut}>
          <div class="picDiv">
              <img class="pic" src={displayThumbnail} alt="">
          </div>
          <div class="textDiv">

            <h2 use:marquee={{speed:70}} class="title">{displayTitle}</h2>
            <h3 use:marquee={{speed:50}} class="artist">{displayArtist}</h3>

          </div>
      </div>
    </div>
  {/key}
{/if}
