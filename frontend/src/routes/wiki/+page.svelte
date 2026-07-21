<script>
  import { fade } from "svelte/transition";
  import { language } from "$lib/stores/stores.js";

  let lang = 'ru';
  $: lang = $language || 'ru';

  const BIND_CODE = `&lt;div style="
  height: 4px;
  background: rgba(255,255,255,0.08);
  border-radius: 4px;
  overflow: hidden;
"&gt;
  &lt;div data-bind="progress-width" style="
    height: 100%;
    background: var(--vibrant);
    border-radius: 4px;
    transition: width 0.3s linear;
  "&gt;&lt;/div&gt;
&lt;/div&gt;`;

  const GOOGLE_CODE = `@import url('https://fonts.googleapis.com/css2?family=Roboto:wght@400;700&display=swap');

* { font-family: 'Roboto', sans-serif; }`;

  const COLORS_CODE = `background: var(--darkMuted);
border: 2px solid var(--vibrant);
color: var(--lightVibrant);`;

  const L = {
    h1:          { ru: 'Документация',           en: 'Documentation' },

    start_title: { ru: 'Начало',                 en: 'Getting started' },
    start_p:     { ru: 'Тут есть вся нужна инфа по поводу кастомизации плееров.', en: '' },

    vars_title:  { ru: 'Переменные',             en: 'Variables' },
    vars_p:      { ru: 'Двойные фигурные скобки заменяются на данные из текущего трека:', en: 'Double curly braces are replaced with data from the current track:' },
    vars_track:  { ru: 'Название трека',          en: 'Track title' },
    vars_artist: { ru: 'Исполнитель',             en: 'Artist' },
    vars_thumb:  { ru: 'Обложка трека',           en: 'Cover image' },
    vars_time:   { ru: 'Текущее время "2:34"',    en: 'Current time "2:34"' },
    vars_dur:    { ru: 'Длительность "4:12"',     en: 'Duration "4:12"' },
    vars_prog:   { ru: 'Прогресс 0–100',          en: 'Progress 0–100' },

    bind_title:  { ru: 'Прогресс-бар',           en: 'Progress bar' },
    bind_intro:  { ru: 'Прогресс-бар обновляется автоматически. Ниже — готовый компонент и его базовая стилизация.', en: 'The progress bar updates automatically. Below — the ready component and its base styles.' },
    bind_component: { ru: 'Просто добавь в свой HTML плеера:', en: 'Just add to your HTML player:' },
    bind_example: { ru: 'Или используй HTML напрямую (чтобы кастомизировать):', en: 'Or use the HTML directly (to customize):' },
    bind_styles:  { ru: 'Базовые CSS стили прогресс-бара (инжектятся автоматически, но ты можешь переопределить):', en: 'Base CSS styles (injected automatically, but you can override):' },
    bind_params:  { ru: 'Параметры компонента:', en: 'Component parameters:' },
    bind_param_height: { ru: 'высота полосы', en: 'bar height' },
    bind_param_radius: { ru: 'скругление', en: 'border radius' },
    bind_param_time: { ru: 'показывать время (true/false)', en: 'show time (true/false)' },
    bind_ref:    { ru: 'Классы для кастомизации:', en: 'Customization classes:' },
    bind_ref_container: { ru: 'контейнер (flex, ширина 100%)', en: 'flex container, full width' },
    bind_ref_time: { ru: 'время (семейство, размер, цвет, тень)', en: 'time label (font, size, color, text-shadow)' },
    bind_ref_current: { ru: 'текущее время (text-align: right)', en: 'current time (text-align: right)' },
    bind_ref_total: { ru: 'длительность (opacity: 0.6)', en: 'duration (opacity: 0.6)' },
    bind_ref_track: { ru: 'трек (flex: 1, overflow: hidden, фон --darkMuted)', en: 'track (flex: 1, overflow: hidden, bg: --darkMuted)' },
    bind_ref_fill: { ru: 'заполнение (градиент --vibrant → --lightVibrant)', en: 'fill (gradient --vibrant → --lightVibrant)' },
    bind_ref_glow: { ru: 'свечение позади fill', en: 'glow behind fill' },
    bind_time:   { ru: 'Отдельное время: <code>data-bind="currentTime"</code> и <code>data-bind="totalTime"</code>.', en: 'Standalone time: <code>data-bind="currentTime"</code> and <code>data-bind="totalTime"</code>.' },

    comp_tag:    { ru: 'Использование',            en: 'Usage' },
    comp_tag_p:  { ru: 'Напиши в своём HTML-плеере один из тегов ниже — он автоматически превратится в готовый прогресс-бар при рендере. Все стили уже предустановлены.', en: 'Write one of these tags in your HTML player — it automatically becomes a full progress bar on render. All styles are pre-installed.' },

    marquee_title:{ ru: 'Бегущая строка',          en: 'Marquee' },
    marquee_p:    { ru: 'ТОЛЬКО классы <code>.title</code> и <code>.artist</code> автоматически включают скролл, если текст не влезает.', en: 'ONLY Classes <code>.title</code> and <code>.artist</code> auto-scroll when text overflows.' },

    colors_title:{ ru: 'Цвета из обложки',         en: 'Colors from cover' },
    colors_p:    { ru: 'Vibrant.js извлекает цвета из обложки трека и записывает CSS-переменные. Используй их в любом месте:', en: 'Vibrant.js extracts colors from the cover and sets CSS variables. Use them anywhere:' },
    colors_note: { ru: 'Пример использования цветов:', en: '' },

    google_title:{ ru: 'Google Fonts',             en: 'Google Fonts' },
    google_p1:   { ru: 'Можно добавить шрифты из Google Fonts через <code>@import</code> :', en: '' },
    google_p2:   { ru: 'Если @import не первый — он не сработает.', en: 'If @import is not first, it will be ignored.' },

    nav_start:   { ru: 'Начало',                  en: 'Start' },
    nav_vars:    { ru: 'Переменные',              en: 'Variables' },
    nav_bind:    { ru: 'Прогресс-бар',            en: 'Progress bar' },
    nav_marquee: { ru: 'Бегущая строка',          en: 'Marquee' },
    nav_colors:  { ru: 'Цвета',                   en: 'Colors' },
    nav_google:  { ru: 'Google Fonts',            en: 'Fonts' },
  };

  const sections = [
    { id: 'start',   key: 'nav_start' },
    { id: 'vars',    key: 'nav_vars' },
    { id: 'bind',    key: 'nav_bind' },
    { id: 'marquee', key: 'nav_marquee' },
    { id: 'colors',  key: 'nav_colors' },
    { id: 'google',  key: 'nav_google' },
  ];

  const CLASSES = [
    { sel: '.progress-container', desc: 'контейнер прогресс-бара' },
    { sel: '.time',               desc: 'метка времени' },
    { sel: '.time.current',       desc: 'текущее время' },
    { sel: '.time.total',         desc: 'общая длительность' },
    { sel: '.progress-bar',       desc: 'трек (фоновая полоса)' },
    { sel: '.progress-fill',      desc: 'заполненная часть' }
  ];

  let hoverSel = '';
