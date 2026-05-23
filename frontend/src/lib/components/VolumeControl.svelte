<script>
    import { onMount } from 'svelte';

    let volume = 0.4; // Default volume
    let audioContext;
    let gainNode;

    onMount(() => {
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
        gainNode = audioContext.createGain();
        gainNode.gain.value = volume;
        // Connect gainNode to the audioContext destination (speakers)
        gainNode.connect(audioContext.destination);
    });

    function handleVolumeChange(event) {
        volume = parseFloat(event.target.value);
        if (gainNode) {
            gainNode.gain.value = volume;
        }
    }
</script>

<div class="volume-control">
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

    input[type="range"] {
        width: 150px;
    }
</style>