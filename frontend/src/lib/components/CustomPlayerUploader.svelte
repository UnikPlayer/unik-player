<script>
    import { onMount } from "svelte";
    import ValidationErrorDialog from "./ValidationErrorDialog.svelte";
    import {
        ShowNotification,
        notificationText,
        editorOpen,
        editingPlayer,
        editingPlayerIsCustom,
    } from "$lib/stores/stores";
    import {
        getAllPlayersAsync,
        invalidateCustomPlayersCache,
    } from "$lib/getPlayers.js";

    function getApiBase() {
        if (typeof window === "undefined") return "http://127.0.0.1:27272";
        const port = window.location.port;
        if (port === "7270" || port === "5173") return "";
        return "";
    }

    export let visible = false;
    export let onClose = () => {};
    export let onSuccess = (name) => {};

    let dragOver = false;
    let uploading = false;
    let showErrors = false;
    let validationErrors = [];
    let htmlContent = "";
    let anim = 'hidden';
    let closing = false;

    /** @type {HTMLInputElement} */
    let fileInput;
    let cloudCanvas = null;
    let contentEl = null;

    // Cloud blob system
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
            {x:0,y:0.05,r:0.40,n:35},
            {x:-0.05,y:-0.22,r:0.24,n:22},{x:0.18,y:-0.18,r:0.20,n:18},
            {x:-0.22,y:-0.14,r:0.18,n:16},{x:0.08,y:-0.32,r:0.14,n:12},
            {x:-0.12,y:-0.35,r:0.11,n:10},{x:0.26,y:-0.28,r:0.10,n:8},
            {x:-0.38,y:-0.02,r:0.16,n:14},{x:0.38,y:-0.02,r:0.16,n:14},
            {x:-0.32,y:-0.12,r:0.12,n:10},{x:0.34,y:-0.12,r:0.12,n:10},
            {x:-0.18,y:0.22,r:0.18,n:12},{x:0.15,y:0.22,r:0.18,n:12},
            {x:0,y:0.26,r:0.14,n:8},
            {x:-0.42,y:-0.02,r:0.09,n:8},{x:0.42,y:-0.02,r:0.09,n:8},
            {x:-0.42,y:0.12,r:0.09,n:8},{x:0.42,y:0.12,r:0.09,n:8},
            {x:-0.42,y:0.25,r:0.08,n:6},{x:0.42,y:0.25,r:0.08,n:6},
        ];
        const SCALE=1.3;
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

    // "start from scratch"
    const BASE_HTML = `<style>
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
</div>`

    
    let nameInput = '';
    let showNameInput = false;
    let existingNames = new Set();
    let nameTaken = false;

    async function loadExistingNames() {
        invalidateCustomPlayersCache();
        const players = await getAllPlayersAsync();
        existingNames = new Set(players.map((p) => p.name.toLowerCase()));
    }

    $: if (nameInput) {
        const trimmed = nameInput.trim().replace(/[^a-zA-Z0-9_-]/g, "_").toLowerCase();
        nameTaken = !!trimmed && existingNames.has(trimmed);
    } else {
        nameTaken = false;
    }

    function openNameInput() {
        nameInput = '';
        showNameInput = true;
        loadExistingNames();
    }

    function cancelNameInput() {
        showNameInput = false;
        nameInput = '';
    }

    async function confirmNameInput() {
        const trimmed = nameInput.trim().replace(/[^a-zA-Z0-9_-]/g, '_');
        if (!trimmed || nameTaken) return;
        showNameInput = false;
        await uploadAndOpen(trimmed, BASE_HTML, true);
    }

    function doClose() {
        if(closing) return;
        closing = true;
        anim = 'fly-out';
        setTimeout(()=>{
            onClose();
            showErrors = false;
            validationErrors = [];
            htmlContent = '';
            closing = false;
            anim = 'hidden';
            blobs = null;
            showNameInput = false;
            nameInput = '';
        }, 500);
    }

    function handleDragOver(e) {
        e.preventDefault();
        dragOver = true;
    }
    function handleDragLeave(e) {
        e.preventDefault();
        dragOver = false;
    }

    function handleDrop(e) {
        e.preventDefault();
        dragOver = false;
        const file = e.dataTransfer?.files?.[0];
        if (file) processFile(file);
    }

    function handleFileSelect(e) {
        const file = e.target?.files?.[0];
        if (file) processFile(file);
    }

    async function processFile(file) {
        if (!file.name.endsWith(".html")) {
            notificationText.set("Only .html files allowed");
            ShowNotification.set(true);
            return;
        }
        const base = file.name.replace(".html", "").trim().replace(/[^a-zA-Z0-9_-]/g, '_');
        if (!base) return;
        await loadExistingNames();
        const name = getAvailableName(base);
        const text = await file.text();
        await uploadAndOpen(name, text, false);
    }

    function getAvailableName(base) {
        if (!existingNames.has(base.toLowerCase())) return base;
        let i = 1;
        while (existingNames.has(`${base}${i}`.toLowerCase())) i++;
        return `${base}${i}`;
    }

    async function uploadAndOpen(name, html, openEditor) {
        uploading = true;
        try {
            const res = await fetch(`${getApiBase()}/api/custom-players`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name, html }),
            });

            const responseText = await res.text();
            let data;
            try {
                data = JSON.parse(responseText);
            } catch {
                notificationText.set("Server returned invalid response");
                ShowNotification.set(true);
                uploading = false;
                return;
            }

            if (res.ok && data.success) {
                onSuccess(data.name);
                doClose();

                if (openEditor) {
                    editingPlayer.set(data.name);
                    editingPlayerIsCustom.set(true);
                    editorOpen.set(true);
                } else {
                    notificationText.set(`Player "${data.name}" added`);
                    ShowNotification.set(true);
                }
            } else if (data.validation) {
                htmlContent = html;
                validationErrors = data.validation.errors;
                showErrors = true;
                uploading = false;
            } else {
                notificationText.set(data.error || "Upload failed");
                ShowNotification.set(true);
                uploading = false;
            }
        } catch (e) {
            console.error("Upload error:", e);
            notificationText.set("Connection error");
            ShowNotification.set(true);
        }
        uploading = false;
    }

    function closeErrorDialog() {
        showErrors = false;
        validationErrors = [];
        htmlContent = "";
    }

    function openFilePicker() {
        fileInput?.click();
    }

    // Trigger fly-in when visible changes
    $: if (visible && anim === 'hidden') {
        requestAnimationFrame(() => { anim = 'fly-in'; closing = false; });
    }

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

        const PAD=500;
        let canvasReady=false;

        function renderCloud(canvas, t){
            if(!canvas) return;
            const el = contentEl;
            if(!el) return;
            const ew=el.offsetWidth, eh=el.offsetHeight;
            if(ew<2||eh<2) return;

            if(!canvasReady){
                canvas.style.left=-PAD+'px';
                canvas.style.top=-PAD+'px';
                canvas.style.width=(ew+PAD*2)+'px';
                canvas.style.height=(eh+PAD*2)+'px';
                canvasReady=true;
            }

            const TW=Math.ceil((ew+PAD*2)/S), TH=Math.ceil((eh+PAD*2)/S);
            const WS=Math.ceil(ew/S), HS=Math.ceil(eh/S);
            const padS=Math.ceil(PAD/S);

            if(!blobs) blobs=makeCloudBlobs(WS,HS, padS, padS);

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
            if(!visible){ canvasReady=false; blobs=null; return; }
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
        class="upl-backdrop"
        class:closing
        on:click|self={doClose}
        on:keydown={(e) => e.key === "Escape" && doClose()}
        role="dialog"
        tabindex="-1"
    >
        <div class="upl-stage" class:fly-in={anim==='fly-in'} class:fly-out={anim==='fly-out'} class:hidden={anim==='hidden'}>
            <canvas bind:this={cloudCanvas} class="cloud-cv"></canvas>
            <div class="upl-content" bind:this={contentEl}>
                <div class="upl-title">ADD CUSTOM PLAYER</div>

                <!-- Drop zone -->
                <div
                    class="drop-zone"
                    class:drag-over={dragOver}
                    class:uploading
                    on:dragover={handleDragOver}
                    on:dragleave={handleDragLeave}
                    on:drop={handleDrop}
                    on:click={openFilePicker}
                    on:keydown={(e) => e.key === "Enter" && openFilePicker()}
                    role="button"
                    tabindex="0"
                >
                    <input
                        bind:this={fileInput}
                        type="file"
                        accept=".html"
                        on:change={handleFileSelect}
                        hidden
                    />
                    {#if uploading}
                        <div class="dz-icon">...</div>
                        <div class="dz-text">UPLOADING</div>
                    {:else}
                        <div class="dz-icon">&lt;/&gt;</div>
                        <div class="dz-text">DROP HTML FILE</div>
                        <div class="dz-sub">or click to browse</div>
                    {/if}
                </div>

                <div class="divider">
                    <span>OR</span>
                </div>

                <!-- Start from scratch -->
                {#if showNameInput}
                    <div class="name-input-wrap">
                        <div class="name-input-label">PLAYER NAME</div>
                        <input
                            class="name-input"
                            type="text"
                            bind:value={nameInput}
                            placeholder="MyPlayer"
                            maxlength="40"
                            on:keydown={(e) => { if (e.key === 'Enter') confirmNameInput(); if (e.key === 'Escape') cancelNameInput(); }}
                            autofocus
                        />
                        <div class="name-input-hint">letters, numbers, _ -</div>
                        {#if nameTaken}
                            <div class="name-taken">This player name already exists</div>
                        {/if}
                        <div class="name-input-actions">
                            <button class="name-btn cancel" on:click={cancelNameInput}>CANCEL</button>
                            <button class="name-btn confirm" on:click={confirmNameInput} disabled={!nameInput.trim() || nameTaken}>CREATE</button>
                        </div>
                    </div>
                {:else}
                    <button
                        class="scratch-btn"
                        on:click={openNameInput}
                        disabled={uploading}
                    >
                        <div class="scratch-icon">[ ]</div>
                        <div class="scratch-text">
                            <span class="scratch-title">START FROM SCRATCH</span>
                            <span class="scratch-sub">base template -> opens in editor</span>
                        </div>
                    </button>
                {/if}

                <div class="upl-info">
                    <p>Only <strong>.html</strong> files. No JavaScript.</p>
                    <p>CSS vars: <code>--vibrant</code>, <code>--lightVibrant</code></p>
                    <p>Templates: <code>{'{{title}}'}</code>, <code>{'{{artist}}'}</code>, <code>{'{{thumbnail}}'}</code></p>
                </div>

                <button class="close-btn" on:click={doClose}>&times;</button>
            </div>
        </div>
    </div>
{/if}

<ValidationErrorDialog
    visible={showErrors}
    errors={validationErrors}
    html={htmlContent}
    onClose={closeErrorDialog}
/>

<style>
    .upl-backdrop {
        position: fixed; inset: 0; z-index: 9000;
        background: var(--c-backdrop);
        display: flex; align-items: center; justify-content: center;
        animation: backdropIn 0.3s ease-out both;
        overflow: visible;
    }
    .upl-backdrop.closing { animation: backdropOut 0.5s ease-in both; }
    @keyframes backdropIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes backdropOut { from { opacity: 1; } to { opacity: 0; } }

    .upl-stage {
        position: relative; width: 560px; overflow: visible; z-index: 1;
    }
    .upl-stage.hidden { visibility: hidden; transform: translateY(-200vh); }
    .upl-stage.fly-in { animation: uplFlyIn 0.7s cubic-bezier(.23,1.02,.32,1) both; }
    .upl-stage.fly-out { animation: uplFlyOut 0.5s cubic-bezier(.6,0,.7,.2) both; }
    @keyframes uplFlyIn { from { opacity: 0; transform: translateY(-120vh); } to { opacity: 1; transform: translateY(0); } }
    @keyframes uplFlyOut { from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(-120vh); } }

    .cloud-cv {
        position: absolute;
        image-rendering: pixelated;
        display: block; pointer-events: none; z-index: 0;
    }

    .upl-content {
        position: relative; z-index: 2; overflow: visible;
        padding: 2.5rem 2.5rem 2rem;
        display: flex; flex-direction: column; gap: 1.2rem;
        font-family: '8bitwonder', monospace;
    }

    .upl-title {
        font-size: 24px; letter-spacing: 0.1em;
        color: var(--c-text); text-align: center;
    }

    .drop-zone {
        padding: 2.5rem 1.5rem;
        border: 2px dashed color-mix(in srgb, var(--c1) 40%, transparent);
        background: color-mix(in srgb, var(--c1) 3%, transparent);
        display: flex; flex-direction: column; align-items: center; gap: 0.5rem;
        cursor: pointer; transition: all 0.2s;
        clip-path: polygon(8px 0, calc(100% - 8px) 0, 100% 8px, 100% calc(100% - 8px), calc(100% - 8px) 100%, 8px 100%, 0 calc(100% - 8px), 0 8px);
    }
    .drop-zone:hover, .drop-zone.drag-over {
        border-color: var(--c1);
        background: color-mix(in srgb, var(--c1) 8%, transparent);
    }
    .drop-zone.uploading { pointer-events: none; opacity: 0.7; }

    .dz-icon {
        font-size: 2.5rem; color: var(--c1);
    }
    .dz-text {
        font-size: 14px; color: var(--c-text); letter-spacing: 0.1em;
    }
    .dz-sub {
        font-size: 11px; color: color-mix(in srgb, var(--c-text) 40%, transparent);
    }

    .divider {
        display: flex; align-items: center; gap: 1rem;
    }
    .divider::before, .divider::after {
        content: ""; flex: 1; height: 1px;
        background: color-mix(in srgb, var(--c-text) 10%, transparent);
    }
    .divider span {
        font-size: 11px; color: color-mix(in srgb, var(--c-text) 30%, transparent);
        letter-spacing: 0.1em;
    }

    .scratch-btn {
        display: flex; align-items: center; gap: 1rem;
        padding: 1rem 1.25rem;
        background: color-mix(in srgb, var(--c1) 3%, transparent);
        border: 1px solid color-mix(in srgb, var(--c1) 10%, transparent);
        cursor: pointer; transition: all 0.2s; text-align: left; width: 100%;
    }
    .scratch-btn:hover:not(:disabled) {
        border-color: color-mix(in srgb, var(--c1) 30%, transparent);
        background: color-mix(in srgb, var(--c1) 6%, transparent);
    }
    .scratch-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .scratch-icon {
        font-size: 0.8rem; color: color-mix(in srgb, var(--c-text) 40%, transparent);
        border: 1px solid color-mix(in srgb, var(--c-text) 15%, transparent);
        padding: 0.5rem 0.6rem; flex-shrink: 0; transition: all 0.2s;
    }
    .scratch-btn:hover .scratch-icon { color: var(--c1); border-color: var(--c1); }

    .scratch-text { display: flex; flex-direction: column; gap: 0.3rem; }
    .scratch-title {
        font-size: 13px; color: color-mix(in srgb, var(--c-text) 70%, transparent);
        letter-spacing: 0.06em; transition: color 0.2s;
    }
    .scratch-btn:hover .scratch-title { color: var(--c-text); }
    .scratch-sub {
        font-family: 'Rubik', sans-serif; font-size: 12px;
        color: color-mix(in srgb, var(--c-text) 35%, transparent);
    }

    .name-input-wrap {
        display: flex; flex-direction: column; gap: 0.5rem;
        padding: 1rem 1.25rem;
        background: color-mix(in srgb, var(--c1) 3%, transparent);
        border: 1px solid color-mix(in srgb, var(--c1) 15%, transparent);
    }
    .name-input-label {
        font-size: 11px; color: color-mix(in srgb, var(--c-text) 40%, transparent);
        letter-spacing: 0.08em;
    }
    .name-input {
        background: color-mix(in srgb, var(--c-text) 4%, transparent);
        border: 1px solid color-mix(in srgb, var(--c1) 20%, transparent);
        padding: 0.5rem 0.75rem;
        font-family: '8bitwonder', monospace; font-size: 14px;
        color: var(--c-text); outline: none; transition: border-color 0.2s;
    }
    .name-input:focus { border-color: var(--c1); }
    .name-input::placeholder { color: color-mix(in srgb, var(--c-text) 20%, transparent); }

    .name-input-hint {
        font-family: 'Rubik', sans-serif; font-size: 11px;
        color: color-mix(in srgb, var(--c-text) 25%, transparent);
    }
    .name-taken {
        font-family: 'Rubik', sans-serif; font-size: 11px;
        color: var(--c-red);
    }
    .name-input-actions { display: flex; gap: 0.5rem; margin-top: 0.25rem; }

    .name-btn {
        flex: 1; padding: 0.45rem 0;
        font-family: '8bitwonder', monospace; font-size: 12px;
        letter-spacing: 0.06em; cursor: pointer; transition: all 0.2s;
    }
    .name-btn.cancel {
        background: transparent;
        border: 1px solid color-mix(in srgb, var(--c-text) 15%, transparent);
        color: color-mix(in srgb, var(--c-text) 50%, transparent);
    }
    .name-btn.cancel:hover { border-color: var(--c1); color: var(--c-text); }
    .name-btn.confirm {
        background: color-mix(in srgb, var(--c1) 10%, transparent);
        border: 1px solid color-mix(in srgb, var(--c1) 30%, transparent);
        color: var(--c1);
    }
    .name-btn.confirm:hover:not(:disabled) {
        background: color-mix(in srgb, var(--c1) 18%, transparent);
        border-color: var(--c1);
    }
    .name-btn.confirm:disabled { opacity: 0.35; cursor: not-allowed; }

    .upl-info {
        padding: 0.75rem 1rem;
        background: color-mix(in srgb, var(--c-text) 3%, transparent);
        border: 1px solid color-mix(in srgb, var(--c-text) 6%, transparent);
    }
    .upl-info p {
        font-size: 11px; color: color-mix(in srgb, var(--c-text) 40%, transparent);
        margin: 0.2rem 0; line-height: 1.5;
    }
    .upl-info code {
        color: var(--c1);
        background: color-mix(in srgb, var(--c1) 10%, transparent);
        padding: 0.1rem 0.3rem;
    }

    .close-btn {
        position: absolute; top: 0.8rem; right: 0.8rem;
        background: none; border: none; cursor: pointer;
        font-size: 1.4rem; color: color-mix(in srgb, var(--c-text) 50%, transparent);
        font-family: '8bitwonder', monospace; transition: color 0.2s;
    }
    .close-btn:hover { color: var(--c-text); }
</style>
