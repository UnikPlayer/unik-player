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
  <div class="notification" in:fly={{ y: -50, duration: 300 }} out:fly={{ y: -50, duration: 200 }}>
    <div class="notification-content">
      <span class="notification-icon">[ ]</span>
      <span class="notification-text">{$notificationText}</span>
    </div>
    <div class="notification-progress"></div>
  </div>
{/if}

<style lang="scss">
  .notification {
    position: fixed;
    top: 2rem;
    left: 50%;
    transform: translateX(-50%);
    z-index: 9999;
    min-width: 280px;

    background: rgba(15, 15, 20, 0.95);
    border: 1px solid rgba(184, 115, 51, 0.5);
    border-radius: 4px;
    overflow: hidden;
    backdrop-filter: blur(10px);

    box-shadow:
      0 4px 24px rgba(0, 0, 0, 0.4),
      0 0 40px rgba(184, 115, 51, 0.1);
  }

  .notification-content {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 1rem 1.5rem;
  }

  .notification-icon {
    color: #B87333;
    font-family: monospace;
    font-size: 1.2rem;
  }

  .notification-text {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.85rem;
    font-weight: 600;
    color: white;
    letter-spacing: 0.1em;
  }

  .notification-progress {
    height: 2px;
    background: linear-gradient(90deg, #B87333, #D4944A);
    animation: progress 2.5s linear forwards;
  }

  @keyframes progress {
    from { width: 100%; }
    to { width: 0%; }
  }
</style>
