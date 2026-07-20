# Shadow DOM for Custom/Example Players

Replace iframe-based rendering in `CustomPlayerRenderer.svelte` with Shadow DOM.

## Why

- ✅ CSS isolation (like iframe, no leaking)
- ✅ Programmatic `use:marquee` on `.title` / `.artist` inside shadow root
- ✅ Svelte `fly` transition on wrapper still works
- ✅ No iframe CORS/postMessage boilerplate
- ✅ `@font-face` from document works inside Shadow DOM (Chromium)
- ✅ Colors/font update via direct DOM, not postMessage
- ✅ Progress/timeline update via reactive `$:` → direct DOM queries inside shadowRoot

## Changes in `CustomPlayerRenderer.svelte`

### DELETED (iframe/postMessage stuff)
- `RUNTIME_SCRIPT` string constant (~130 lines)
- `<iframe bind:this={iframeEl}>`
- `srcdoc`-based rendering (`iframeSrcdoc`, `iframeReady`)
- `sendFullUpdate()` — was postMessage with progress/colors/font
- `onMessage()` — was listening for `unik-ready` from iframe
- `buildSrcdoc()` — was constructing iframe srcdoc with injected CSS + runtime script
- `injectedCSS` generation inside `buildSrcdoc()` — CSS variables move to a different place

### KEPT (refactored)
- `handleColorsUpdated()` — still needed to increment `colorsVersion`, which triggers `computeColors` to re-read `:root` CSS vars and push new colors into shadowRoot
- `formatTime()` — still needed for progress/timeline display inside shadowRoot
- `loadTemplate()` — still fetches HTML from API, but no longer passes through `buildSrcdoc()`
- `colors`, `font`, `title`, `artist`, `thumbnail`, `showAlways`, `isExample` props
- `{#if shouldShow}`, `{#key trackKey}`, `in:fly / out:fly`
- `error` / `loading` states

### ADDED
- `<div bind:this={shadowHost}>` with `style="overflow:hidden;width:100%;height:100%"`
- Shadow DOM initialization:
  ```javascript
  let shadowRoot = null;
  function renderToShadow(html) {
      shadowRoot = shadowHost.attachShadow({ mode: 'open' });
      shadowRoot.innerHTML = `<style>${injectedCSS}</style>${html}`;
  }
  ```
- CSS variable injection via `<style id="_unik-css">` inside shadowRoot, updated reactively:
  ```javascript
  $: if (shadowRoot) {
      const cssVars = `:root {\n${Object.entries(safeColors).map(([k,v]) => `  --${k}: ${v};`).join('\n')}\n  --font: "${font}", sans-serif;\n}`;
      let style = shadowRoot.querySelector('#_unik-css');
      if (!style) { style = shadowRoot.createElement('style'); style.id = '_unik-css'; shadowRoot.prepend(style); }
      style.textContent = cssVars;
  }
  ```
- `import { marquee } from '$lib/marquee.js'` + programmatic call:
  ```javascript
  $: updateMarquee(shadowRoot, trackKey);
  let marqueeCleanups = [];
  function updateMarquee(root, _keyTrigger) {
      marqueeCleanups.forEach(fn => fn());
      marqueeCleanups = [];
      if (!root) return;
      root.querySelectorAll('.title, .artist').forEach(el => {
          marqueeCleanups.push(marquee(el, { speed: 70, optGap: 69 }));
      });
  }
  ```
- Progress/timeline updates via reactive `$:` → direct shadowRoot DOM queries:
  ```javascript
  $: if (shadowRoot) {
      shadowRoot.querySelectorAll('[data-bind="progress-width"]').forEach(el => {
          el.style.width = ($trackProgress || 0) + '%';
      });
      shadowRoot.querySelectorAll('[data-bind="currentTime"]').forEach(el => {
          el.textContent = formatTime($trackPosition);
      });
      shadowRoot.querySelectorAll('[data-bind="totalTime"]').forEach(el => {
          el.textContent = formatTime($trackDuration);
      });
      shadowRoot.querySelectorAll('[data-bind="position"]').forEach(el => {
          el.textContent = Math.floor($trackPosition || 0);
      });
      shadowRoot.querySelectorAll('[data-bind="duration"]').forEach(el => {
          el.textContent = Math.floor($trackDuration || 0);
      });
      shadowRoot.querySelectorAll('[data-bind="playing"]').forEach(el => {
          el.setAttribute('data-playing', $isPlaying ? 'true' : 'false');
      });
  }
  ```

## Template for new players

`frontend/src/lib/players/_example.html`:
```html
<style>
/*                Colors:                  */
    var(--vibrant),      var(--muted),
    var(--lightVibrant), var(--lightMuted),
    var(--darkVibrant),  var(--darkMuted) 
/*                                         */
    * { font-family: "Rubik", sans-serif; }
</style>

<div>
  <p class="title">{{title}}</p>
  <p class="artist">{{artist}}</p>
  <img src="{{thumbnail}}" alt="cover" width="100" />
</div>
```

## No backend changes

## Edge Cases

- **No `.title`/`.artist` elements**: `updateMarquee` handles gracefully
- **Shadow DOM not supported**: rare, OBS Chromium supports since ~2019
- **Multiple instances**: each has its own shadow host + root
- **TrackKey changes**: wrapper destroyed/recreated by `{#key}`, shadow root fresh
- **Reset in Editor**: works via existing API, no changes needed

## Task list

1. Open `frontend/src/lib/components/CustomPlayerRenderer.svelte`
2. Remove: `RUNTIME_SCRIPT`, `iframeEl`, `iframeSrcdoc`, `iframeReady`, `sendFullUpdate()`, `onMessage()`, `buildSrcdoc()`
3. Import `marquee` from `$lib/marquee.js`
4. Add `shadowHost` binding (`<div bind:this={shadowHost}>`)
5. Add `renderToShadow(html)` function
6. Add reactive CSS variable injection (`#_unik-css` style element inside shadowRoot)
7. Add `updateMarquee()` with `$:` trigger on `trackKey`
8. Add reactive progress/timeline DOM updates (data-bind queries)
9. Keep `handleColorsUpdated()`, `formatTime()`, `loadTemplate()`, `shouldShow`, fly transitions
10. Build `npm run build` and verify no errors
11. Quick test in dev mode

## Validation

- `npm run build` passes
- Custom players render in card preview (main page)
- Custom players render in `/player` route
- `.title` / `.artist` have working marquee
- Colors update on track change
- Progress/timeline bindings work
- `{#if shouldShow}` shows/hides correctly
- `in:fly / out:fly` transition plays on track change
