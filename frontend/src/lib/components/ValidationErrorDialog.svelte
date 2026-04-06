<script>
  import { fly, fade } from 'svelte/transition';

  /** @type {boolean} */
  export let visible = false;

  /** @type {Array<{line: number, column: number, message: string, severity: string}>} */
  export let errors = [];

  /** @type {string} */
  export let html = '';

  /** @type {() => void} */
  export let onClose = () => {};

  let copied = false;

  function copyErrors() {
    const text = errors
      .map(e => `Line ${e.line}:${e.column} - [${e.severity.toUpperCase()}] ${e.message}`)
      .join('\n');
    navigator.clipboard.writeText(text);
    copied = true;
    setTimeout(() => copied = false, 2000);
  }

  function getHighlightedHTML() {
    if (!html) return '';
    const lines = html.split('\n');
    const errorLines = new Set(errors.filter(e => e.severity === 'error').map(e => e.line));

    return lines.map((line, i) => {
      const lineNum = i + 1;
      const isError = errorLines.has(lineNum);
      const escaped = line
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
      return `<span class="line ${isError ? 'error-line' : ''}"><span class="line-num">${String(lineNum).padStart(3, ' ')}</span>${escaped}</span>`;
    }).join('\n');
  }

  $: highlightedHTML = getHighlightedHTML();
  $: errorCount = errors.filter(e => e.severity === 'error').length;
  $: warningCount = errors.filter(e => e.severity === 'warning').length;
</script>

