<script>
    import { onMount, onDestroy } from 'svelte';
    import { currentStepIndex } from '$lib/stores/guideState';
    
    export let file = '';
    
    let audioElement = null;
    let currentFile = '';
    
    $: if (file && file !== currentFile) {
        playAudio(file);
    }
    
    function playAudio(filename) {
        if (audioElement) {
            audioElement.pause();
            audioElement.currentTime = 0;
        }
        if (!filename) return;
        const audioPath = `/tts/${filename}`;
        currentFile = filename;
        try {
            audioElement = new Audio(audioPath);
            audioElement.volume = 0.4;
            audioElement.play().catch(err => {
                console.warn(`[Guide TTS] Playback failed for ${audioPath}:`, err.message);
            });
            audioElement.onerror = () => {
                console.error(`[Guide TTS] File not found: ${audioPath}`);
                audioElement = null;
            };
        } catch (err) {
            console.error(`[Guide TTS] Error loading audio file ${audioPath}:`, err.message);
            audioElement = null;
        }
    }
    
    onDestroy(() => {
        if (audioElement) {
            audioElement.pause();
            audioElement.currentTime = 0;
            audioElement = null;
        }
    });
</script>

<div style="display: none;"></div>
