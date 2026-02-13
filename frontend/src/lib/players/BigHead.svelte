<script context="module">
  // Метаданные плеера для auto-discovery
  export const meta = {
    name: 'BigHead',
    defaultCSS: `/* === BIGHEAD PLAYER === */
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
  const demoThumbnail = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="300" height="300"%3E%3Crect fill="%231a1a2e" width="300" height="300"/%3E%3Ctext x="150" y="160" text-anchor="middle" fill="%23B87333" font-size="40" font-family="sans-serif"%3EDEMO%3C/text%3E%3C/svg%3E';
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
    <div class="player-BigHead">
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
