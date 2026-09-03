<script>
  import { onMount } from 'svelte';
  import { fade } from 'svelte/transition';
  import GuideHand from './GuideHand.svelte';
  import GuideText from './GuideText.svelte';

  export let onClose = () => {};

  // handOffsetX / handOffsetY (px) fine-tune where the hand points per step.
  const STEPS = [
    {
      title: 'Ты ща афигеешь',
      subtitle: 'Это отдел, чтобы синхронизировать скорость трека и твои гифки!',
      hand: 'hi',
      handOffsetX: 0,
      handOffsetY: 0
    },
    {
      title: 'Небольшая настройка',
      subtitle: 'Тут нужно выбрать гифку, которую хочешь показать на экране',
      hand: 'pick',
      pickSpot: true,   // point at the drop zone / list
      noDim: true,      // don't darken the screen here
      handOffsetX: 300,
      handOffsetY: 0
    },
    {
      title: 'Синхронизация!',
      subtitle: 'Надо подогнать скорость гифки и бара снизу. Персонаж должен попадать в бит трека.',
      hand: 'pick',
      target: '.stepper',
      handOffsetX: 200,
      handOffsetY: 0
    },
    {
      title: 'Это всё',
      subtitle: 'Можешь копировать ссылку и вставлять её в OBS. При следующем треке гифка будет танцевать под бит!',
      hand: 'hi',
      handOffsetX: 0,
      handOffsetY: 0
    }
  ];

  let idx = 0;
  let spotlightStyle = 'rgba(0,0,0,0.85)';
  let spotCX = 0, spotCY = 0, spotR = 0;
  let handX = 0, handY = 0, handRot = 0;
  let handImage = 'hi.png';
  let handType = 'hi';

  if (typeof window !== 'undefined') {
    handX = window.innerWidth / 2;
    handY = window.innerHeight * 0.32;
  }

  const step = () => STEPS[idx];

  function pickRect() {
    const el =
      document.querySelector('.drop-empty') ||
      document.querySelector('.gif-list-pane') ||
      document.querySelector('.gif-preview-pane');
    return el ? el.getBoundingClientRect() : null;
  }

  function targetRect() {
    const s = step();
    if (s.pickSpot) return pickRect();
    if (s.target) {
      const el = document.querySelector(s.target);
      return el ? el.getBoundingClientRect() : null;
    }
    return null;
  }

  function layout() {
    const s = step();
    const rect = s.pickSpot || s.target ? targetRect() : null;
    handImage = s.hand === 'hi' ? 'hi.png' : 'picker.png';
    handType = s.hand;

    if (s.noDim) {
      spotlightStyle = 'transparent';
    } else if (!rect) {
      spotlightStyle = 'rgba(0,0,0,0.85)';
    } else {
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height / 2;
      const r = Math.max(rect.width, rect.height) / 2 + 36;
      spotCX = cx; spotCY = cy; spotR = r;
      spotlightStyle =
        `radial-gradient(circle ${r}px at ${cx}px ${cy}px, transparent 0%, transparent 62%, rgba(0,0,0,0.9) 100%)`;
    }

    if (s.hand === 'hi') {
      // greeting/farewell hand high up so it does not cover the screen
      handX = window.innerWidth / 2;
      handY = window.innerHeight * 0.32;
      handRot = 0;
    } else if (s.pickSpot) {
      // "here is where you add the gif"
      const r = rect || { left: 0, top: 0, width: 0, height: 0 };
      handX = r.left + r.width / 2;
      handY = r.top + r.height / 2;
      handRot = -45;
    } else if (rect) {
      handX = rect.left + rect.width / 2 + 70;
      handY = rect.top + rect.height / 2 + 10;
      handRot = -45;
    }

    // per-step pixel offset (authoring aid)
    handX += s.handOffsetX || 0;
    handY += s.handOffsetY || 0;
  }

  function scheduleLayout() {
    requestAnimationFrame(() => requestAnimationFrame(layout));
  }

  $: idx, scheduleLayout();

  function go(next) {
    if (next) {
      if (idx < STEPS.length - 1) idx++;
      else { onClose(); return; }
    } else {
      if (idx > 0) idx--;
      else { onClose(); return; }
    }
    scheduleLayout();
  }

  function onClick(e) {
    if (
      e.target.closest('.guide-text-container') ||
      e.target.closest('.dguide-nav') ||
      e.target.closest('.dguide-close')
    ) return;
    if (!spotlightStyle.startsWith('radial')) { go(true); return; }
    const dx = e.clientX - spotCX;
    const dy = e.clientY - spotCY;
    if (Math.sqrt(dx * dx + dy * dy) > spotR) go(true);
  }

  function onKey(e) {
    if (e.key === 'Escape') onClose();
    else if (e.key === 'Enter' || e.key === ' ') go(true);
  }

  onMount(() => {
    window.addEventListener('resize', scheduleLayout);
    scheduleLayout();
    return () => window.removeEventListener('resize', scheduleLayout);
  });
</script>

<svelte:window on:keydown={onKey} />

<div
  class="dguide"
  role="dialog"
  aria-label="Гайд dancesync"
  tabindex="0"
  transition:fade={{ duration: 320 }}
  on:click={onClick}
  on:keydown={onKey}
>
  <div class="dguide-backdrop" style="background:{spotlightStyle};"></div>

  <button class="dguide-close" on:click|stopPropagation={onClose} aria-label="Закрыть гайд">&#10005;</button>

  {#if idx > 0}
    <button class="dguide-nav prev" on:click|stopPropagation={() => go(false)} aria-label="Назад">&#9664;</button>
  {/if}
  {#if idx < STEPS.length - 1}
    <button class="dguide-nav next" on:click|stopPropagation={() => go(true)} aria-label="Далее">&#9654;</button>
  {:else}
    <button class="dguide-nav next" on:click|stopPropagation={() => onClose()} aria-label="Готово">OK</button>
  {/if}

  {#key idx}
    <GuideText title={step().title} subtitle={step().subtitle} stepIndex={idx} totalSteps={STEPS.length} />
  {/key}

  <GuideHand
    image={handImage}
    handType={handType}
    x={handX}
    y={handY}
    rotation={handRot}
    size={220}
  />
</div>

<style>
  .dguide {
    position: fixed;
    inset: 0;
    z-index: 20000;
    outline: none;
    cursor: pointer;
  }
  .dguide-backdrop {
    position: absolute;
    inset: 0;
  }
  .dguide-close {
    position: absolute;
    top: 20px;
    right: 20px;
    z-index: 20002;
    width: 46px;
    height: 46px;
    background: rgba(255, 255, 255, 0.08);
    border: 2px solid rgba(255, 255, 255, 0.35);
    color: #fff;
    font-size: 20px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
  }
  .dguide-close:hover {
    background: rgba(255, 255, 255, 0.2);
    transform: scale(1.1);
  }
  .dguide-nav {
    position: fixed;
    bottom: 2%;
    z-index: 20002;
    background: rgba(0, 0, 0, 0.5);
    border: 2px solid rgba(255, 255, 255, 0.3);
    color: #fff;
    min-width: 44px;
    height: 44px;
    padding: 0 0.75rem;
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
  }
  .dguide-nav:hover {
    background: rgba(0, 0, 0, 0.7);
    transform: scale(1.1);
  }
  .dguide-nav.prev { left: calc(50% - 70px); }
  .dguide-nav.next { left: calc(50% + 12px); }
</style>
