<script>
    export let stepData;
    import { fly } from 'svelte/transition';

    $: element = document.querySelector(stepData.element);

    function getArrowStyle() {
        if (!element) return {};

        const rect = element.getBoundingClientRect();
        const arrowSize = 20;
        const arrowOffset = 10;

        switch (stepData.position) {
            case 'top':
                return {
                    top: `${rect.top - arrowSize - arrowOffset}px`,
                    left: `${rect.left + rect.width / 2}px`,
                    transform: 'translateX(-50%) rotate(180deg)',
                };
            case 'bottom':
                return {
                    top: `${rect.bottom + arrowOffset}px`,
                    left: `${rect.left + rect.width / 2}px`,
                    transform: 'translateX(-50%)',
                };
            case 'left':
                return {
                    top: `${rect.top + rect.height / 2}px`,
                    left: `${rect.left - arrowSize - arrowOffset}px`,
                    transform: 'translateY(-50%) rotate(90deg)',
                };
            case 'right':
                return {
                    top: `${rect.top + rect.height / 2}px`,
                    left: `${rect.right + arrowOffset}px`,
                    transform: 'translateY(-50%) rotate(-90deg)',
                };
            default:
                return {};
        }
    }

    function speakText(text) {
        if ('speechSynthesis' in window) {
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.volume = 0.4; // Set volume to 40%
            speechSynthesis.speak(utterance);
        }
    }
</script>

{#if element}
    <div class="guide-step" style="position: absolute; top: {element.offsetTop}px; left: {element.offsetLeft}px; width: {element.offsetWidth}px; height: {element.offsetHeight}px;">
        <div class="highlight" style="position: absolute; top: 0; left: 0; width: 100%; height: 100%;"></div>
    </div>
{/if}

{#if stepData}
    <div class="guide-text" transition:fly={{ y: 20, duration: 300 }}>
        <p>{stepData.text}</p>
    </div>
    <div class="arrow" style="{getArrowStyle()}" transition:fly={{ y: -20, duration: 300 }}>
        &#9650;
    </div>
    {#if stepData.text}
        <script>
            speakText(stepData.text);
        </script>
    {/if}
{/if}

<style>
    .guide-step {
        pointer-events: none;
    }

    .highlight {
        border: 2px dashed yellow;
        border-radius: 5px;
    }

    .guide-text {
        position: fixed;
        background-color: white;
        color: black;
        padding: 1rem;
        border-radius: 5px;
        box-shadow: 0 2px 5px rgba(0, 0, 0, 0.2);
        z-index: 1001;
        max-width: 300px;
        top: 20px; /* Adjust as needed */
        left: 20px; /* Adjust as needed */
    }

    .arrow {
        position: absolute;
        font-size: 2rem;
        color: yellow;
        z-index: 1001;
        pointer-events: none;
    }
</style>