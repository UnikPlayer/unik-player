<script>
  import { onMount, tick } from 'svelte';
  import { goto } from '$app/navigation';
  import { fade, fly, scale } from 'svelte/transition';
  import { getAllPlayers, getAllPlayersAsync, invalidateCustomPlayersCache, getBuiltInPlayerNames, getPlayerMeta } from '$lib/getPlayers.js';
  import PlayerCard from '$lib/components/PlayerCard.svelte';
  import Editor from '$lib/components/Editor.svelte';
  import Notification from '$lib/components/Notification.svelte';
  import CustomPlayerUploader from '$lib/components/CustomPlayerUploader.svelte';
  import MediaFilter from '$lib/components/MediaFilter.svelte';
  import { title, artist, thumbnail, ShowTrack, language } from '$lib/stores/stores.js';
  import { transformCSS, injectCSS, loadCSSFromBackend } from '$lib/utils/playerCSS.js';
  import AccountPanel from '$lib/components/AccountPanel.svelte';
  import GuideOverlay from '$lib/components/GuideOverlay.svelte';
  import { isFirstLaunch, startGuide } from '$lib/stores/guideState.js';

  let players = [];
  let showUploader = false;
  let showFilter = false;
  let sidebarOpen = true;
  let cssReady = false;

  // Translations
  const t = {
    en: {
      widgets: 'WIDGETS',
      docs: 'DOCS',
      search: 'SEARCH',
      filters: 'FILTERS',
      all: 'ALL',
      builtIn: 'BUILT-IN',
      custom: 'CUSTOM',
      addCustom: 'ADD CUSTOM PLAYER',
      filterMedia: 'MEDIA FILTER',
      step1: 'Select widget',
      step1Desc: 'Choose your style',
      step2: 'Click SELECT',
      step2Desc: 'Copy link to clipboard',
      step3: 'Paste in OBS',
      step3Desc: 'Browser Source → URL',
      empty: 'NO PLAYERS',
      danceSync: 'DANCE SYNC',
    },
    ru: {
      widgets: 'ВИДЖЕТЫ',
      docs: 'ДОКИ',
      search: 'ПОИСК',
      filters: 'ФИЛЬТРЫ',
      all: 'ВСЕ',
      builtIn: 'ВСТРОЕННЫЕ',
      custom: 'КАСТОМНЫЕ',
      addCustom: 'ДОБАВИТЬ ПЛЕЕР',
      filterMedia: 'ФИЛЬТР МЕДИА',
      step1: 'Выбери виджет',
      step1Desc: 'Понравившийся стиль',
      step2: 'Нажми SELECT',
      step2Desc: 'Ссылка скопируется',
      step3: 'Вставь в OBS',
      step3Desc: 'Browser Source → URL',
      empty: 'НЕТ ПЛЕЕРОВ',
      danceSync: 'синхронизация гифок',
    }
  };

  $: texts = t[$language] || t.ru;

  // Sort/filter state
  let sortIdx = 0;
  $: SORT_ITEMS = [texts.all, texts.builtIn, texts.custom];
  let searchText = '';
  let debouncedSearch = '';
  let _searchTimer;
  let filteredPlayers = [];

  $: {
    clearTimeout(_searchTimer);
    const _st = searchText;
    _searchTimer = setTimeout(() => { debouncedSearch = _st; }, 150);
  }

  $: {
    let list = players;
    if (sortIdx === 1) list = list.filter(p => !p.isCustom);
    if (sortIdx === 2) list = list.filter(p => p.isCustom);
    if (debouncedSearch) {
      const q = debouncedSearch.toLowerCase();
      list = list.filter(p => p.name.toLowerCase().includes(q));
    }
    filteredPlayers = list;
  }

  async function loadAllPlayerCSS() {
    const playerNames = getBuiltInPlayerNames();

    // Load CSS in parallel
    const results = await Promise.allSettled(playerNames.map(async (name) => {
      try {
        let rawCSS = await loadCSSFromBackend(name);
        if (!rawCSS) {
          const meta = getPlayerMeta(name);
          rawCSS = meta?.defaultCSS || '';
        }
        if (rawCSS) {
          const transformed = transformCSS(rawCSS, name, '.preview-container');
          return transformed;
        }
      } catch (err) {
        console.warn('CSS load failed for ' + name + ':', err);
      }
      return '';
    }));

    const allCSS = results.map(r => r.status === 'fulfilled' ? r.value : '').filter(Boolean).join('\n\n');
    if (allCSS) {
      injectCSS(allCSS, 'unik-preview-css');
    }
  }

  async function loadPlayers() {
    // Load players AND CSS in parallel, grid shows only when CSS is ready
    const [playerList] = await Promise.all([
      getAllPlayersAsync(),
      loadAllPlayerCSS()
    ]);
    players = playerList;
    cssReady = true;
  }

  async function handleCustomPlayerAdded(name) {
    invalidateCustomPlayersCache();
    await loadPlayers();
  }

  function toggleLanguage() {
    language.update(l => l === 'ru' ? 'en' : 'ru');
  }
  function toggleSidebar() { sidebarOpen = !sidebarOpen; }

  // ===== BLOB SYSTEM =====
  const CR=0, CG=0, CB=0;
  const BTN_PS=4, BTN_PAD=40;
  const LW=280, LH=44, LPS=3;
  const LIST_VPAD=5;
  const B1W=480, B1H=96;
  const B2W=480, B2H=72;
  const B3W=480, B3H=72;
  const SR_W=220, SR_H=64, SR_LINE_Y=52, SR_TEXT_Y=40, SR_X0=4, N_LPT=36;

  class WaveBlob {
    constructor(hx, hy, r) {
      this.hx=hx; this.hy=hy; this.r=r;
      this.dx=0; this.dy=0; this.vx=0; this.vy=0;
      this.ph1=Math.random()*Math.PI*2;
      this.ph2=Math.random()*Math.PI*2;
      this.ph3=Math.random()*Math.PI*2;
    }
    update(t, mx, my) {
      const wdx=Math.sin(t*.022+this.ph1)*2.5+Math.sin(t*.009+this.ph3)*1.5;
      const wdy=Math.sin(t*.017+this.ph2)*2+Math.cos(t*.011+this.ph1)*1;
      this.vx+=-(this.dx-wdx)*.08;
      this.vy+=-(this.dy-wdy)*.08;
      const px=this.hx+this.dx, py=this.hy+this.dy;
      const ex=px-mx, ey=py-my, d=Math.sqrt(ex*ex+ey*ey)+.001;
      const zone=this.r*4;
      if(d<zone){const f=Math.pow(1-d/zone,1.5)*3.5; this.vx+=(ex/d)*f; this.vy+=(ey/d)*f;}
      this.vx*=.78; this.vy*=.78;
      this.dx+=this.vx; this.dy+=this.vy;
    }
  }

  class BtnBlob {
    constructor(x, y, r) {
      this.hx=x; this.hy=y; this.r=r;
      this.dx=0; this.dy=0;
      this.vx=(Math.random()-.5)*.45;
      this.vy=(Math.random()-.5)*.3;
      this.phase=Math.random()*Math.PI*2;
    }
    update(t) {
      this.vx+=Math.sin(t*.007+this.phase)*.0225;
      this.vy+=Math.cos(t*.005+this.phase*1.4)*.0225;
      this.vx+=-this.dx*.003; this.vy+=-this.dy*.003;
      this.vx*=.98; this.vy*=.98;
      this.dx+=this.vx; this.dy+=this.vy;
    }
  }

  class LinePt {
    constructor() { this.y=0; this.vy=0; this.ph=Math.random()*Math.PI*2; }
    update(t, i) {
      const rest=Math.sin(t*.013+i*.38+this.ph)*.9+Math.cos(t*.008+i*.22)*.4;
      this.vy+=(rest-this.y)*.09; this.vy*=.87; this.y+=this.vy;
    }
    push(str) { this.vy+=str; }
  }

  const SR_DUR=5;
  class FlyLetter {
    constructor(char, x) {
      this.char=char; this.x=x; this.age=0; this.y=SR_LINE_Y+2; this.done=false;
    }
    update() {
      if(this.done) return;
      this.age++;
      const p=Math.min(this.age/SR_DUR,1);
      const ease=1-(1-p)*(1-p)*(1-p);
      this.y=SR_LINE_Y+2+ease*(SR_TEXT_Y-(SR_LINE_Y+2));
      if(p>=1){this.y=SR_TEXT_Y; this.done=true;}
    }
  }

  function makeBtnBlobs(BW, BH) {
    const cx=BW/2, cy=BH/2, b=[];
    const clusters=[
      {x:0, y:0, rx:BW*.22, ry:BH*.32, n:12},
      {x:-BW*.18, y:-BH*.08, rx:BW*.14, ry:BH*.26, n:8},
      {x:BW*.18, y:-BH*.06, rx:BW*.14, ry:BH*.26, n:8},
      {x:-BW*.32, y:BH*.02, rx:BW*.10, ry:BH*.22, n:6},
      {x:BW*.32, y:BH*.02, rx:BW*.10, ry:BH*.22, n:6},
      {x:-BW*.10, y:-BH*.22, rx:BW*.08, ry:BH*.16, n:5},
      {x:BW*.08, y:-BH*.20, rx:BW*.09, ry:BH*.16, n:5},
      {x:0, y:BH*.18, rx:BW*.16, ry:BH*.14, n:5},
      {x:-BW*.22, y:BH*.14, rx:BW*.08, ry:BH*.14, n:4},
      {x:BW*.22, y:BH*.14, rx:BW*.08, ry:BH*.14, n:4},
    ];
    for(const cl of clusters){
      for(let i=0;i<cl.n;i++){
        const a=Math.random()*Math.PI*2, d=Math.pow(Math.random(),.6);
        const x=cx+cl.x+Math.cos(a)*cl.rx*d;
        const y=cy+cl.y+Math.sin(a)*cl.ry*d;
        const r=5+Math.random()*7;
        b.push(new BtnBlob(x,y,r));
      }
    }
    return b;
  }

  const blobs1=makeBtnBlobs(B1W,B1H);
  const blobs2=makeBtnBlobs(B2W,B2H);
  const blobs3=makeBtnBlobs(B3W,B3H);

  const LROWS=3;
  const LPW=Math.ceil(LW/LPS);
  const LPH_ROW=Math.ceil(LH/LPS);
  const LPH=LPH_ROW*LROWS;
  const LPH_FULL=LPH+LIST_VPAD*2;
  const LH_FULL=LPH_FULL*LPS;

  const listBlobNorm=[];

  function makeListBlobs() {
    const b=[];
    const cy=LPH_ROW/2;
    const clusters=[
      {x:0.5,y:0.0,rx:0.22,ry:0.32,n:10},
      {x:0.2,y:-0.02,rx:0.14,ry:0.28,n:7},
      {x:0.8,y:-0.02,rx:0.14,ry:0.28,n:7},
      {x:0.35,y:-0.22,rx:0.10,ry:0.18,n:5},
      {x:0.65,y:-0.22,rx:0.10,ry:0.18,n:5},
      {x:0.08,y:0.02,rx:0.07,ry:0.22,n:4},
      {x:0.92,y:0.02,rx:0.07,ry:0.22,n:4},
      {x:0.5,y:0.22,rx:0.14,ry:0.14,n:4},
    ];
    for(const cl of clusters){
      for(let i=0;i<cl.n;i++){
        const a=Math.random()*Math.PI*2,d=Math.pow(Math.random(),.55);
        const nx=Math.max(-.05,Math.min(1.05,cl.x+Math.cos(a)*cl.rx*d));
        const nyOff=cl.y+Math.sin(a)*cl.ry*d;
        listBlobNorm.push({nx});
        const y=cy+nyOff*LPH_ROW*0.8;
        b.push(new WaveBlob(0,y,0.9+Math.random()*0.8));
      }
    }
    return b;
  }
  const listBlobs=makeListBlobs();

  let listTextWidths=[];
  const PAD_PX=0.8*16;
  function applyTextLayout(idx, animate) {
    if(!listTextWidths.length) return;
    const tw=listTextWidths[idx]-2*PAD_PX;
    const L=(LW-PAD_PX-tw)/LPS;
    const R=(LW-PAD_PX)/LPS;
    for(let i=0;i<listBlobs.length;i++){
      const b=listBlobs[i];
      const {nx}=listBlobNorm[i];
      const targetHx=L+nx*(R-L);
      if(animate){b.dx-=(targetHx-b.hx);}
      b.hx=targetHx;
    }
  }

  const srLinePts=Array.from({length:N_LPT},()=>new LinePt());
  let srLetters=[];
  let srText='';

  const SR_WORDS=['SIGMA','CHAD','GOAT','BASED','RIZZ','GIGA','BUSSIN','CERTIFIED',
    'GIGACHAD','MEWING','RATIO','SHEESH','DRIP','GLAZING','POGGERS',
    'SUSSY','CLUTCH','LOWKEY','CRINGE','SWAG','YEET','NPC','BEAST'];
  const srPlaceholder=SR_WORDS[Math.floor(Math.random()*SR_WORDS.length)]+' PLAYER';

  let srCanvas=null;
  let listBlobCanvas=null;
  let listContainer=null;
  let btn1ring=null, btn1fill=null;
  let btn2ring=null, btn2fill=null;
  let h1=false, h2=false, h3=false;
  let p1=0, p2=0, p3=0;
  let btn3ring=null, btn3fill=null;
  let listMx=-999, listMy=-999;

  function selectSort(newIdx) {
    if(newIdx===sortIdx) return;
    const delta=newIdx-sortIdx;
    const shiftY=delta*LPH_ROW;
    for(const b of listBlobs){
      b.hy+=shiftY; b.dy-=shiftY;
      b.vx+=(Math.random()-.5)*1.5;
      b.vy+=delta*0.8+(Math.random()-.5)*0.8;
    }
    applyTextLayout(newIdx, true);
    sortIdx=newIdx;
  }

  function lmm(e) {
    if(!listContainer) return;
    const r=listContainer.getBoundingClientRect();
    const relX=(e.clientX-r.left)/LW*LPW;
    const relY=(e.clientY-r.top)/LH*LPH_ROW;
    listMx=relX; listMy=relY;
  }
  function lml() { listMx=-999; listMy=-999; }

  function srMeasureX(sc, str, i) { return SR_X0+sc.measureText(str.slice(0,i)).width; }

  function srOnInput(e) {
    const raw=e.target.value;
    const newVal=raw.toUpperCase();
    if(raw!==newVal){
      const pos=e.target.selectionStart;
      e.target.value=newVal;
      e.target.setSelectionRange(pos,pos);
    }
    searchText=newVal;
    if(!srCanvas){srText=newVal; return;}
    const sc=srCanvas.getContext('2d');
    sc.font="16px '8bitwonder',monospace";
    let p=0;
    while(p<srText.length&&p<newVal.length&&srText[p]===newVal[p]) p++;
    let os=srText.length, ns=newVal.length;
    while(os>p&&ns>p&&srText[os-1]===newVal[ns-1]){os--; ns--;}
    const prefix=srLetters.slice(0,p);
    const suffix=srLetters.slice(os);
    const inserted=[];
    for(let i=p;i<ns;i++){
      const x=srMeasureX(sc,newVal,i);
      srLinePts.forEach((pt,pi)=>{
        const px=pi/(N_LPT-1)*SR_W;
        const dist=Math.abs(px-x);
        const zone=SR_W*.2;
        if(dist<zone) pt.push(2.8*Math.pow(1-dist/zone,2));
      });
      inserted.push(new FlyLetter(newVal[i],x));
    }
    srLetters=[...prefix,...inserted,...suffix];
    for(let i=0;i<srLetters.length;i++) srLetters[i].x=srMeasureX(sc,newVal,i);
    srText=newVal;
  }

  onMount(()=>{
    const cs=getComputedStyle(document.documentElement);
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
    const cText=parseCSSColor('--c-text');
    const cLight=parseCSSColor('--c2');
    const cDark=parseCSSColor('--c1');

    requestAnimationFrame(()=>{
      if(listContainer){
        const spans=listContainer.querySelectorAll('.sort-label');
        listTextWidths=Array.from(spans).map(s=>s.offsetWidth);
        applyTextLayout(0,false);
      }
    });

    loadPlayers();

    // Автозапуск гайда при первом посещении
    if ($isFirstLaunch) {
      setTimeout(() => {
        startGuide(); // Пустой массив, пока пользователь не даст шаги
      }, 1000);
    }

    const handleCSSRefresh = () => loadAllPlayerCSS();
    const handlePlayerDeleted = () => loadPlayers();
    window.addEventListener('unik-css-refresh', handleCSSRefresh);
    window.addEventListener('unik-player-deleted', handlePlayerDeleted);

    const lOff1=document.createElement('canvas'); lOff1.width=LPW; lOff1.height=LPH_FULL;
    const lc1=lOff1.getContext('2d');
    const lOff2=document.createElement('canvas'); lOff2.width=LPW; lOff2.height=LPH_FULL;
    const lc2=lOff2.getContext('2d');
    const lOff3=document.createElement('canvas'); lOff3.width=LPW; lOff3.height=LPH_FULL;
    const lc3=lOff3.getContext('2d');

    function renderListBlob(){
      const dc=listBlobCanvas;
      if(!dc) return;
      if(dc.width!==LPW||dc.height!==LPH_FULL){dc.width=LPW; dc.height=LPH_FULL;}
      // Normal pass
      lc1.clearRect(0,0,LPW,LPH_FULL);
      for(const bl of listBlobs){
        const bx=bl.hx+bl.dx, by=bl.hy+bl.dy+LIST_VPAD;
        const g=lc1.createRadialGradient(bx,by,0,bx,by,bl.r);
        g.addColorStop(0,'rgba(255,255,255,1)');
        g.addColorStop(.5,'rgba(255,255,255,.85)');
        g.addColorStop(1,'rgba(255,255,255,0)');
        lc1.fillStyle=g; lc1.beginPath(); lc1.arc(bx,by,bl.r,0,Math.PI*2); lc1.fill();
      }
      lc2.clearRect(0,0,LPW,LPH_FULL);
      lc2.filter='blur(2.5px)'; lc2.drawImage(lOff1,0,0); lc2.filter='none';
      const imgN=lc2.getImageData(0,0,LPW,LPH_FULL);
      // Grown pass (slightly larger blobs for outline)
      lc1.clearRect(0,0,LPW,LPH_FULL);
      for(const bl of listBlobs){
        const bx=bl.hx+bl.dx, by=bl.hy+bl.dy+LIST_VPAD, br=bl.r+1;
        const g=lc1.createRadialGradient(bx,by,0,bx,by,br);
        g.addColorStop(0,'rgba(255,255,255,1)');
        g.addColorStop(.5,'rgba(255,255,255,.85)');
        g.addColorStop(1,'rgba(255,255,255,0)');
        lc1.fillStyle=g; lc1.beginPath(); lc1.arc(bx,by,br,0,Math.PI*2); lc1.fill();
      }
      lc3.clearRect(0,0,LPW,LPH_FULL);
      lc3.filter='blur(2.5px)'; lc3.drawImage(lOff1,0,0); lc3.filter='none';
      const imgG=lc3.getImageData(0,0,LPW,LPH_FULL);
      const dN=imgN.data, dG=imgG.data;
      const out=lc2.createImageData(LPW,LPH_FULL);
      const od=out.data;
      for(let i=0;i<dN.length;i+=4){
        const vN=dN[i], vG=dG[i];
        const inNorm=vN>55, inGrown=vG>55;
        if(inGrown&&!inNorm){
          od[i]=cDark[0]; od[i+1]=cDark[1]; od[i+2]=cDark[2]; od[i+3]=255;
        } else if(inNorm){
          od[i]=CR; od[i+1]=CG; od[i+2]=CB; od[i+3]=255;
        }
      }
      lc2.putImageData(out,0,0);
      const dctx=dc.getContext('2d');
      dctx.imageSmoothingEnabled=false;
      dctx.clearRect(0,0,dc.width,dc.height);
      dctx.drawImage(lOff2,0,0,dc.width,dc.height);
    }

    function makeBOff(BW,BH){
      const PW=Math.ceil((BW+BTN_PAD*2)/BTN_PS), PH=Math.ceil((BH+BTN_PAD*2)/BTN_PS);
      const a=document.createElement('canvas'); a.width=PW; a.height=PH;
      const b=document.createElement('canvas'); b.width=PW; b.height=PH;
      const c=document.createElement('canvas'); c.width=PW; c.height=PH;
      return {a,ca:a.getContext('2d'),b,cb:b.getContext('2d'),c,cc:c.getContext('2d'),PW,PH};
    }
    const bo1=makeBOff(B1W,B1H);
    const bo2=makeBOff(B2W,B2H);
    const bo3=makeBOff(B3W,B3H);

    function renderBtnBoth(dcRing,dcFill,blobs,off,lt,pt,reversed){
      const {PW,PH,ca,cb,cc,a}=off;
      const scale=1/BTN_PS;
      if(dcRing.width!==PW||dcRing.height!==PH){dcRing.width=PW; dcRing.height=PH;}
      if(dcFill.width!==PW||dcFill.height!==PH){dcFill.width=PW; dcFill.height=PH;}
      ca.clearRect(0,0,PW,PH);
      for(const bl of blobs){
        const padOff=BTN_PAD*scale;
        const bx=(bl.hx+bl.dx)*scale+padOff, by=(bl.hy+bl.dy)*scale+padOff, br=bl.r*scale;
        const g=ca.createRadialGradient(bx,by,0,bx,by,br);
        g.addColorStop(0,'rgba(255,255,255,1)');
        g.addColorStop(.5,'rgba(255,255,255,.85)');
        g.addColorStop(1,'rgba(255,255,255,0)');
        ca.fillStyle=g; ca.beginPath(); ca.arc(bx,by,br,0,Math.PI*2); ca.fill();
      }
      cb.clearRect(0,0,PW,PH); cb.filter='blur(2px)'; cb.drawImage(a,0,0); cb.filter='none';
      const rawN=cb.getImageData(0,0,PW,PH).data;
      ca.clearRect(0,0,PW,PH);
      for(const bl of blobs){
        const padOff2=BTN_PAD*scale;
        const bx=(bl.hx+bl.dx)*scale+padOff2, by=(bl.hy+bl.dy)*scale+padOff2, br=bl.r*scale+2;
        const g=ca.createRadialGradient(bx,by,0,bx,by,br);
        g.addColorStop(0,'rgba(255,255,255,1)');
        g.addColorStop(.5,'rgba(255,255,255,.85)');
        g.addColorStop(1,'rgba(255,255,255,0)');
        ca.fillStyle=g; ca.beginPath(); ca.arc(bx,by,br,0,Math.PI*2); ca.fill();
      }
      cc.clearRect(0,0,PW,PH); cc.filter='blur(2px)'; cc.drawImage(a,0,0); cc.filter='none';
      const rawG=cc.getImageData(0,0,PW,PH).data;
      const dRing=new ImageData(PW,PH);
      const dFill=new ImageData(PW,PH);
      for(let i=0;i<rawN.length;i+=4){
        const vN=rawN[i], vG=rawG[i];
        const inNorm=vN>50, inGrown=vG>50;
        if(inGrown&&!inNorm){
          dRing.data[i]=cDark[0]; dRing.data[i+1]=cDark[1]; dRing.data[i+2]=cDark[2]; dRing.data[i+3]=255;
        }
        if(inNorm){
          let fr,fg,fb;
          if(reversed){
            fr=Math.round(255*(1-lt)); fg=Math.round(255*(1-lt)); fb=Math.round(255*(1-lt));
          } else {
            fr=Math.round(255*lt); fg=Math.round(255*lt); fb=Math.round(255*lt);
          }
          dFill.data[i]=Math.round(fr+(255-fr)*pt);
          dFill.data[i+1]=Math.round(fg+(255-fg)*pt);
          dFill.data[i+2]=Math.round(fb+(255-fb)*pt);
          dFill.data[i+3]=255;
        }
      }
      const ctxR=dcRing.getContext('2d'); ctxR.imageSmoothingEnabled=false; ctxR.clearRect(0,0,PW,PH); ctxR.putImageData(dRing,0,0);
      const ctxF=dcFill.getContext('2d'); ctxF.imageSmoothingEnabled=false; ctxF.clearRect(0,0,PW,PH); ctxF.putImageData(dFill,0,0);
    }

    let raf, last=0, tt=0, lt1=0, lt2=0, lt3=0, pt1=0, pt2=0, pt3=0;

    function render(ts){
      raf=requestAnimationFrame(render);
      if(ts-last<16) return; last=ts; tt++;
      lt1=Math.max(0,Math.min(1,lt1+(h1?0.15:-0.15)));
      lt2=Math.max(0,Math.min(1,lt2+(h2?0.15:-0.15)));
      lt3=Math.max(0,Math.min(1,lt3+(h3?0.15:-0.15)));
      if(p1>0){pt1=p1; p1=0;}
      if(p2>0){pt2=p2; p2=0;}
      pt1=Math.max(0,pt1-0.1);
      pt2=Math.max(0,pt2-0.1);
      for(const b of blobs1) b.update(tt);
      for(const b of blobs2) b.update(tt);
      for(const b of blobs3) b.update(tt);
      if(btn1ring&&btn1fill) renderBtnBoth(btn1ring,btn1fill,blobs1,bo1,lt1,pt1,false);
      if(btn2ring&&btn2fill) renderBtnBoth(btn2ring,btn2fill,blobs2,bo2,lt2,pt2,true);
      if(btn3ring&&btn3fill) renderBtnBoth(btn3ring,btn3fill,blobs3,bo3,lt3,pt3,false);
      for(const b of listBlobs) b.update(tt,listMx,listMy);
      renderListBlob();
      if(srCanvas){
        const sc=srCanvas.getContext('2d');
        if(srCanvas.width!==SR_W||srCanvas.height!==SR_H){srCanvas.width=SR_W; srCanvas.height=SR_H;}
        sc.clearRect(0,0,SR_W,SR_H);
        sc.font="16px '8bitwonder',monospace";
        sc.textBaseline='alphabetic';
        sc.fillStyle='rgb('+cDark[0]+','+cDark[1]+','+cDark[2]+')';
        for(const fl of srLetters) fl.update();
        let settled=0;
        while(settled<srLetters.length&&srLetters[settled].done) settled++;
        if(settled>0) sc.fillText(srText.slice(0,settled),SR_X0,SR_TEXT_Y);
        for(let i=settled;i<srLetters.length;i++){
          const fl=srLetters[i];
          sc.fillText(fl.char,fl.x,fl.done?SR_TEXT_Y:fl.y);
        }
        if(srLetters.length===0){
          sc.fillStyle='rgba('+cDark[0]+','+cDark[1]+','+cDark[2]+',0.25)';
          sc.fillText(srPlaceholder,SR_X0,SR_TEXT_Y);
        }
        for(let i=0;i<N_LPT;i++) srLinePts[i].update(tt,i);
        const BLK=4, LT=3;
        for(let bx=0;bx<SR_W;bx+=BLK){
          const frac=bx/(SR_W-1);
          const idx=frac*(N_LPT-1);
          const i0=Math.floor(idx), i1=Math.min(i0+1,N_LPT-1);
          const lerp=idx-i0;
          const rawY=SR_LINE_Y+srLinePts[i0].y*(1-lerp)+srLinePts[i1].y*lerp;
          const py=Math.round(rawY);
          sc.fillStyle='rgb('+cLight[0]+','+cLight[1]+','+cLight[2]+')';
          sc.fillRect(bx,py+LT,BLK,SR_H-(py+LT));
          sc.fillStyle='rgb('+cDark[0]+','+cDark[1]+','+cDark[2]+')';
          sc.fillRect(bx,py,BLK,LT);
        }
      }
    }

    raf=requestAnimationFrame(render);

    return ()=>{
      cancelAnimationFrame(raf);
      window.removeEventListener('unik-css-refresh', handleCSSRefresh);
      window.removeEventListener('unik-player-deleted', handlePlayerDeleted);
    };
  });