{#if visible}
  <div class="overlay" transition:fade={{ duration: 200 }} on:click={onClose} on:keydown={(e) => e.key === 'Escape' && onClose()} role="dialog" tabindex="-1">
    <div class="dialog" transition:fly={{ y: 50, duration: 300 }} on:click|stopPropagation role="document">
      <div class="header">
        <button class="close-btn" on:click={onClose} aria-label="Close">X</button>
        <h2>VALIDATION ERRORS</h2>
        <div class="stats">
          {#if errorCount > 0}
            <span class="stat error">{errorCount} error{errorCount > 1 ? 's' : ''}</span>
          {/if}
          {#if warningCount > 0}
            <span class="stat warning">{warningCount} warning{warningCount > 1 ? 's' : ''}</span>
          {/if}
        </div>
      </div>

      <div class="content">
        <div class="errors-list">
          {#each errors as error}
            <div class="error-item {error.severity}">
              <span class="location">Line {error.line}:{error.column}</span>
              <span class="message">{error.message}</span>
            </div>
          {/each}
        </div>

        <div class="preview-section">
          <div class="preview-header">HTML PREVIEW</div>
          <pre class="html-preview"><code>{@html highlightedHTML}</code></pre>
        </div>
      </div>

      <div class="footer">
        <button class="btn copy-btn" on:click={copyErrors}>
          {copied ? '[ COPIED ]' : '[ COPY ERRORS ]'}
        </button>
        <button class="btn close-btn-footer" on:click={onClose}>
          [ CLOSE ]
        </button>
      </div>
    </div>
  </div>
{/if}

<style lang="scss">
  .overlay {
    position: fixed;
    inset: 0;
    background: var(--c-backdrop, rgba(0, 0, 0, 0.65));
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 10000;
    backdrop-filter: blur(5px);
  }

  .dialog {
    width: 90%;
    max-width: 900px;
    max-height: 85vh;
    background: var(--c1);
    border: 1px solid rgba(184, 115, 51, 0.4);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    clip-path: polygon(
      0px 12px, 4px 12px, 4px 8px, 8px 8px, 8px 4px, 12px 4px, 12px 0px,
      calc(100% - 12px) 0px, calc(100% - 12px) 4px, calc(100% - 8px) 4px, calc(100% - 8px) 8px, calc(100% - 4px) 8px, calc(100% - 4px) 12px, 100% 12px,
      100% calc(100% - 12px), calc(100% - 4px) calc(100% - 12px), calc(100% - 4px) calc(100% - 8px), calc(100% - 8px) calc(100% - 8px), calc(100% - 8px) calc(100% - 4px), calc(100% - 12px) calc(100% - 4px), calc(100% - 12px) 100%,
      12px 100%, 12px calc(100% - 4px), 8px calc(100% - 4px), 8px calc(100% - 8px), 4px calc(100% - 8px), 4px calc(100% - 12px), 0px calc(100% - 12px)
    );
    box-shadow: 0 8px 40px rgba(0, 0, 0, 0.3);
  }

  .header {
    padding: 1.5rem 2rem;
    border-bottom: 1px solid rgba(184, 115, 51, 0.25);
    display: flex;
    align-items: center;
    gap: 1rem;
    position: relative;

    h2 {
      font-family: '8bitwonder', monospace;
      font-size: 0.7rem;
      color: #ff6b6b;
      letter-spacing: 0.08em;
      margin: 0;
    }

    .close-btn {
      position: absolute;
      right: 1.5rem;
      top: 50%;
      transform: translateY(-50%);
      background: transparent;
      border: 1px solid rgba(255, 107, 107, 0.4);
      color: #ff6b6b;
      font-family: '8bitwonder', monospace;
      font-size: 0.55rem;
      padding: 0.4rem 0.8rem;
      cursor: pointer;
      transition: all 0.2s;

      &:hover {
        background: rgba(255, 107, 107, 0.1);
        border-color: #ff6b6b;
      }
    }

    .stats {
      display: flex;
      gap: 0.75rem;

      .stat {
        font-family: '8bitwonder', monospace;
        font-size: 0.4rem;
        padding: 0.25rem 0.6rem;

        &.error {
          background: rgba(255, 107, 107, 0.12);
          color: #ff6b6b;
          border: 1px solid rgba(255, 107, 107, 0.25);
        }

        &.warning {
          background: rgba(255, 193, 7, 0.12);
          color: #ffc107;
          border: 1px solid rgba(255, 193, 7, 0.25);
        }
      }
    }
  }

  .content {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    padding: 1.5rem 2rem;
    gap: 1.5rem;
  }

  .errors-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    max-height: 200px;
    overflow-y: auto;

    &::-webkit-scrollbar { width: 6px; }
    &::-webkit-scrollbar-track { background: rgba(255, 255, 255, 0.03); }
    &::-webkit-scrollbar-thumb { background: rgba(184, 115, 51, 0.4); }
  }

  .error-item {
    display: flex;
    gap: 1rem;
    padding: 0.6rem 1rem;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;

    &.error {
      background: rgba(255, 107, 107, 0.08);
      border-left: 3px solid #ff6b6b;
    }

    &.warning {
      background: rgba(255, 193, 7, 0.08);
      border-left: 3px solid #ffc107;
    }

    .location {
      color: rgba(255, 255, 255, 0.4);
      white-space: nowrap;
    }

    .message {
      color: var(--c2);
    }
  }

  .preview-section {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }

  .preview-header {
    font-family: '8bitwonder', monospace;
    font-size: 0.5rem;
    color: rgba(255, 255, 255, 0.35);
    letter-spacing: 0.08em;
    margin-bottom: 0.75rem;
  }

  .html-preview {
    flex: 1;
    margin: 0;
    padding: 1rem;
    background: rgba(0, 0, 0, 0.4);
    border: 1px solid rgba(255, 255, 255, 0.08);
    overflow: auto;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.75rem;
    line-height: 1.6;

    &::-webkit-scrollbar { width: 6px; height: 6px; }
    &::-webkit-scrollbar-track { background: rgba(255, 255, 255, 0.03); }
    &::-webkit-scrollbar-thumb { background: rgba(184, 115, 51, 0.4); }

    code {
      display: block;
      white-space: pre;
      color: rgba(255, 255, 255, 0.6);
    }

    :global(.line) {
      display: block;
    }

    :global(.line-num) {
      display: inline-block;
      width: 3ch;
      margin-right: 1rem;
      color: rgba(255, 255, 255, 0.25);
      user-select: none;
    }

    :global(.error-line) {
      background: rgba(255, 107, 107, 0.12);
      margin: 0 -1rem;
      padding: 0 1rem;
    }
  }

  .footer {
    padding: 1.25rem 2rem;
    border-top: 1px solid rgba(255, 255, 255, 0.08);
    display: flex;
    justify-content: flex-end;
    gap: 1rem;
  }

  .btn {
    font-family: '8bitwonder', monospace;
    font-size: 0.55rem;
    letter-spacing: 0.06em;
    padding: 0.6rem 1.2rem;
    border: 1px solid;
    background: transparent;
    cursor: pointer;
    transition: all 0.2s;

    &.copy-btn {
      color: #B87333;
      border-color: rgba(184, 115, 51, 0.4);

      &:hover {
        background: rgba(184, 115, 51, 0.1);
        border-color: #B87333;
      }
    }

    &.close-btn-footer {
      color: rgba(255, 255, 255, 0.5);
      border-color: rgba(255, 255, 255, 0.2);

      &:hover {
        background: rgba(255, 255, 255, 0.05);
        color: var(--c2);
      }
    }
  }
</style>