</script>

<svelte:head>
  <title>UnikPlayer — Docs</title>
</svelte:head>

<div class="top-bar">
  <a href="/" class="logo">UNIKPLAYER</a>
  <span class="spacer"></span>
  <a href="https://github.com/UNIKNOW0/unik-player" target="_blank" class="github-link" rel="noopener noreferrer" aria-label="GitHub">
    <svg viewBox="0 0 16 16" width="18" height="18" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
  </a>
  <button class="lang-btn" on:click={() => language.set(lang === 'ru' ? 'en' : 'ru')}>
    {lang === 'ru' ? 'EN' : 'RU'}
  </button>
</div>

<div class="layout">
  <nav class="sidebar">
    {#each sections as s}
      <a href="#{s.id}" class="nav-item">{L[s.key][lang]}</a>
    {/each}
  </nav>

  <main class="content">
    {#key lang}
    <div in:fade={{ duration: 200 }}>

    <section id="start">
      <h1>{L.h1[lang]}</h1>
      <div class="block">
        <p>{@html L.start_p[lang]}</p>
      </div>
    </section>

    <section id="vars">
      <h2>{L.vars_title[lang]}</h2>
      <div class="block">
        <p>{L.vars_p[lang]}</p>
        <table><tbody>
          <tr><td class="cell">{'{'}{'{'}title{'}'}{'}'}</td><td>{L.vars_track[lang]}</td></tr>
          <tr><td class="cell">{'{'}{'{'}artist{'}'}{'}'}</td><td>{L.vars_artist[lang]}</td></tr>
          <tr><td class="cell">{'{'}{'{'}thumbnail{'}'}{'}'}</td><td>{L.vars_thumb[lang]}</td></tr>
          <tr><td class="cell">{'{'}{'{'}currentTime{'}'}{'}'}</td><td>{L.vars_time[lang]}</td></tr>
          <tr><td class="cell">{'{'}{'{'}totalTime{'}'}{'}'}</td><td>{L.vars_dur[lang]}</td></tr>
          <tr><td class="cell">{'{'}{'{'}progress{'}'}{'}'}</td><td>{L.vars_prog[lang]}</td></tr>
        </tbody></table>
      </div>
    </section>

    <section id="bind">
      <h2>{L.bind_title[lang]}</h2>
      <p>{L.bind_intro[lang]}</p>

      <h3>{L.comp_tag[lang]}</h3>
      <p>{@html L.comp_tag_p[lang]}</p>
      <pre><code>&lt;ProgressBarComponent height="4px" borderRadius="2px" showTime /&gt;</code></pre>
      <p>{L.bind_params[lang]}</p>
      <table><tbody>
        <tr><td class="cell">height</td><td>{L.bind_param_height[lang]} (по умолч. 4px)</td></tr>
        <tr><td class="cell">borderRadius</td><td>{L.bind_param_radius[lang]} (по умолч. 2px)</td></tr>
        <tr><td class="cell">ShowTime</td><td>{L.bind_param_time[lang]} (по умолч. скрыт)</td></tr>
      </tbody></table>

      <p>{L.bind_styles[lang]}</p>
      <div class="block">
        <div class="code-ref">
{#each CLASSES as c}
<span class="code-line" class:active={hoverSel && (hoverSel === c.sel || c.sel === '.time' && hoverSel.startsWith('.time'))} on:mouseenter={() => hoverSel=c.sel} on:mouseleave={() => hoverSel=''}>
  <span class="cm">// {c.desc}</span>
  <span class="s">{c.sel}</span> {'{'} ... {'}'}
</span>
{/each}
        </div>
      </div>

      <div class="block">
        <div class="bar-demo" style="--dv:#C48C37;--dlv:#E4AFAB;--ddm:#4B4F25;">
          <div class="bar-container" class:hovered={hoverSel === '.progress-container'} on:mouseenter={() => hoverSel='.progress-container'} on:mouseleave={() => hoverSel=''}>
            <span class="bar-time bar-cur" class:hovered={hoverSel === '.time' || hoverSel === '.time.current'} on:mouseenter={() => hoverSel='.time.current'} on:mouseleave={() => hoverSel=''}>2:28</span>
            <div class="bar-track" class:hovered={hoverSel === '.progress-bar'} on:mouseenter={() => hoverSel='.progress-bar'} on:mouseleave={() => hoverSel=''}>
              <div class="bar-fill" style="width:34%;" class:hovered={hoverSel === '.progress-fill'} on:mouseenter={() => hoverSel='.progress-fill'} on:mouseleave={() => hoverSel=''}></div>
            </div>
            <span class="bar-time bar-total" class:hovered={hoverSel === '.time' || hoverSel === '.time.total'} on:mouseenter={() => hoverSel='.time.total'} on:mouseleave={() => hoverSel=''}>7:27</span>
          </div>
        </div>
      </div>
    </section>

    <section id="marquee">
      <h2>{L.marquee_title[lang]}</h2>
      <div class="block">
        <p>{@html L.marquee_p[lang]}</p>
        <pre><code>&lt;div class="title"&gt;{'{'}{'{'}title{'}}'}&lt;/div&gt;
&lt;div class="artist"&gt;{'{'}{'{'}artist{'}}'}&lt;/div&gt;</code></pre>
      </div>
    </section>

    <section id="colors">
      <h2>{L.colors_title[lang]}</h2>
      <div class="block">
        <p>{@html L.colors_p[lang]}</p>
        <div class="colors-row">
          <table><tbody>
            <tr><td class="cell"><code>--vibrant</code></td><td><span class="sw" style="background:#C48C37;"></span> #C48C37</td></tr>
            <tr><td class="cell"><code>--lightVibrant</code></td><td><span class="sw" style="background:#E4AFAB;"></span> #E4AFAB</td></tr>
            <tr><td class="cell"><code>--darkVibrant</code></td><td><span class="sw" style="background:#86611C;"></span> #86611C</td></tr>
            <tr><td class="cell"><code>--muted</code></td><td><span class="sw" style="background:#B0855D;"></span> #B0855D</td></tr>
            <tr><td class="cell"><code>--lightMuted</code></td><td><span class="sw" style="background:#CCABA4;"></span> #CCABA4</td></tr>
            <tr><td class="cell"><code>--darkMuted</code></td><td><span class="sw" style="background:#4B4F25;"></span> #4B4F25</td></tr>
          </tbody></table>
          <img src="/exampleForWiki.png" alt="Color palette example" class="palette-img" />
        </div>
        <p>{L.colors_note[lang]}</p>
        <pre><code>{@html COLORS_CODE}</code></pre>
      </div>
    </section>

    <section id="google">
      <h2>{L.google_title[lang]}</h2>
      <div class="block">
        <p>{@html L.google_p1[lang]}</p>
        <pre><code>{@html GOOGLE_CODE}</code></pre>
        <p>{@html L.google_p2[lang]}</p>
      </div>
    </section>

    </div>
    {/key}
  </main>
</div>

<style>
  :root{
    --hoovered:#8661C1;
  }

  :global(body) { background: #000; margin: 0; }
  :global(.bg-gradient) { background: #000 !important; }

  .top-bar {
    position: fixed; top: 0; left: 0; right: 0; z-index: 100;
    display: flex; align-items: center; gap: 1rem;
    padding: 0.75rem 1.5rem; background: #000;
    border-bottom: 1px solid rgba(255,255,255,0.06);
  }
  .logo {
    font-family: 'Unbounded', monospace; font-size: 2rem; font-weight: 800;
    letter-spacing: 0.1em; color: rgba(255,255,255,0.5);
    text-decoration: none; transition: all 0.3s ease;
  }
  .logo:hover { color: #fff; font-weight: 300; }
  .spacer { margin-left: auto; }
  .lang-btn {
    background: none; border: 1px solid rgba(255,255,255,0.15);
    color: rgba(255,255,255,0.6); padding: 0.2rem 0.6rem; cursor: pointer;
    font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; border-radius: 4px;
    transition: all 0.2s;
  }
  .lang-btn:hover { border-color: rgba(255,255,255,0.4); color: #fff; }
  .github-link { color: rgba(255,255,255,0.5); transition: color 0.2s; display: flex; align-items: center; }
  .github-link:hover { color: #fff; }

  .layout { display: flex; justify-content: center; padding-top: 3.2rem; min-height: 100vh; font-family: 'Rubik', sans-serif; background: #000; }
  .sidebar { width: 220px; min-width: 220px; padding: 1.5rem 0; position: sticky; top: 3.2rem; height: calc(100vh - 3.2rem); overflow-y: auto; }
  .nav-item { display: block; padding: 0.4rem 1.5rem; font-size: 1rem; color: rgba(255,255,255,0.8); text-decoration: none; border-left: 2px solid transparent; border-right: 2px solid transparent; transition: all 0.2s ease; }
  .nav-item:hover { color: #fff; border-left-color: #fff; border-right-color: #fff; font-weight: 700; }
  .content { flex: 1; max-width: 700px; padding: 1.5rem 3rem 6rem; }

  h1 { font-family: 'Unbounded', sans-serif; font-size: 2rem; font-weight: 600; margin: 0 0 1.5rem; color: #fff; }
  h2 { font-family: 'Unbounded', sans-serif; font-size: 1.3rem; font-weight: 500; margin: 3rem 0 1rem; padding-bottom: 0.4rem; border-bottom: 1px solid rgba(255,255,255,0.06); color: #fff; }
  h3 { font-family: 'Unbounded', sans-serif; font-size: 1rem; font-weight: 500; margin: 1.5rem 0 0.5rem; color: #fff; }
  section { scroll-margin-top: 4rem; }
  p { line-height: 1.7; margin: 0.6rem 0; font-size: 1rem; color: rgba(255,255,255,0.65); }
  strong { color: rgba(255,255,255,0.9); }
  code { font-family: 'JetBrains Mono', monospace; font-size: 0.9em; color:white; background: rgba(255,255,255,0.04); padding: 0.15rem 0.4rem; border-radius: 3px; }
  pre { background: rgba(255,255,255,0.03); padding: 1rem; border-radius: 6px; overflow-x: auto; font-size: 1rem; border: 1px solid rgba(255,255,255,0.08); margin: 0.8rem 0; }
  pre code { background: transparent; padding: 0; }

  .code-line { display: block; padding: 0.15rem 0.3rem; margin-bottom: 0.4rem; transition: background 0.15s; border-radius: 3px; }
  .code-line.active { background: rgba(255,255,255,0.1); }
  .code-line .cm { display: block; color: rgba(255,255,255,0.35); font-style: italic; }
  .code-line .s { color: var(--c2, #fff); font-weight: 500; }


  .bar-demo { display: flex; flex-direction: column; margin-top: 0.5rem; padding: 0.5rem; border: 1px solid rgba(255,255,255,0.06); border-radius: 6px; }
  .bar-container { display: flex; align-items: center; gap: 0; width: 100%; height:10px; transition: all 0.15s; }
  .bar-time { font-family: "JetBrains Mono",monospace; font-size: 1rem; color: var(--dlv,#E4AFAB); min-width: 2.2rem; transition: color 0.2s, text-shadow 0.2s; }
  .bar-cur { text-align: right; padding-right: 0.3rem; }
  .bar-total { text-align: left; opacity: 0.6; padding-left: 0.3rem; }
  .bar-track { flex: 1; position: relative; height: 8px; background: var(--ddm,#4B4F25); overflow: hidden; border-radius: 2px; transition: all 0.2s; }
  .bar-fill { height: 100%; background: linear-gradient(90deg,var(--dv,#C48C37),var(--dlv,#E4AFAB)); border-radius: 2px; transition: width 0.1s linear, background 0.2s, box-shadow 0.2s; }
  .bar-container.hovered { outline: 2px solid var(--hoovered); outline-offset: 3px; border-radius: 4px; }

  .bar-time.hovered  { color:            var(--hoovered);}
  .bar-track.hovered { background-color: var(--hoovered);}
  .bar-fill.hovered  { background-color: var(--hoovered);}
  .bar-fill.hovered  { background:       var(--hoovered);}
  table { border-collapse: collapse; margin: 0.8rem 0; font-size: 1rem; }
  td { padding: 0.4rem 0.6rem; border-bottom: 1px solid rgba(255,255,255,0.05); vertical-align: middle; color: rgba(255,255,255,0.65); }
  .cell { font-family: 'JetBrains Mono', monospace; font-size: 0.85em; white-space: nowrap; padding-right: 1.5rem; color: rgba(255,255,255,0.9); }
  .sw { display: inline-block; width: 1.2em; height: 1.2em; border-radius: 3px; vertical-align: middle; margin-right: 0.4em; border: 1px solid rgba(255,255,255,0.1); }

  .colors-row { display: flex; gap: 1.5rem; justify-content:space-between; margin: 0.8rem 0; }
  .palette-img { width: 170px; border-radius: 6px; border: 1px solid rgba(255,255,255,0.08); flex-shrink: 0; }

  .block{ 
    border: 1px solid rgba(255,255,255,0.08);
    border-radius: 6px;
    padding: 0.25rem;
    margin: 0.8rem 0;
  }
</style>


1