</script>

<Notification />
<Editor />

<div class="root">
  <aside class="sidebar" class:collapsed={!sidebarOpen}>
    <div class="logo-container">
      <div class="logo">[<span class="logo-accent">UNIK</span>PLAYER]</div>
      <button class="guide-btn" on:click={() => startGuide()} aria-label="Как пользоваться">
        КАК ПОЛЬЗОВАТЬСЯ
      </button>
    </div>

    <div class="two-col">
      <div class="col-left">
        <div class="col-label">{texts.search}</div>
        <div class="sr-wrap">
          <canvas bind:this={srCanvas} width={SR_W} height={SR_H}
            style="width:{SR_W}px;height:{SR_H}px;display:block;"></canvas>
          <input class="sr-input" type="text" on:input={srOnInput}
            autocomplete="off" spellcheck="false"/>
        </div>
      </div>
      <div class="col-right">
        <div class="col-label">{texts.filters}</div>
        <div class="sort-list" bind:this={listContainer} on:mousemove={lmm} on:mouseleave={lml} role="list">
          <canvas bind:this={listBlobCanvas} class="list-blob-cv"
            width={LPW} height={LPH_FULL}
            style="width:{LW}px;height:{LH_FULL}px;top:{-LIST_VPAD*LPS}px;"></canvas>
          {#each SORT_ITEMS as item, i}
            <button class="sort-item" on:click={()=>selectSort(i)}>
              <span class="sort-label" style="color:{sortIdx===i?'var(--c2)':'var(--c1)'}">{item}</span>
            </button>
          {/each}
        </div>
      </div>
    </div>

    <div class="sidebar-spacer"></div>

    <div class="btn-group">
      <div id="dance-sync-btn" class="blob-btn" style="width:{B3W}px;height:{B3H}px;"
        on:mouseenter={()=>h3=true} on:mouseleave={()=>h3=false}
        on:click={() => goto('/dancesync')} role="button" tabindex="0"
        on:keydown={e=>e.key==='Enter'&& goto('/dancesync')}>
        <canvas bind:this={btn3ring} style="position:absolute;top:{-BTN_PAD}px;left:{-BTN_PAD}px;width:{B3W+BTN_PAD*2}px;height:{B3H+BTN_PAD*2}px;image-rendering:pixelated;display:block;pointer-events:none;"></canvas>
        <canvas bind:this={btn3fill} style="position:absolute;top:{-BTN_PAD}px;left:{-BTN_PAD}px;width:{B3W+BTN_PAD*2}px;height:{B3H+BTN_PAD*2}px;image-rendering:pixelated;display:block;pointer-events:none;"></canvas>
        <span class="btn-label" style="color:{h3?'var(--c1)':'var(--c2)'}">{texts.danceSync}</span>
      </div>
      <div id="custom-btn" class="blob-btn" style="width:{B1W}px;height:{B1H}px;"
        on:mouseenter={()=>h1=true} on:mouseleave={()=>h1=false}
        on:click={() => showUploader = true} role="button" tabindex="0"
        on:keydown={e=>e.key==='Enter'&& (showUploader = true)}>
        <canvas bind:this={btn1ring} style="position:absolute;top:{-BTN_PAD}px;left:{-BTN_PAD}px;width:{B1W+BTN_PAD*2}px;height:{B1H+BTN_PAD*2}px;image-rendering:pixelated;display:block;pointer-events:none;"></canvas>
        <canvas bind:this={btn1fill} style="position:absolute;top:{-BTN_PAD}px;left:{-BTN_PAD}px;width:{B1W+BTN_PAD*2}px;height:{B1H+BTN_PAD*2}px;image-rendering:pixelated;display:block;pointer-events:none;"></canvas>
        <span class="btn-label" style="color:{h1?'var(--c1)':'var(--c2)'}">{texts.addCustom}</span>
      </div>

      <div id="btn-filter" class="blob-btn" style="width:{B2W}px;height:{B2H}px;"
        on:mouseenter={()=>h2=true} on:mouseleave={()=>h2=false}
        on:click={() => showFilter = true} role="button" tabindex="0"
        on:keydown={e=>e.key==='Enter'&& (showFilter = true)}>
        <canvas bind:this={btn2ring} style="position:absolute;top:{-BTN_PAD}px;left:{-BTN_PAD}px;width:{B2W+BTN_PAD*2}px;height:{B2H+BTN_PAD*2}px;image-rendering:pixelated;display:block;pointer-events:none;"></canvas>
        <canvas bind:this={btn2fill} style="position:absolute;top:{-BTN_PAD}px;left:{-BTN_PAD}px;width:{B2W+BTN_PAD*2}px;height:{B2H+BTN_PAD*2}px;image-rendering:pixelated;display:block;pointer-events:none;"></canvas>
        <span class="btn-label" style="color:{h2?'var(--c2)':'var(--c1)'}">{texts.filterMedia}</span>
      </div>
    </div>

    <AccountPanel />

    <div class="bottom-bar">
      <button class="bottom-icon" on:click={toggleSidebar}>{sidebarOpen ? "\u2039" : "\u203A"}</button>
      <div class="bottom-right">
        <a href="/wiki" class="bottom-link">{texts.docs}</a>
        <button class="lang-btn" on:click={toggleLanguage}>{$language === 'ru' ? 'EN' : 'RU'}</button>
      </div>
    </div>
  </aside>

  <main class="main">
  {#if cssReady}
    <div class="players-grid" in:fade={{ delay: 0, speed:300}}>
      {#if filteredPlayers.length === 0 && players.length > 0}
        <div class="empty">
        <div class="empty-icon"></div>
          <p>{texts.empty}</p>
        </div>
      {:else}
        {#each filteredPlayers as player}
          <PlayerCard 
            component={player.component}
            name={player.name}
            isCustom={player.isCustom || false}
            isExample={player.isExample || false}
            error={player.error || null}
          />
        {/each}
      {/if}
    </div>

    <footer class="page-footer">
      <a href="https://github.com/UNIKNOW0/unik-player" target="_blank" rel="noopener" class="footer-link">GITHUB</a>
      <a href="https://www.donationalerts.com/r/unikn0w" target="_blank" rel="noopener" class="footer-btn-donate">DONATE</a>
      <span class="footer-text">v0.7</span>
    </footer>
  {/if}
  </main>

</div>

<GuideOverlay />

<CustomPlayerUploader
  visible={showUploader}
  onClose={() => showUploader = false}
  onSuccess={handleCustomPlayerAdded}
/>

<MediaFilter
  visible={showFilter}
  onClose={() => showFilter = false}
/>

<style>
  :global(header) { display: none !important; }

  .root {
    display: flex; height: 100vh; width: 100vw; overflow: visible;
    position: fixed; top: 0; left: 0;
    font-family: '8bitwonder', monospace;
  }
  .sidebar {
    width: 560px; flex-shrink: 0;
    background: var(--c2); color: var(--c1);
    display: flex; flex-direction: column;
    padding: 1.5rem 1.5rem 1rem;
    overflow: visible;
    transition: transform 0.4s cubic-bezier(.4,0,.2,1);
  }
  .sidebar.collapsed { transform: translateX(calc(-100% + 3rem)); }
  .sidebar.collapsed .two-col,
  .sidebar.collapsed .btn-group,
  .sidebar.collapsed .logo,
  .sidebar.collapsed .lang-btn,
  .sidebar.collapsed .bottom-link { opacity: 0; pointer-events: none; transition: opacity 0.2s; }

  .logo-container {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 2rem;
  }
  .logo { font-size: 2rem; letter-spacing: 0.05em; text-align: left; }
  .logo-accent { color: var(--ca); }
  
  .guide-btn {
    background: none;
    border: 2px solid color-mix(in srgb,var(--c1) 30%,transparent);
    color: color-mix(in srgb,var(--c1) 60%,transparent);
    font-family: '8bitwonder', monospace;
    font-size: 0.6rem;
    padding: 0.4rem 0.8rem;
    cursor: pointer;
    letter-spacing: 0.05em;
    transition: all 0.2s;
    white-space: nowrap;
  }
  .guide-btn:hover {
    color: var(--c1);
    border-color: var(--c1);
    background: rgba(255,255,255,0.05);
  }

  .two-col { display: flex; gap: 12px; align-items: flex-start; }
  .col-left { width: 220px; flex-shrink: 0; }
  .col-right { width: 280px; flex-shrink: 0; }
  .col-label {
    font-family: '8bitwonder', monospace;
    font-size: 0.7rem; letter-spacing: 0.08em;
    color: color-mix(in srgb,var(--c1) 40%,transparent);
    margin-bottom: 0.5rem;
  }

  .sr-wrap { position: relative; width: 220px; height: 64px; }
  .sr-input {
    position: absolute; top: 0; left: 4px;
    width: calc(100% - 8px); height: 100%;
    background: transparent; border: none; outline: none;
    color: transparent; caret-color: var(--c1);
    font-family: '8bitwonder', monospace; font-size: 1rem;
    letter-spacing: 0; padding: 0; cursor: text;
  }

  .sort-list {
    position: relative; display: flex; flex-direction: column;
    width: 280px; overflow: visible; padding: 15px 0;
  }
  .list-blob-cv {
    position: absolute; left: 0;
    image-rendering: pixelated; pointer-events: none; display: block;
  }
  .sort-item {
    background: none; border: none; cursor: pointer;
    width: 280px; height: 44px;
    padding: 0; display: flex; align-items: center;
    justify-content: flex-end; position: relative; z-index: 1;
  }
  .sort-label {
    font-family: '8bitwonder', monospace; font-size: 1rem;
    letter-spacing: 0.08em; padding: 0 0.8rem;
    white-space: nowrap; transition: color 0.2s;
  }

  .sidebar-spacer { flex: 1; }

  .btn-group {
    display: flex; flex-direction: column;
    gap: 2.5rem; margin-bottom: 1.5rem;
    overflow: visible; padding: 12px 0;
  }
  .blob-btn {
    position: relative; cursor: pointer; user-select: none; overflow: visible;
  }
  .btn-label {
    position: absolute; inset: 0;
    display: flex; align-items: center; justify-content: center;
    font-family: '8bitwonder', monospace; font-size: 1rem;
    letter-spacing: 0.06em; pointer-events: none;
    white-space: nowrap; transition: color 0.2s;
  }

  .bottom-bar { display: flex; justify-content: space-between; align-items: center; }
  .bottom-right { display: flex; align-items: center; gap: 1rem; }
  .bottom-icon, .lang-btn {
    background: none; border: none; color: color-mix(in srgb,var(--c1) 50%,transparent);
    font-family: '8bitwonder', monospace; font-size: 1rem;
    cursor: pointer; letter-spacing: 0.06em; transition: color 0.2s;
  }
  .lang-btn:hover { color: var(--c1); }
  .bottom-link {
    font-family: '8bitwonder', monospace; font-size: 0.7rem;
    color: color-mix(in srgb,var(--c1) 40%,transparent);
    text-decoration: none; letter-spacing: 0.06em;
    transition: color 0.2s;
  }
  .bottom-link:hover { color: var(--c1); }

  .main { flex: 1; background: var(--c1); overflow-y: auto; padding: 0; display: flex; flex-direction: column; }
  .players-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0; flex: 1; }

  .empty {
    grid-column: 1 / -1; display: flex; flex-direction: column;
    align-items: center; gap: 1rem; padding: 4rem 0;
  }
  .empty-icon { font-size: 4rem; color: color-mix(in srgb,var(--c2) 20%,transparent); }
  .empty :global(p) { color: color-mix(in srgb,var(--c2) 40%,transparent); font-size: 1.5rem; margin: 0; }

  .page-footer {
    padding: 1.5rem 0 0.5rem;
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 1.5rem;
    flex-shrink: 0;
  }

  .footer-link {
    font-family: '8bitwonder', monospace;
    font-size: 0.65rem;
    color: var(--c2);
    text-decoration: none;
    letter-spacing: 0.05em;
    transition: opacity 0.2s;
    opacity: 0.6;
  }
  .footer-link:hover { opacity: 1; }

  .footer-btn-donate {
    font-family: '8bitwonder', monospace;
    font-size: 0.65rem;
    color: var(--c2);
    text-decoration: none;
    letter-spacing: 0.05em;
    padding: 0.4rem 1rem;
    border: 1px solid rgba(255, 255, 255, 0.3);
    background: rgba(255, 255, 255, 0.05);
    transition: all 0.2s;
  }
  .footer-btn-donate:hover {
    background: rgba(255, 255, 255, 0.1);
    border-color: var(--c2);
  }

  .footer-text {
    font-family: '8bitwonder', monospace;
    font-size: 0.55rem;
    color: color-mix(in srgb,var(--c2) 25%,transparent);
    letter-spacing: 0.1em;
  }
</style>