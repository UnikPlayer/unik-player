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
  		in:fly|global={{  x: -50, duration: 400, opacity: 0 }}
	   out:fly|global={{  x:  50, duration: 400, opacity: 0 }}>
        <div class="picDiv">
            <img class="pic" src={$thumbnail} alt="">
        </div>
        <div class="textDiv">
            <div class="titleDiv">
                <h2 use:marquee={{speed:70}} class="title">{$title}</h2>
            </div>

            <div class="artistDiv">
                <h3 use:marquee={{speed:50}} class="artist">{$artist}</h3>
            </div>

        </div>
    </div>
  {/key}
{/if}

<style lang="scss">
.mainDiv {
  display: flex;
  align-items: stretch;
  gap: 0.6rem;
  max-width: 100%;
}

.pic {
  display: block;
  border-radius: 1rem;
  border: 0.2rem solid var(--lightMuted);
  width: 10rem;
  height: 10rem;
  object-fit: cover;
  flex: 0 0 10rem;
}

.textDiv {
  display: flex;
  flex-direction: column;
  flex: 1;
  width: 20rem;
  gap: 0.6rem;
}

.titleDiv {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  border: 0.2rem solid var(--vibrant);
  background-color: var(--darkMuted);
  flex: 4;
  padding: 0.8rem 1rem;
}

.artistDiv {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.8rem;
  border: 0.2rem solid var(--vibrant);
  background-color: var(--darkMuted);
  flex: 2;
  padding: 0.6rem 1rem;
}

.title,
.artist {
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

.title {
  font-size: 1.8rem;
}

.artist {
  font-size: 1.3rem;
}
</style>