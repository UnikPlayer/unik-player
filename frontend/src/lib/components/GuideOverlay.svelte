<script>
    import { fade } from 'svelte/transition';
    import { guideStep, isGuideActive, guideData } from '$lib/stores/guide';
    import GuideStep from './GuideStep.svelte';
    import VolumeControl from './VolumeControl.svelte'; // Import VolumeControl

    let currentStepData;

    $: {
        if ($isGuideActive && $guideStep !== null && $guideData && $guideData.length > 0) {
            currentStepData = $guideData[$guideStep];
        } else {
            currentStepData = null;
        }
    }

    function closeGuide() {
        isGuideActive.set(false);
        guideStep.set(null);
    }
</script>

{#if $isGuideActive}
    <div class="guide-overlay" transition:fade={{ duration: 200 }}>
        <div class="guide-backdrop" on:click={closeGuide}></div>
        <VolumeControl />
        {#if currentStepData}
            <GuideStep stepData={currentStepData} />
        {/if}
    </div>
{/if}

<style>
    .guide-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        z-index: 1000;
    }

    .guide-backdrop {
        position: absolute;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background-color: rgba(0, 0, 0, 0.8);
        backdrop-filter: blur(5px);
    }
</style>