<script>
  import { onMount } from 'svelte';

  export let value = 'Rubik';

  let isOpen = false;
  let systemFonts = [];
  let loadingFonts = false;
  let searchQuery = '';

  // Base fonts (Google Fonts + fallbacks)
  const baseFonts = [
    { name: 'Rubik', family: 'Rubik', category: 'google' },
    { name: 'EB Garamond', family: '"EB Garamond"', category: 'google' },
    { name: 'Old Standard TT', family: '"Old Standard TT"', category: 'google' },
    { name: 'Yeseva One', family: '"Yeseva One"', category: 'google' },
    { name: 'JetBrains Mono', family: '"JetBrains Mono"', category: 'google' },
    { name: 'System UI', family: 'system-ui', category: 'system' },
  ];

  // Common system fonts fallback - comprehensive list for Windows
  const commonSystemFonts = [
    { name: 'Arial', family: 'Arial', category: 'system' },
    { name: 'Arial Black', family: '"Arial Black"', category: 'system' },
    { name: 'Calibri', family: 'Calibri', category: 'system' },
    { name: 'Candara', family: 'Candara', category: 'system' },
    { name: 'Century Gothic', family: '"Century Gothic"', category: 'system' },
    { name: 'Helvetica', family: 'Helvetica', category: 'system' },
    { name: 'Segoe UI', family: '"Segoe UI"', category: 'system' },
    { name: 'Tahoma', family: 'Tahoma', category: 'system' },
    { name: 'Trebuchet MS', family: '"Trebuchet MS"', category: 'system' },
    { name: 'Verdana', family: 'Verdana', category: 'system' },
    { name: 'Franklin Gothic Medium', family: '"Franklin Gothic Medium"', category: 'system' },
    { name: 'Gill Sans MT', family: '"Gill Sans MT"', category: 'system' },
    { name: 'Times New Roman', family: '"Times New Roman"', category: 'system' },
    { name: 'Georgia', family: 'Georgia', category: 'system' },
    { name: 'Palatino Linotype', family: '"Palatino Linotype"', category: 'system' },
    { name: 'Book Antiqua', family: '"Book Antiqua"', category: 'system' },
    { name: 'Cambria', family: 'Cambria', category: 'system' },
    { name: 'Garamond', family: 'Garamond', category: 'system' },
    { name: 'Bodoni MT', family: '"Bodoni MT"', category: 'system' },
    { name: 'Rockwell', family: 'Rockwell', category: 'system' },
    { name: 'Consolas', family: 'Consolas', category: 'system' },
    { name: 'Courier New', family: '"Courier New"', category: 'system' },
    { name: 'Lucida Console', family: '"Lucida Console"', category: 'system' },
    { name: 'Cascadia Code', family: '"Cascadia Code"', category: 'system' },
    { name: 'Cascadia Mono', family: '"Cascadia Mono"', category: 'system' },
    { name: 'Impact', family: 'Impact', category: 'system' },
    { name: 'Comic Sans MS', family: '"Comic Sans MS"', category: 'system' },
    { name: 'Papyrus', family: 'Papyrus', category: 'system' },
    { name: 'Copperplate Gothic Bold', family: '"Copperplate Gothic Bold"', category: 'system' },
    { name: 'Lucida Handwriting', family: '"Lucida Handwriting"', category: 'system' },
    { name: 'Brush Script MT', family: '"Brush Script MT"', category: 'system' },
    { name: 'Segoe Script', family: '"Segoe Script"', category: 'system' },
    { name: 'Segoe Print', family: '"Segoe Print"', category: 'system' },
    { name: 'MV Boli', family: '"MV Boli"', category: 'system' },
    { name: 'Bahnschrift', family: 'Bahnschrift', category: 'system' },
    { name: 'Sitka Text', family: '"Sitka Text"', category: 'system' },
    { name: 'Sylfaen', family: 'Sylfaen', category: 'system' },
  ];

  onMount(async () => {
    loadingFonts = true;

    try {
      const res = await fetch('/api/fonts');
      if (res.ok) {
        const data = await res.json();
        if (data.fonts && data.fonts.length > 0) {
          systemFonts = data.fonts.map(name => ({
            name,
            family: `"${name}"`,
            category: 'system'
          }));
          console.log(`[FontPicker] Loaded ${systemFonts.length} fonts from backend`);
          loadingFonts = false;
          return;
        }
      }
    } catch (err) {
      console.log('[FontPicker] Backend API not available, trying fallbacks');
    }

    if ('queryLocalFonts' in window) {
      try {
        const fonts = await window.queryLocalFonts();
        const uniqueFonts = new Map();

        fonts.forEach(font => {
          if (!uniqueFonts.has(font.family)) {
            uniqueFonts.set(font.family, {
              name: font.family,
              family: `"${font.family}"`,
              category: 'local'
            });
          }
        });

        systemFonts = Array.from(uniqueFonts.values())
          .sort((a, b) => a.name.localeCompare(b.name));
        console.log(`[FontPicker] Loaded ${systemFonts.length} fonts from queryLocalFonts`);
        loadingFonts = false;
        return;
      } catch (err) {
        console.log('[FontPicker] Local fonts access denied');
      }
    }

    systemFonts = commonSystemFonts;
    console.log('[FontPicker] Using fallback font list');
    loadingFonts = false;
  });

  function selectFont(font) {
    value = font.name;
    isOpen = false;
    searchQuery = '';
  }

  function handleKeydown(e) {
    if (e.key === 'Escape') {
      isOpen = false;
      searchQuery = '';
    }
  }

  $: allFonts = [...baseFonts, ...systemFonts];
  $: filteredFonts = searchQuery
    ? allFonts.filter(f => f.name.toLowerCase().includes(searchQuery.toLowerCase()))
    : allFonts;
  $: selectedFont = allFonts.find(f => f.name === value) || baseFonts[0];
