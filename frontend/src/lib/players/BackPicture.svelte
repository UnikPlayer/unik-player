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
  		  out:fly|global={{x:  50, duration: 400, opacity: 0 }}>
      <div class="mainDivGlow"></div>
      <!-- background here, becouse i use variable from svelte store. css can't reach scipts -->
      <div class="textDiv" style="background-image: url('{$thumbnail}');">

        <h2 use:marquee={{speed:70, optGap:69}} class="title">{$title}</h2>
        <h3 use:marquee={{speed:50, optGap:69}} class="artist">{$artist}</h3>

        <div class="blurDiv"></div>
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
  width: 18rem;
  max-width: 100%;
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
  border-radius: 1rem;
  border: 0.2rem solid ;
  z-index: 1;
}

.title {
  flex: 3;
  padding: 0.5rem 1rem;
  font-size: 1.8rem;
}

.artist {
  flex: 2;
  padding: 0.8rem 1rem;
  font-size: 1.6rem;
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