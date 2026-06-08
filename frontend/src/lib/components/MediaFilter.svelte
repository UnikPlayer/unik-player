<script>
  import { onMount, onDestroy } from 'svelte';
  import { language } from '$lib/stores/stores.js';

  export let visible = false;
  export let onClose = () => {};

  const isBrowser = typeof window !== 'undefined';
  const API_BASE = isBrowser && (window.location.port === '7270' || window.location.port === '5173')
    ? '' : '';

  let mode = 'allowAll';
  let sources = [];
  let seenSources = [];
  let sourceInfo = [];
  let loading = true;
  let showContent = false;
  let pollInterval = null;

  let anim = 'hidden';
  let closing = false;

  const t = {
    en: {
      title: 'MEDIA FILTER',
      modeLabel: 'MODE',
      allowAll: 'Listen to everything',
      allowOnly: 'Listen only to selected',
      blockOnly: 'Listen to everything except',
      recommended: 'Recommended',
      sourcesLabel: 'SOURCES',
      noSources: 'Play media in any app so sources appear here',
      noMediaSource: 'No media',
      save: 'SAVE',
      cancel: 'CANCEL',
    },
    ru: {
      title: 'ФИЛЬТР МЕДИА',
      modeLabel: 'РЕЖИМ',
      allowAll: 'Слушать всё',
      allowOnly: 'Слушать только выбранные',
      blockOnly: 'Слушать всё кроме',
      recommended: 'Рекомендуется',
      sourcesLabel: 'ИСТОЧНИКИ',
      noSources: 'Запусти медиа в любом приложении, чтобы источники появились здесь',
      noMediaSource: 'Нет медиа',
      save: 'СОХРАНИТЬ',
      cancel: 'ОТМЕНА',
    }
  };

  $: texts = t[$language] || t.ru;

  $: if (visible) {
    showContent = false;
    loading = true;
    setTimeout(() => { showContent = true; }, 300);
    loadFilter();
    startPolling();
    if (anim === 'hidden') {
      requestAnimationFrame(() => { anim = 'fly-in'; closing = false; });
    }
  } else {
    stopPolling();
  }

  onDestroy(() => {
    stopPolling();
  });

  function startPolling() {
    stopPolling();
    pollInterval = setInterval(pollSources, 1500);
  }

  function stopPolling() {
    if (pollInterval) {
      clearInterval(pollInterval);
      pollInterval = null;
    }
  }

  async function pollSources() {
    try {
      const res = await fetch(`${API_BASE}/api/media-filter`);
      if (res.ok) {
        const data = await res.json();
        seenSources = [...(data.seenSources || [])];
        sourceInfo = [...(data.sourceInfo || [])];
      }
    } catch (e) {}
  }

  async function loadFilter() {
    loading = true;
    try {
      const res = await fetch(`${API_BASE}/api/media-filter`);
      if (res.ok) {
        const data = await res.json();
        mode = data.mode || 'allowAll';
        sources = [...(data.sources || [])];
        seenSources = [...(data.seenSources || [])];
        sourceInfo = [...(data.sourceInfo || [])];
      }
    } catch (e) {
      console.error('[MediaFilter] Failed to load:', e);
    }
    loading = false;
  }

  function toggleSource(appId) {
    if (sources.includes(appId)) {
      sources = sources.filter(s => s !== appId);
    } else {
      sources = [...sources, appId];
    }
  }

  async function handleSave() {
    try {
      await fetch(`${API_BASE}/api/media-filter`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ mode, sources, seenSources })
      });
    } catch (e) {
      console.error('[MediaFilter] Failed to save:', e);
    }
    doClose();
  }

  function doClose() {
    if (closing) return;
    closing = true;
    anim = 'fly-out';
    setTimeout(() => {
      onClose();
      closing = false;
      anim = 'hidden';
      blobs = null;
    }, 500);
  }

  // Cloud blob system (same as CustomPlayerUploader)
  const S = 8;

  class CloudBlob {
    constructor(x,y,r){
      this.hx=x;this.hy=y;this.r=r;this.dx=0;this.dy=0;this.vx=0;this.vy=0;
      this.wph1=Math.random()*Math.PI*2;this.wph2=Math.random()*Math.PI*2;
      this.wamp=0.15+Math.random()*0.2;
    }
    update(mx,my,t){
      const wx=Math.sin(t*.008+this.wph1)*this.wamp;
      const wy=Math.cos(t*.006+this.wph2)*this.wamp*0.7;
      const px=this.hx+this.dx,py=this.hy+this.dy;
      const ex=px-mx,ey=py-my,d=Math.sqrt(ex*ex+ey*ey)+.001;
      const zone=this.r*4;
      if(d<zone){const f=Math.pow(1-d/zone,1.5)*3.5;this.vx+=(ex/d)*f;this.vy+=(ey/d)*f;}
      this.vx+=-(this.dx-wx)*.08;this.vy+=-(this.dy-wy)*.08;
      this.vx*=.78;this.vy*=.78;this.dx+=this.vx;this.dy+=this.vy;
    }
  }

  function makeCloudBlobs(WS,HS,offX=0,offY=0){
    const b=[],cx=WS/2+offX,cy=HS/2+offY;
    const spheres=[
      // Left stripe
      {x:-0.50,y:-0.70,r:0.11,n:8},{x:-0.54,y:-0.58,r:0.10,n:8},
      {x:-0.47,y:-0.46,r:0.11,n:8},{x:-0.52,y:-0.34,r:0.10,n:8},
      {x:-0.48,y:-0.22,r:0.11,n:8},{x:-0.53,y:-0.10,r:0.10,n:8},
      {x:-0.46,y:0.02,r:0.11,n:8},{x:-0.51,y:0.14,r:0.10,n:8},
      {x:-0.49,y:0.26,r:0.11,n:8},{x:-0.54,y:0.38,r:0.10,n:8},
      {x:-0.47,y:0.50,r:0.11,n:8},{x:-0.52,y:0.62,r:0.10,n:8},
      {x:-0.48,y:0.70,r:0.11,n:8},
      // Right stripe
      {x:0.50,y:-0.70,r:0.11,n:8},{x:0.54,y:-0.58,r:0.10,n:8},
      {x:0.47,y:-0.46,r:0.11,n:8},{x:0.52,y:-0.34,r:0.10,n:8},
      {x:0.48,y:-0.22,r:0.11,n:8},{x:0.53,y:-0.10,r:0.10,n:8},
      {x:0.46,y:0.02,r:0.11,n:8},{x:0.51,y:0.14,r:0.10,n:8},
      {x:0.49,y:0.26,r:0.11,n:8},{x:0.54,y:0.38,r:0.10,n:8},
      {x:0.47,y:0.50,r:0.11,n:8},{x:0.52,y:0.62,r:0.10,n:8},
      {x:0.48,y:0.70,r:0.11,n:8},
    ];
    const SCALE=1.0;
    for(const sp of spheres){
      const scx=cx+sp.x*WS*SCALE,scy=cy+sp.y*HS*SCALE;
      const sr=sp.r*Math.min(WS,HS)*SCALE;
      for(let i=0;i<sp.n;i++){
        const a=Math.random()*Math.PI*2,dist=Math.pow(Math.random(),.55);
        b.push(new CloudBlob(scx+Math.cos(a)*sr*dist,scy+Math.sin(a)*sr*dist, 3+Math.random()*5));
      }
    }
    return b;
  }

  let blobs = null;
  let mx = -9999, my = -9999;
  let cloudCanvas = null;
  let contentEl = null;

  onMount(() => {
    const cs = getComputedStyle(document.documentElement);
    function parseCSSColor(varName){
      const v=cs.getPropertyValue(varName).trim();
      if(!v)return[0,0,0];
      if(v.startsWith('#')){
        const h=v.replace('#','');
        if(h.length===3)return[parseInt(h[0]+h[0],16),parseInt(h[1]+h[1],16),parseInt(h[2]+h[2],16)];
        return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];
      }
      const m=v.match(/(\d+)/g);
      return m?[+m[0],+m[1],+m[2]]:[0,0,0];
    }
    const cCloud=parseCSSColor('--c-cloud');
    const cOutline=parseCSSColor('--c-cloud-outline');

    const _off1=document.createElement('canvas');
    const _off2=document.createElement('canvas');

    const PAD=300;
    let lastEW=0;

    function renderCloud(canvas, t){
      if(!canvas) return;
      const el = contentEl;
      if(!el) return;
      const ew=el.offsetWidth;
      const eh=window.innerHeight;
      if(ew<2||eh<2) return;

      if(ew!==lastEW){
        canvas.style.left=-PAD+'px';
        canvas.style.width=(ew+PAD*2)+'px';
        canvas.style.height=(eh+PAD*2)+'px';
        lastEW=ew;
        blobs=null;
      }
      canvas.style.top=(-el.parentElement.getBoundingClientRect().top)+'px';

      const TW=Math.ceil((ew+PAD*2)/S), TH=Math.ceil((eh+PAD*2)/S);
      const WS=Math.ceil(ew/S), HS=Math.ceil(eh/S);

      if(!blobs) blobs=makeCloudBlobs(WS,HS,Math.ceil(PAD/S),Math.ceil(PAD/S));

      if(_off1.width!==TW||_off1.height!==TH){_off1.width=TW;_off1.height=TH;}
      if(_off2.width!==TW||_off2.height!==TH){_off2.width=TW;_off2.height=TH;}
      if(canvas.width!==TW||canvas.height!==TH){canvas.width=TW;canvas.height=TH;}

      const c1=_off1.getContext('2d');
      c1.clearRect(0,0,TW,TH);
      for(const bl of blobs){
        const bx=bl.hx+bl.dx,by=bl.hy+bl.dy;
        const g=c1.createRadialGradient(bx,by,0,bx,by,bl.r);
        g.addColorStop(0,'rgba(255,255,255,1)');
        g.addColorStop(.5,'rgba(255,255,255,.85)');
        g.addColorStop(1,'rgba(255,255,255,0)');
        c1.fillStyle=g;c1.beginPath();c1.arc(bx,by,bl.r,0,Math.PI*2);c1.fill();
      }

      const c2=_off2.getContext('2d');
      c2.clearRect(0,0,TW,TH);
      c2.filter='blur(2px)';c2.drawImage(_off1,0,0);c2.filter='none';
      const img=c2.getImageData(0,0,TW,TH);
      const d=img.data;
      for(let i=0;i<d.length;i+=4){
        const v=d[i];
        if(v>60){d[i]=cCloud[0];d[i+1]=cCloud[1];d[i+2]=cCloud[2];d[i+3]=255;}
        else if(v>30){d[i]=cOutline[0];d[i+1]=cOutline[1];d[i+2]=cOutline[2];d[i+3]=255;}
        else{d[i]=d[i+1]=d[i+2]=d[i+3]=0;}
      }
      c2.putImageData(img,0,0);
      const dctx=canvas.getContext('2d');
      dctx.imageSmoothingEnabled=false;
      dctx.clearRect(0,0,TW,TH);
      dctx.drawImage(_off2,0,0,TW,TH);
    }

    function onMM(e){
      if(!cloudCanvas) return;
      const r=cloudCanvas.getBoundingClientRect();
      if(r.width<1) return;
      mx=(e.clientX-r.left)/r.width*cloudCanvas.width;
      my=(e.clientY-r.top)/r.height*cloudCanvas.height;
    }
    function onML(){ mx=-9999;my=-9999; }
    window.addEventListener('mousemove',onMM);
    window.addEventListener('mouseleave',onML);

    let raf,last=0,tick=0;
    function frame(ts){
      raf=requestAnimationFrame(frame);
      if(!visible){ lastEW=0; blobs=null; return; }
      if(ts-last<16)return;last=ts;tick++;
      if(blobs) for(const b of blobs) b.update(mx,my,tick);
      renderCloud(cloudCanvas,tick);
    }
    raf=requestAnimationFrame(frame);

    return ()=>{
      cancelAnimationFrame(raf);
      window.removeEventListener('mousemove',onMM);
      window.removeEventListener('mouseleave',onML);
      _off1.width = _off1.height = 0;
      _off2.width = _off2.height = 0;
      if (cloudCanvas) cloudCanvas.width = cloudCanvas.height = 0;
      blobs = null;
    };
  });
