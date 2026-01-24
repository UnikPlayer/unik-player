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
	  out:fly|global={{ x: 50, duration: 400, opacity: 0 }}>
        <div class="picDiv">
            <img class="pic" src={$thumbnail} alt="">
        </div>
        <div class="textDiv">

          <h2 use:marquee={{speed:70}} class="title">{$title}</h2>
          <h3 use:marquee={{speed:50}} class="artist">{$artist}</h3>

        </div>
    </div>
  {/key}
{/if}

<style lang="scss">
.mainDiv {
  display: flex;
  flex-direction: row;
  align-items: center;
  gap: 0;
  max-width: 100%;
}

.picDiv {
  overflow: hidden;
  z-index: 2;
}

.pic {
  width: 8rem;
  height: 8rem;
  object-fit: cover;
  border-radius: 1rem;
  border: 0.2rem solid var(--lightMuted);
  display: block;
  z-index: 2;
}

.textDiv {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  width: 20rem;
  height: 8rem;
  margin-left: -1rem;
  border-radius: 0 1rem 1rem 0;
  border: 0.2rem solid var(--vibrant);
  border-left: none;
  background-color: var(--darkMuted);
  z-index: 1;
}

.title,
.artist {
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

.title {
  font-size: 1.8rem;
  margin-bottom: 0.3rem;
}

.artist {
  font-size: 1.7rem;
}
</style>