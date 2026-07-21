<script>
    import { onMount, onDestroy } from 'svelte';
    import { fade, scale } from 'svelte/transition';
    import { tweened } from 'svelte/motion';
    import { cubicOut } from 'svelte/easing';
    import { 
        isActive, 
        currentStep, 
        currentStepIndex, 
        totalSteps, 
        nextStep, 
        closeGuide, 
        getElementPosition,
        calculateHandPosition 
    } from '$lib/stores/guideState.js';
    import GuideHand from './GuideHand.svelte';
    import GuideText from './GuideText.svelte';
    import GuideAudio from './GuideAudio.svelte';
    import VolumeControl from './VolumeControl.svelte';
    import { editorOpen } from '$lib/stores/stores.js';
    
    let showVolumeControl = false;
    let volumeControlPopped = false;
    let spotlightStyle = '';
    let spotlightCX = -9999, spotlightCY = -9999, spotlightR = 0;
    
    const handX = tweened(0, { duration: 500, easing: cubicOut });
    const handY = tweened(0, { duration: 500, easing: cubicOut });
    const handRotation = tweened(0, { duration: 500, easing: cubicOut });
    
    $: if ($currentStep) {
        updateStepElements($currentStep);
    }
    
    function updateStepElements(step) {
        if (!step) return;
        updateSpotlight(step);
        updateHandPosition(step);
        if (step.autoClick) {
            setTimeout(() => {
                const el = document.querySelector(step.autoClick);
                if (el) el.click();
            }, 500);
        }
        if (step.closeEditor) {
            editorOpen.set(false);
        }
    }
    
    function updateSpotlight(step) {
        if (!step.targetElement) {
            spotlightStyle = '';
            return;
        }
        const rect = getElementPosition(step.targetElement);
        if (!rect) {
            spotlightStyle = '';
            return;
        }
        const cx = rect.left + rect.width / 2;
        const cy = rect.top + rect.height / 2;
        const r = Math.max(rect.width, rect.height) / 2 + 30;
        const shape = step.spotlightShape || 'circle';
        if (shape === 'rect') {
            const rx = rect.width / 2 + 20;
            const ry = rect.height / 2 + 20;
            spotlightCX = cx; spotlightCY = cy; spotlightR = r;
            spotlightStyle = `radial-gradient(ellipse ${rx}px ${ry}px at ${cx}px ${cy}px, transparent 0%, transparent 70%, rgba(0,0,0,0.85) 100%)`;
        } else {
            spotlightCX = cx; spotlightCY = cy; spotlightR = r;
            spotlightStyle = `radial-gradient(circle ${r}px at ${cx}px ${cy}px, transparent 0%, transparent 60%, rgba(0,0,0,0.85) 100%)`;
        }
    }
    
    function updateHandPosition(step) {
        const handFile = step.handImage || (step.handType === 'hi' ? 'hi.png' : 'picker.png');
        if (!handFile) return;
        
        let targetX, targetY, targetRotation = 0;
        
        if (step.targetElement) {
            const rect = getElementPosition(step.targetElement);
            if (rect) {
                if (step.handType === 'hi') {
                    targetX = window.innerWidth / 2;
                    targetY = window.innerHeight / 2;
                } else {
                    targetX = rect.left + rect.width / 2 + 210;
                    targetY = rect.top + rect.height / 2 + 20;
                }
                targetRotation = step.handType === 'hi' ? 0 : -45;
            } else {
                targetX = window.innerWidth / 2;
                targetY = window.innerHeight / 2;
            }
        } else {
            targetX = window.innerWidth / 2;
            targetY = window.innerHeight / 2;
        }
        
        handX.set(targetX);
        handY.set(targetY);
        handRotation.set(targetRotation);
    }
    
    function getRotationForPosition(pos) {
        switch (pos) {
            case 'top': return 180;
            case 'bottom': return 0;
            case 'left': return 90;
            case 'right': return -90;
            default: return -90;
        }
    }
    
    function handleOverlayClick(e) {
        if (e.target.closest('.guide-volume') || e.target.closest('.guide-text-container')) return;
        const dx = e.clientX - spotlightCX;
        const dy = e.clientY - spotlightCY;
        const dist = Math.sqrt(dx * dx + dy * dy);
        if (dist > spotlightR) {
            nextStep();
        } else {
            const overlay = e.currentTarget;
            overlay.style.pointerEvents = 'none';
            const el = document.elementFromPoint(e.clientX, e.clientY);
            overlay.style.pointerEvents = '';
            if (el && el !== overlay) el.click();
        }
    }
    
    function handleKeydown(e) {
        if (e.key === 'Escape') closeGuide();
        else if (e.key === 'Enter' || e.key === ' ') nextStep();
    }
    
    onMount(() => {
        const unsubActive = isActive.subscribe(active => {
            if (active) {
                setTimeout(() => {
                    showVolumeControl = true;
                    setTimeout(() => { volumeControlPopped = true; }, 1000);
                }, 1000);
            } else {
                showVolumeControl = false;
                volumeControlPopped = false;
            }
        });
        
        const handleResize = () => {
            if ($isActive && $currentStep) updateStepElements($currentStep);
        };
        window.addEventListener('resize', handleResize);
        
        return () => {
            unsubActive();
            window.removeEventListener('resize', handleResize);
        };
    });
