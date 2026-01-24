<script>
  //necessary data for player
  import { title, artist, thumbnail, ShowTrack} from '$lib/stores/stores.js';
  import { marquee } from '$lib/marquee.js';

  //styles
  import { fly } from 'svelte/transition';
</script>

{#if $ShowTrack}
  {#key `${$title}-${$artist}-${$thumbnail}`}
    <div class="mainDiv"
    		in:fly|global={{ x: -50, duration: 400, opacity: 0 }}
  		 out:fly|global={{ x:  50, duration: 400, opacity: 0 }}>
      <div class="mainDivGlow"></div>
        <div class="textDiv" style="background-image: url('{$thumbnail}'">
            <div class="blurDiv"></div>
            <h2 use:marquee={{speed:70}} class="title">{$title}</h2>
            <h3 use:marquee={{speed:50}} class="artist">{$artist}</h3>

        </div>
    </div>
  {/key}
{/if}

<style lang="scss">
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

.mainDiv {
  position: relative;
  display: flex;
  align-items: stretch;
  max-width: 100%;
  width: 10rem;
  height: 10rem;
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
  border-radius: 1rem;
  border: 0.2rem solid var(--lightMuted);
  background-size: cover;
  background-position: center;
  overflow: hidden;
  z-index: 1;
}

.title {
  flex: 3;
  padding: 0.5rem;
  font-size: 1.5rem;
}

.artist {
  flex: 2;
  padding: 0.3rem;
  font-size: 1.5rem;
}

.title,
.artist {
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