</script>

<svelte:window on:keydown={handleKeydown} />

<div class="font-picker">
  <button
    class="font-select"
    class:open={isOpen}
    on:click={() => isOpen = !isOpen}
  >
    <span class="font-preview" style="font-family: {selectedFont.family}">
      {selectedFont.name}
    </span>
    <span class="dropdown-arrow">{isOpen ? '▲' : '▼'}</span>
  </button>

  {#if isOpen}
    <div class="font-dropdown">
      <div class="search-box">
        <input
          type="text"
          class="search-input"
          placeholder="Search fonts..."
          bind:value={searchQuery}
          on:click|stopPropagation
        />
      </div>
      <div class="font-list">
        {#if loadingFonts}
          <div class="loading">Loading fonts...</div>
        {:else if filteredFonts.length === 0}
          <div class="no-results">No fonts found</div>
        {:else}
          {#each filteredFonts as font}
            <button
              class="font-option"
              class:selected={value === font.name}
              on:click={() => selectFont(font)}
            >
              <span class="font-name" style="font-family: {font.family}">
                {font.name}
              </span>
              <span class="font-category">{font.category}</span>
            </button>
          {/each}
        {/if}
      </div>
    </div>
  {/if}
</div>

<style lang="scss">
  .font-picker {
    position: relative;
  }

  .font-select {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    background: transparent;
    border: 3px solid rgba(0, 0, 0, 0.12);
    cursor: pointer;
    transition: all 0.2s ease;
    height: 44px;
    box-sizing: border-box;

    &:hover, &.open {
      background: rgba(0, 0, 0, 0.04);
      border-color: rgba(0, 0, 0, 0.25);
    }
  }

  .font-preview {
    font-size: 14px !important;
    line-height: 1.2 !important;
    color: var(--c1);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 150px;
  }

  .dropdown-arrow {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: rgba(0, 0, 0, 0.3);
  }

  .font-dropdown {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    margin-top: 4px;
    background: rgba(255, 255, 255, 0.97);
    border: 3px solid rgba(0, 0, 0, 0.15);
    z-index: 100;
    display: flex;
    flex-direction: column;
    max-height: 300px;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
  }

  .search-box {
    padding: 0.5rem;
    border-bottom: 3px solid rgba(0, 0, 0, 0.08);
  }

  .search-input {
    width: 100%;
    padding: 0.5rem 0.75rem;
    background: rgba(0, 0, 0, 0.04);
    border: 3px solid rgba(0, 0, 0, 0.1);
    color: var(--c1);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    outline: none;

    &::placeholder {
      color: rgba(0, 0, 0, 0.25);
    }

    &:focus {
      border-color: var(--c1);
    }
  }

  .font-list {
    flex: 1;
    overflow-y: auto;

    &::-webkit-scrollbar { width: 6px; }
    &::-webkit-scrollbar-track { background: rgba(0, 0, 0, 0.03); }
    &::-webkit-scrollbar-thumb {
      background: rgba(0, 0, 0, 0.12);
      &:hover { background: rgba(0, 0, 0, 0.2); }
    }
  }

  .loading,
  .no-results {
    padding: 1rem;
    text-align: center;
    color: rgba(0, 0, 0, 0.35);
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
  }

  .font-option {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.6rem 1rem;
    background: transparent;
    border: none;
    border-bottom: 3px solid rgba(0, 0, 0, 0.06);
    cursor: pointer;
    transition: background 0.15s ease;
    text-align: left;
    height: 40px;
    min-height: 40px;
    max-height: 40px;
    box-sizing: border-box;

    &:hover {
      background: rgba(0, 0, 0, 0.05);
    }

    &.selected {
      background: rgba(0, 0, 0, 0.08);

      .font-name {
        color: var(--c1);
        font-weight: 700;
      }
    }

    &:last-child {
      border-bottom: none;
    }
  }

  .font-name {
    font-size: 16px !important;
    line-height: 1.2 !important;
    color: var(--c1);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 180px;
  }

  .font-category {
    font-family: '8bitwonder', monospace;
    font-size: 1rem;
    color: rgba(0, 0, 0, 0.3);
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }
</style>