</script>

{#if visible}
  <div
    class="filter-backdrop"
    class:closing
    on:click|self={doClose}
    on:keydown={(e) => e.key === 'Escape' && doClose()}
    role="dialog"
    tabindex="-1"
  >
    <div class="filter-stage" class:fly-in={anim==='fly-in'} class:fly-out={anim==='fly-out'} class:hidden={anim==='hidden'}>
      <canvas bind:this={cloudCanvas} class="cloud-cv"></canvas>
      <div class="filter-content" bind:this={contentEl}>

        <div class="filter-title">{texts.title}</div>
        <button class="close-btn" on:click={doClose}>&times;</button>

        {#if loading}
          <div class="loading">...</div>
        {:else}

          <!-- Mode selector -->
          <div class="section">
            <span class="section-label">{texts.modeLabel}</span>
            <div class="mode-options">
              <label class="mode-option" class:active={mode === 'allowAll'}>
                <input type="radio" bind:group={mode} value="allowAll" />
                <span class="radio-dot"></span>
                <span>{texts.allowAll}</span>
              </label>
              <label class="mode-option" class:active={mode === 'allowOnly'}>
                <input type="radio" bind:group={mode} value="allowOnly" />
                <span class="radio-dot"></span>
                <span>{texts.allowOnly}</span>
                <span class="rec-badge">{texts.recommended}</span>
              </label>
              <label class="mode-option" class:active={mode === 'blockOnly'}>
                <input type="radio" bind:group={mode} value="blockOnly" />
                <span class="radio-dot"></span>
                <span>{texts.blockOnly}</span>
              </label>
            </div>
          </div>

          <!-- Sources list -->
          {#if mode !== 'allowAll'}
            <div class="section">
              <span class="section-label">{texts.sourcesLabel}</span>
              {#if sourceInfo.length === 0}
                <p class="no-sources">{texts.noSources}</p>
              {:else}
                <div class="sources-list">
                  {#each sourceInfo as info (`${info.id}-${info.title}-${info.isPlaying}`)}
                    <label class="source-item" class:checked={sources.includes(info.id)}>
                      <input
                        type="checkbox"
                        checked={sources.includes(info.id)}
                        on:change={() => toggleSource(info.id)}
                      />
                      <span class="checkbox-box"></span>
                      <div class="source-info">
                        <span class="source-name">{info.displayName || info.id}</span>
                        {#if info.title}
                          <span class="source-media" class:playing={info.isPlaying}>
                            {info.isPlaying ? '▶' : '⏸'} {info.title} — {info.artist || ''}
                          </span>
                        {:else}
                          <span class="source-media idle">{texts.noMediaSource}</span>
                        {/if}
                      </div>
                    </label>
                  {/each}
                </div>
              {/if}
            </div>
          {/if}

        {/if}

      </div>

      <div class="filter-footer">
        <button class="btn btn-save" on:click={handleSave}>{texts.save}</button>
        <button class="btn btn-cancel" on:click={doClose}>{texts.cancel}</button>
      </div>
    </div>
  </div>
{/if}

<style>
  .filter-backdrop {
    position: fixed; inset: 0; z-index: 9000;
    background: var(--c-backdrop);
    display: flex; align-items: center; justify-content: center;
    animation: backdropIn 0.3s ease-out both;
    overflow: visible;
  }
  .filter-backdrop.closing { animation: backdropOut 0.5s ease-in both; }
  @keyframes backdropIn { from { opacity: 0; } to { opacity: 1; } }
  @keyframes backdropOut { from { opacity: 1; } to { opacity: 0; } }

  .filter-stage {
    position: relative; width: 500px; height: 100vh;
    display: flex; flex-direction: column;
    background: white;
    transition: opacity 0.4s ease;
    opacity: 0;
  }
  .filter-stage.fly-in { opacity: 1; }
  .filter-stage.fly-out { opacity: 0; }
  .filter-stage.hidden { opacity: 0; pointer-events: none; }

  .cloud-cv {
    position: absolute;
    image-rendering: pixelated;
    display: block; pointer-events: none; z-index: 0;
  }

  .filter-content {
    position: relative; z-index: 2;
    padding: 1rem 0rem 5rem;
    display: flex; flex-direction: column; gap: 1.2rem;
    font-family: '8bitwonder', monospace;
    flex: 1; min-height: 0;
    overflow-y: auto;
    max-height: none;
    scrollbar-width: none;
  }
  .filter-content::-webkit-scrollbar { display: none; }

  .filter-title {
    font-size: 28px; letter-spacing: 0.1em;
    color: var(--c-text); text-align: center;
  }

  .close-btn {
    position: absolute; top: 0.8rem; right: 0.8rem;
    background: none; border: none; cursor: pointer;
    font-size: 1.4rem; color: color-mix(in srgb, var(--c-text) 50%, transparent);
    font-family: '8bitwonder', monospace; transition: color 0.2s;
  }
  .close-btn:hover { color: var(--c-text); }

  .loading {
    text-align: center;
    color: color-mix(in srgb, var(--c-text) 30%, transparent);
    font-family: '8bitwonder', monospace;
    font-size: 0.6rem;
    padding: 2rem;
  }

  .section {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .section-label {
    font-family: '8bitwonder', monospace;
    font-size: 16px;
    color: var(--c1);
    letter-spacing: 0.08em;
  }

  .mode-options {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .mode-option {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.6rem 1rem;
    background: color-mix(in srgb, var(--c1) 3%, transparent);
    border: 1px solid color-mix(in srgb, var(--c1) 12%, transparent);
    cursor: pointer;
    transition: all 0.2s;
    font-size: 1rem;
    color: color-mix(in srgb, var(--c-text) 60%, transparent);
  }
  .mode-option input { display: none; }
  .mode-option:hover {
    border-color: var(--c1);
    color: var(--c-text);
  }
  .mode-option.active {
    border-color: var(--c1);
    background: color-mix(in srgb, var(--c1) 10%, transparent);
    color: var(--c-text);
  }
  .mode-option.active .radio-dot {
    background: var(--c1);
  }

  .rec-badge {
    font-family: '8bitwonder', monospace;
    font-size: 0.4rem;
    color: var(--c1);
    border: 1px solid color-mix(in srgb, var(--c1) 40%, transparent);
    padding: 0.15rem 0.4rem;
    margin-left: auto;
    letter-spacing: 0.05em;
    flex-shrink: 0;
  }

  .radio-dot {
    width: 10px;
    height: 10px;
    border: 2px solid color-mix(in srgb, var(--c1) 25%, transparent);
    background: transparent;
    transition: all 0.2s;
    flex-shrink: 0;
  }

  .no-sources {
    font-family: 'Rubik', sans-serif;
    font-size: 0.8rem;
    color: color-mix(in srgb, var(--c-text) 35%, transparent);
    line-height: 1.6;
    margin: 0;
    padding: 1rem;
    text-align: center;
    border: 1px dashed color-mix(in srgb, var(--c1) 15%, transparent);
  }

  .sources-list {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;
  }

  .source-item {
    display: flex;
    align-items: flex-start;
    gap: 0.75rem;
    padding: 0.75rem 1rem;
    background: color-mix(in srgb, var(--c1) 3%, transparent);
    border: 1px solid color-mix(in srgb, var(--c1) 10%, transparent);
    cursor: pointer;
    transition: all 0.2s;
  }
  .source-item input { display: none; }
  .source-item .checkbox-box { margin-top: 2px; }
  .source-item:hover {
    border-color: color-mix(in srgb, var(--c1) 70%, transparent);
  }
  .source-item.checked {
    border-color: var(--c1);
    background: color-mix(in srgb, var(--c1) 8%, transparent);
  }
  .source-item.checked .checkbox-box {
    background: var(--c1);
    border-color: var(--c1);
  }

  .checkbox-box {
    width: 14px;
    height: 14px;
    border: 2px solid color-mix(in srgb, var(--c1) 25%, transparent);
    background: transparent;
    transition: all 0.2s;
    flex-shrink: 0;
  }

  .source-info {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;
    overflow: hidden;
    flex: 1;
  }

  .source-name {
    font-family: '8bitwonder', monospace;
    font-size: 0.8rem;
    color: var(--c-text);
  }

  .source-media {
    font-family: 'Rubik', sans-serif;
    font-size: 0.75rem;
    color: color-mix(in srgb, var(--c-text) 35%, transparent);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .source-media.playing {
    color: var(--c1);
  }
  .source-media.idle {
    color: color-mix(in srgb, var(--c-text) 20%, transparent);
    font-style: italic;
  }

  .filter-footer {
    display: flex;
    gap: 1rem;
    flex-shrink: 0;
    padding: 0.5rem 0 1rem;
    position: relative; z-index: 2;
  }

  .btn {
    flex: 1;
    font-family: '8bitwonder', monospace;
    font-size: 0.8rem;
    letter-spacing: 0.06em;
    padding: 0.8rem 1rem;
    border: 1px solid;
    cursor: pointer;
    transition: all 0.2s;
    -webkit-text-stroke: 2px #ffffff;
    paint-order: stroke fill;
  }

  .btn-save {
    background: color-mix(in srgb, var(--c1) 10%, transparent);
    border-color: var(--c1);
    color: var(--c1);
  }
  .btn-save:hover {
    transform: scale(1.03);
    background: color-mix(in srgb, var(--c1) 25%, transparent);
    color: var(--c-text);
  }

  .btn-cancel {
    background: transparent;
    border-color: color-mix(in srgb, var(--c-text) 20%, transparent);
    color: color-mix(in srgb, var(--c-text) 50%, transparent);
  }
  .btn-cancel:hover {
    transform: scale(1.03);
    background: color-mix(in srgb, var(--c1) 8%, transparent);
  }
</style>
