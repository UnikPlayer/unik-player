<script>
    import { tweened } from 'svelte/motion';
    import { cubicOut } from 'svelte/easing';
    import { fade } from 'svelte/transition';
    
    export let image = 'hi.png';
    export let x = 0;
    export let y = 0;
    export let rotation = 0;
    export let size = 240;
    export let handType = 'hi';
    
    const tweenedX = tweened(x, { duration: 500, easing: cubicOut });
    const tweenedY = tweened(y, { duration: 500, easing: cubicOut });
    const tweenedRotation = tweened(rotation, { duration: 500, easing: cubicOut });
    
    $: if (x !== undefined) tweenedX.set(x);
    $: if (y !== undefined) tweenedY.set(y);
    $: if (rotation !== undefined) tweenedRotation.set(rotation);
    
    $: isWave = handType === 'hi';
</script>

{#if image}
    <div 
        class="guide-hand" class:wave={isWave}
        style="left: {$tweenedX}px; top: {$tweenedY}px; transform: translate(-50%, -50%) rotate({$tweenedRotation}deg);"
        transition:fade={{ duration: 300 }}
        aria-hidden="true"
    >
        <img 
            src="/hands/{image}" 
            alt="Guide hand"
            style="width: {size}px; height: auto;"
        />
    </div>
{/if}

<style>
    .guide-hand {
        position: fixed;
        z-index: 10000;
        pointer-events: none;
        filter: drop-shadow(0 4px 8px rgba(0, 0, 0, 0.5));
    }
    
    .guide-hand img {
        display: block;
        image-rendering: pixelated;
    }
    .guide-hand.wave img {
        transform-origin: 50% 100%;
        animation: wave 1.2s ease-in-out infinite;
    }
    @keyframes wave {
        0%, 100% { transform: rotate(-15deg); }
        50% { transform: rotate(15deg); }
    }
</style>
