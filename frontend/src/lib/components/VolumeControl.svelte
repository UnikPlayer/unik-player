<script>
    import { onMount } from 'svelte';
    import { guideVolume } from '$lib/stores/guideState.js';
    
    export let guideMode = false;
    let volume = 0.4;
    let audioContext;
    let gainNode;
    
    // Подписываемся на изменения guideVolume если в режиме гайда
    onMount(() => {
        if (guideMode) {
            const unsubscribe = guideVolume.subscribe(value => {
                volume = value;
            });
            return unsubscribe;
        } else {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
            gainNode = audioContext.createGain();
            gainNode.gain.value = volume;
            gainNode.connect(audioContext.destination);
        }
    });
    
    function handleVolumeChange(event) {
        volume = parseFloat(event.target.value);
        if (guideMode) {
            guideVolume.set(volume);
        } else {
            if (gainNode) {
                gainNode.gain.value = volume;
            }
        }
    }
</script>

<div class="volume-control" class:guide-mode={guideMode}>
    <input
        type="range"
        min="0"
        max="1"
        step="0.01"
        bind:value={volume}
        on:input={handleVolumeChange}
    />
</div>

<style>
    .volume-control {
        position: fixed;
        top: 10px;
        left: 50%;
        transform: translateX(-50%);
        z-index: 1001;
        background-color: rgba(0, 0, 0, 0.7);
        padding: 0.5rem 1rem;
        border-radius: 5px;
    }
    
    .guide-mode {
        position: relative;
        top: auto;
        left: auto;
        transform: none;
        background: transparent;
        padding: 0;
        z-index: auto;
        width: 200px;
    }
    
    /* 8bit style for guide mode */
    .guide-mode input[type="range"] {
        -webkit-appearance: none;
        width: 100%;
        height: 8px;
        background: var(--c2);
        border: 2px solid var(--c1);
        outline: none;
        cursor: pointer;
    }
    
    .guide-mode input[type="range"]::-webkit-slider-thumb {
        -webkit-appearance: none;
        width: 16px;
        height: 16px;
        background: var(--c1);
        border: 2px solid var(--c2);
        cursor: pointer;
        image-rendering: pixelated;
    }
    
    .guide-mode input[type="range"]::-moz-range-thumb {
        width: 16px;
        height: 16px;
        background: var(--c1);
        border: 2px solid var(--c2);
        cursor: pointer;
        image-rendering: pixelated;
    }
    
    /* Default style (non-guide) */
    input[type="range"] {
        width: 150px;
    }
</style>