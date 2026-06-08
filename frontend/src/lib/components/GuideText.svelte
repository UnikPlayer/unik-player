<script>
    import { fade } from 'svelte/transition';
    export let title = '';
    export let subtitle = '';
    export let stepIndex = 0;
    export let totalSteps = 0;
</script>

{#if title || subtitle}
    <div class="guide-text-container"
         transition:fade={{ duration: 300 }}
         aria-live="polite"
         aria-atomic="true">
        {#key title}
            <div class="text-wrapper">
                {#if title}
                    <h2 class="guide-title">{title}</h2>
                {/if}
                {#if subtitle}
                    <p class="guide-subtitle">{subtitle}</p>
                {/if}
                {#if totalSteps > 0}
                    <div class="step-counter">
                        {stepIndex + 1} / {totalSteps}
                    </div>
                {/if}
            </div>
        {/key}
    </div>
{/if}

<style>
    .guide-text-container {
        position: fixed;
        bottom: 15%;
        left: 50%;
        transform: translateX(-50%);
        z-index: 10000;
        pointer-events: none;
        width: 90%;
        max-width: 800px;
        text-align: center;
    }

    .text-wrapper {
        background: rgba(0, 0, 0, 0.7);
        border: 2px solid rgba(255, 255, 255, 0.3);
        border-radius: 8px;
        padding: 1.5rem 2rem;
        backdrop-filter: blur(3px);
        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
    }

    .guide-title {
        font-family: '8bitwonder', monospace;
        font-size: 1.8rem;
        color: white;
        margin: 0 0 0.8rem 0;
        letter-spacing: 0.05em;
        text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5);
        line-height: 1.3;
    }

    .guide-subtitle {
        font-family: '8bitwonder', monospace;
        font-size: 1rem;
        color: rgba(255, 255, 255, 0.7);
        margin: 0;
        letter-spacing: 0.03em;
        text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
        line-height: 1.4;
    }

    .step-counter {
        font-family: '8bitwonder', monospace;
        font-size: 0.7rem;
        color: rgba(255, 255, 255, 0.5);
        margin-top: 1rem;
        letter-spacing: 0.1em;
    }
</style>
