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

  // Use real data if available, fallback to demo
  $: displayTitle = preview ? demoTitle : ($title || demoTitle);
  $: displayArtist = preview ? demoArtist : ($artist || demoArtist);
  $: displayThumbnail = preview ? demoThumbnail : ($thumbnail || demoThumbnail);
  $: shouldShow = preview || showAlways || $ShowTrack;

  // Animation config
  const flyIn = { x: -50, duration: 400, opacity: 0 };
  const flyOut = { x: 50, duration: 400, opacity: 0 };
</script>


{#if shouldShow}
  {#key `${displayTitle}-${displayArtist}-${displayThumbnail}`}
    <div class="player-BackPicture">
      <div class="mainDiv"
          in:fly|global={flyIn}
          out:fly|global={flyOut}>
        <div class="mainDivGlow"></div>
        <!-- background here, becouse i use variable from svelte store. css can't reach scipts -->
        <div class="textDiv" style="background-image: url('{displayThumbnail}');">

          <h2 use:marquee={{speed:70, optGap:69}} class="title">{displayTitle}</h2>
          <h3 use:marquee={{speed:50, optGap:69}} class="artist">{displayArtist}</h3>

          <div class="blurDiv"></div>
        </div>
      </div>
    </div>
  {/key}
{/if}

<style lang="scss">
/* Scoped to .player-BackPicture so styles don't conflict with other players */
:global(.player-BackPicture) {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

:global(.player-BackPicture .blurDiv) {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  backdrop-filter: blur(8px);
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 1;
}

:global(.player-BackPicture .mainDiv) {
  display: flex;
  align-items: stretch;
  width: 18rem;
  max-width: 100%;
  height: 7.5rem;
}

:global(.player-BackPicture .mainDivGlow) {
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

:global(.player-BackPicture .textDiv) {
  position: relative;
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
  background-size: cover;
  background-position: center;
  border-radius: 1rem;
  border: 0.2rem solid ;
  z-index: 1;
}

:global(.player-BackPicture .title) {
  flex: 3;
  padding: 0.5rem 1rem;
  font-size: 1.8rem;
}

:global(.player-BackPicture .artist) {
  flex: 2;
  padding: 0.8rem 1rem;
  font-size: 1.6rem;
}

:global(.player-BackPicture .title),
:global(.player-BackPicture .artist) {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0;
  line-height: 1.2;
  color: var(--lightVibrant);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  z-index: 2;
}
</style>