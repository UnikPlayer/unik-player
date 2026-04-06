<script>
  import { fly } from 'svelte/transition';
  import { ShowNotification, notificationText } from '$lib/stores/stores';

  let visible = false;
  const timer = 2500;

  ShowNotification.subscribe(value => {
    if (value) {
      visible = true;
      setTimeout(() => {
        ShowNotification.set(false);
      }, timer);
    } else {
      visible = false;
    }
  });
</script>

{#if visible}
  <div class="notification" in:fly={{ y: -30, duration: 300 }} out:fly={{ y: -30, duration: 200 }}>
    <div class="notification-content">
      <span class="notification-icon">[ ]</span>
      <span class="notification-text">{$notificationText}</span>
    </div>
    <div class="notification-progress"></div>
  </div>
{/if}

<style>
  .notification {
    position: fixed;
    top: 1.5rem;
    left: 50%;
    transform: translateX(-50%);
    z-index: 9999;
    min-width: 240px;
    background: var(--c-cloud, #f5f5f5);
    overflow: hidden;
    clip-path: polygon(
      0px 8px, 4px 8px, 4px 4px, 8px 4px, 8px 0px,
      calc(100% - 8px) 0px, calc(100% - 8px) 4px, calc(100% - 4px) 4px, calc(100% - 4px) 8px, 100% 8px,
      100% calc(100% - 8px), calc(100% - 4px) calc(100% - 8px), calc(100% - 4px) calc(100% - 4px), calc(100% - 8px) calc(100% - 4px), calc(100% - 8px) 100%,
      8px 100%, 8px calc(100% - 4px), 4px calc(100% - 4px), 4px calc(100% - 8px), 0px calc(100% - 8px)
    );
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
  }

  .notification-content {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.8rem 1.2rem;
  }

  .notification-icon {
    color: var(--c1, #0a0a0a);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
  }

  .notification-text {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: var(--c1, #0a0a0a);
    letter-spacing: 0.06em;
  }

  .notification-progress {
    height: 3px;
    background: var(--c1, #0a0a0a);
    animation: progress 2.5s linear forwards;
  }

  @keyframes progress {
    from { width: 100%; }
    to { width: 0%; }
  }
</style>