</script>

{#if $isActive}
<div class="guide-overlay"
     transition:fade={{ duration: 300 }}
     on:click={handleOverlayClick}
     on:keydown={handleKeydown}
     role="dialog"
     aria-label="Интерактивный гайд"
     tabindex="0">

    <div class="guide-backdrop" style="background: {$currentStep?.transparentBackdrop ? (spotlightStyle || 'transparent') : (spotlightStyle || 'rgba(0,0,0,0.85)')};"></div>

    <button class="guide-close-btn" on:click|stopPropagation={closeGuide} aria-label="Закрыть гайд">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M18 6L6 18M6 6L18 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
    </button>

    {#if $currentStepIndex > 0}
    <button class="guide-nav guide-nav-prev" on:click|stopPropagation={() => { currentStepIndex.update(n => n - 1); }} aria-label="Назад">
        &#9664;
    </button>
    {/if}

    {#if $currentStepIndex < $totalSteps - 1}
    <button class="guide-nav guide-nav-next" on:click|stopPropagation={nextStep} aria-label="Далее">
        &#9654;
    </button>
    {/if}

    {#if showVolumeControl}
    <div class="guide-volume" class:popped={volumeControlPopped}
         transition:scale={{ duration: 300, start: 0.8 }}>
        <VolumeControl guideMode={true} />
    </div>
    {/if}

    {#if $currentStep && $currentStep.handType}
    <GuideHand
        image={$currentStep.handImage || ($currentStep.handType === 'hi' ? 'hi.png' : 'picker.png')}
        handType={$currentStep.handType}
        x={$handX}
        y={$handY}
        rotation={$handRotation}
    />
    {/if}

    {#if $currentStep && ($currentStep.title || $currentStep.text)}
    <GuideText
        title={$currentStep.title || ''}
        subtitle={$currentStep.subtitle || $currentStep.text || ''}
        stepIndex={$currentStepIndex}
        totalSteps={$totalSteps}
    />
    {/if}

    {#if $currentStep && $currentStep.ttsFile}
    <GuideAudio file={$currentStep.ttsFile} />
    {/if}
</div>
{/if}

<style>
    .guide-overlay {
        position: fixed;
        top: 0; left: 0;
        width: 100%; height: 100%;
        z-index: 20000;
        outline: none;
    }
    .guide-backdrop {
        position: absolute;
        top: 0; left: 0;
        width: 100%; height: 100%;
        z-index: 0;
    }
    .guide-close-btn {
        position: absolute;
        top: 20px; right: 20px;
        z-index: 20001;
        background: rgba(255,255,255,0.1);
        border: 2px solid rgba(255,255,255,0.3);
        color: white;
        width: 48px; height: 48px;
        border-radius: 8px;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        transition: all 0.2s;
        pointer-events: auto;
    }
    .guide-close-btn:hover {
        background: rgba(255,255,255,0.2);
        border-color: rgba(255,255,255,0.5);
        transform: scale(1.1);
    }
    .guide-volume {
        position: absolute;
        top: 20px; left: 50%;
        transform: translateX(-50%) scale(0.8);
        z-index: 10000;
        pointer-events: auto;
        opacity: 0;
        transition: opacity 0.3s, transform 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55);
    }
    .guide-volume.popped {
        opacity: 1;
        transform: translateX(-50%) scale(1);
    }
    .guide-nav {
        position: absolute;
        bottom: 2%;
        z-index: 20001;
        background: rgba(0,0,0,0.5);
        border: 2px solid rgba(255,255,255,0.3);
        color: white;
        width: 44px; height: 44px;
        border-radius: 4px;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 18px;
        transition: all 0.2s;
        pointer-events: auto;
    }
    .guide-nav:hover {
        background: rgba(0,0,0,0.7);
        transform: scale(1.1);
        font-size:22px;
    }
    .guide-nav-prev { left: calc(50% - 60px); }
    .guide-nav-next { left: calc(50% + 16px); }
</style>