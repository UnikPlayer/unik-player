import { writable, derived } from 'svelte/store';

export const isActive = writable(false);
export const currentStepIndex = writable(0);
export const steps = writable([]);
export const guideVolume = writable(0.4);

const FIRST_LAUNCH_KEY = 'unikplayer_guide_shown';
export const isFirstLaunch = writable(false);

if (typeof window !== 'undefined') {
    const shown = localStorage.getItem(FIRST_LAUNCH_KEY);
    isFirstLaunch.set(!shown);
}

export const currentStep = derived(
    [steps, currentStepIndex],
    ([$steps, $index]) => {
        if ($steps && $steps.length > 0 && $index >= 0 && $index < $steps.length) {
            return $steps[$index];
        }
        return null;
    }
);

export const totalSteps = derived(steps, ($steps) => $steps?.length || 0);

export const hasNextStep = derived(
    [currentStepIndex, totalSteps],
    ([$index, $total]) => $index < $total - 1
);

export async function startGuide() {
    try {
        const res = await fetch('/guide.json');
        const data = await res.json();
        steps.set(data.steps);
    } catch (e) {
        console.error('[Guide] Failed to load:', e);
    }
    currentStepIndex.set(0);
    isActive.set(true);
    if (typeof document !== 'undefined') {
        document.body.classList.add('guide-active');
    }
}

export function nextStep() {
    let currentIndex;
    currentStepIndex.subscribe(v => currentIndex = v)();
    let total;
    totalSteps.subscribe(v => total = v)();
    if (currentIndex < total - 1) {
        currentStepIndex.update(n => n + 1);
        return false;
    } else {
        closeGuide();
        return true;
    }
}

export function closeGuide() {
    isActive.set(false);
    currentStepIndex.set(0);
    if (typeof window !== 'undefined') {
        localStorage.setItem(FIRST_LAUNCH_KEY, 'true');
        isFirstLaunch.set(false);
    }
    if (typeof document !== 'undefined') {
        document.body.classList.remove('guide-active');
    }
}

export function getElementPosition(selector) {
    if (typeof document === 'undefined' || !selector) return null;
    const element = document.querySelector(selector);
    if (!element) {
        console.warn(`[Guide] Element not found: ${selector}`);
        return null;
    }
    return element.getBoundingClientRect();
}

export function calculateHandPosition(rect, relativePos = 'right', offset = 20) {
    if (!rect) return { x: window.innerWidth / 2, y: window.innerHeight / 2 };
    let x, y;
    switch (relativePos) {
        case 'top': x = rect.left + rect.width / 2; y = rect.top - offset; break;
        case 'bottom': x = rect.left + rect.width / 2; y = rect.bottom + offset; break;
        case 'left': x = rect.left - offset; y = rect.top + rect.height / 2; break;
        case 'right': x = rect.right + offset; y = rect.top + rect.height / 2; break;
        case 'center': x = rect.left + rect.width / 2; y = rect.top + rect.height / 2; break;
        default: x = rect.right + offset; y = rect.top + rect.height / 2;
    }
    return { x, y };
}
