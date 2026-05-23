import { writable } from 'svelte/store';

export const isGuideActive = writable(false);
export const guideStep = writable(null);
export const guideData = writable([]);

export function startGuide(data) {
    guideData.set(data);
    guideStep.set(0);
    isGuideActive.set(true);
}

export function nextGuideStep() {
    guideStep.update(step => {
        if (step === null) return 0;
        return Math.min(step + 1, $guideData.length - 1);
    });
}

export function prevGuideStep() {
    guideStep.update(step => {
        if (step === null) return 0;
        return Math.max(step - 1, 0);
    });
}