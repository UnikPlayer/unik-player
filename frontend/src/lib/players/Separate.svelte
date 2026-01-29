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
    <div class="player-Separate">
      <div class="mainDiv"
          in:fly|global={flyIn}
          out:fly|global={flyOut}>
          <div class="picDiv">
              <img class="pic" src={displayThumbnail} alt="">
          </div>
          <div class="textDiv">
              <div class="titleDiv">
                  <h2 use:marquee={{speed:70}} class="title">{displayTitle}</h2>
              </div>

              <div class="artistDiv">
                  <h3 use:marquee={{speed:50}} class="artist">{displayArtist}</h3>
              </div>
          </div>
      </div>
    </div>
  {/key}
{/if}

<style lang="scss">
/* Scoped to .player-Separate so styles don't conflict with other players */
:global(.player-Separate) {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

:global(.player-Separate .mainDiv) {
  display: flex;
  align-items: stretch;
  gap: 0.6rem;
  max-width: 100%;
}

:global(.player-Separate .pic) {
  display: block;
  border-radius: 1rem;
  border: 0.2rem solid var(--lightMuted);
  width: 10rem;
  height: 10rem;
  object-fit: cover;
  flex: 0 0 10rem;
}

:global(.player-Separate .textDiv) {
  display: flex;
  flex-direction: column;
  flex: 1;
  width: 20rem;
  gap: 0.6rem;
}

:global(.player-Separate .titleDiv) {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  border: 0.2rem solid var(--vibrant);
  background-color: var(--darkMuted);
  flex: 4;
  padding: 0.8rem 0rem;
}

:global(.player-Separate .artistDiv) {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  border: 0.2rem solid var(--vibrant);
  background-color: var(--darkMuted);
  flex: 2;
  padding: 0.6rem 0rem;
}

:global(.player-Separate .title),
:global(.player-Separate .artist) {
  display: flex;
  justify-content: center;
  align-items: center;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--lightVibrant);
  margin: 0;
  line-height: 1.2;
}

:global(.player-Separate .title) {
  font-size: 1.8rem;
}

:global(.player-Separate .artist) {
  font-size: 1.3rem;
}
</style>