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

<style lang="scss">
/* Scoped to .player-BigHead so styles don't conflict with other players */
:global(.player-BigHead) {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

:global(.player-BigHead .mainDiv) {
  display: flex;
  align-items: center;
  gap: 0;
  max-width: 100%;
}

:global(.player-BigHead .picDiv) {
  width: 9.5rem;
  height: 9.5rem;
  flex: 0 0 9.5rem;
  overflow: hidden;
  border: 0.2rem solid var(--lightMuted);
  border-radius: 1rem;
  z-index: 2;
}

:global(.player-BigHead .pic) {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

:global(.player-BigHead .textDiv) {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  width: 20rem;
  height: 6.5rem;
  padding: 0.8rem 0rem;
  background: var(--darkMuted);
  border-radius: 0 1rem 1rem 0;
  border: 0.2rem solid var(--vibrant);
  border-left: none;
  margin-left: -0.2rem;
  z-index: 1;
}

:global(.player-BigHead .title),
:global(.player-BigHead .artist) {
  display: flex;
  justify-content: center;
  align-items: center;
  margin: 0;
  line-height: 1.2;
  color: var(--lightVibrant);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

:global(.player-BigHead .title) {
  font-size: 1.8rem;
  margin-bottom: 0.3rem;
}

:global(.player-BigHead .artist) {
  font-size: 1.3rem;
}
</style>
