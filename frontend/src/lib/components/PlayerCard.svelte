<script>
    import { onMount, onDestroy, tick } from "svelte";
    import {
        editorOpen,
        editingPlayer,
        editingPlayerIsCustom,
        ShowNotification,
        notificationText,
        playerStyles,
        title as trackTitle,
        artist as trackArtist,
        thumbnail as trackThumbnail,
    } from "$lib/stores/stores.js";
    import { copyPlayerStyle } from "$lib/playerButtons.js";
    import { deleteCustomPlayer, getPlayerMeta } from "$lib/getPlayers.js";
    import { generateColorVars } from "$lib/utils/colors.js";

    export let component;
    export let name;
    export let isCustom = false;
    export let error = null;

    $: savedStyle = $playerStyles[name] || {};
    $: useStaticColor = savedStyle.colorMode === "static";
    $: staticColorValue = savedStyle.staticColor || "#B87333";
    $: colors = useStaticColor ? generateColorVars(staticColorValue) : null;
    $: fontFamily = savedStyle.font || "Rubik";
    $: previewScale =
        savedStyle.previewScale ?? getPlayerMeta(name)?.defaultScale ?? 0.5;

    $: previewStyle =
        useStaticColor && colors
            ? `
    --vibrant: ${colors.vibrant};
    --lightVibrant: ${colors.lightVibrant};
    --darkVibrant: ${colors.darkVibrant};
    --muted: ${colors.muted};
    --lightMuted: ${colors.lightMuted};
    --darkMuted: ${colors.darkMuted};
    font-family: "${fontFamily}", sans-serif;
    transform: translate(-50%, -50%) scale(${previewScale});
  `
            : `font-family: "${fontFamily}", sans-serif; transform: translate(-50%, -50%) scale(${previewScale});`;

    function openEditor() {
        editingPlayer.set(name);
        editingPlayerIsCustom.set(isCustom);
        editorOpen.set(true);
    }

    async function selectPlayer() {
        await copyPlayerStyle(name);
        notificationText.set("COPIED_TO_BUFFER");
        ShowNotification.set(true);
        setTimeout(() => ShowNotification.set(false), 2500);
    }

    async function handleDelete() {
        if (!confirm(`Delete "${name}" player?`)) return;
        const success = await deleteCustomPlayer(name);
        if (success) {
            notificationText.set(`"${name}" deleted`);
            ShowNotification.set(true);
            window.dispatchEvent(new CustomEvent("unik-player-deleted"));
        } else {
            notificationText.set("Delete failed");
            ShowNotification.set(true);
        }
    }

    // Cloud blob system — unified pipeline at S=8 (no downscale, solid pixels)
    const S = 8;
    const PAD = 0.15;

    class CloudBlob {
        constructor(x,y,r){this.hx=x;this.hy=y;this.r=r;this.dx=0;this.dy=0;this.vx=0;this.vy=0;
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

    let cardEl = null;
    let cloudCv = null;
    let blobs = null;
    let mx = -999, my = -999;

    // Cleanup refs — must be at component scope for onDestroy
    let _raf = null;
    let _off1 = null, _off2 = null, _off3 = null;
    let _onMM = null, _onML = null;
    let _destroyed = false;

    onDestroy(() => {
        _destroyed = true;
        if (_raf) cancelAnimationFrame(_raf);
        if (cardEl) {
            if (_onMM) cardEl.removeEventListener('mousemove', _onMM);
            if (_onML) cardEl.removeEventListener('mouseleave', _onML);
        }
        if (_off1) _off1.width = _off1.height = 0;
        if (_off2) _off2.width = _off2.height = 0;
        if (_off3) _off3.width = _off3.height = 0;
        if (cloudCv) cloudCv.width = cloudCv.height = 0;
        blobs = null;
    });

    onMount(async () => {
        await tick();
        if (document.fonts && document.fonts.ready) {
            await document.fonts.ready;
        }
        if (_destroyed) return;

        if (!cloudCv || !cardEl) return;
        const W = cardEl.offsetWidth, H = cardEl.offsetHeight;
        if (W < 2 || H < 2) return;

        const CW = Math.round(W * (1 + PAD*2));
        const CH = Math.round(H * (1 + PAD*2));
        const TW = Math.ceil(CW/S), TH = Math.ceil(CH/S);

        const cx = TW*0.5, cy = TH*0.5;
        const rw = TW*0.23, rh = TH*0.21;
        blobs = [];

        // Generate seeded blobs based on name
        let seed = 0;
        for (let i = 0; i < name.length; i++) seed = ((seed << 5) - seed + name.charCodeAt(i)) | 0;
        function srand(i) { let x = Math.sin(Math.abs(seed) * 9301 + i * 49297) * 49297; return x - Math.floor(x); }

        for (let i = 0; i < 160; i++) {
            const a = srand(i*2) * Math.PI * 2;
            const dist = Math.pow(srand(i*2+1), 0.55);
            blobs.push(new CloudBlob(
                cx + Math.cos(a) * rw * dist,
                cy + Math.sin(a) * rh * dist,
                3 + srand(i*3) * 5
            ));
        }

        const off1 = document.createElement('canvas'); off1.width=TW; off1.height=TH;
        _off1 = off1;
        const c1 = off1.getContext('2d');
        const off2 = document.createElement('canvas'); off2.width=TW; off2.height=TH;
        _off2 = off2;
        const c2 = off2.getContext('2d');
        const off3 = document.createElement('canvas'); off3.width=TW; off3.height=TH;
        _off3 = off3;
        const c3 = off3.getContext('2d');

        cloudCv.width = TW; cloudCv.height = TH;
        const ctx = cloudCv.getContext('2d');
        ctx.imageSmoothingEnabled = false;

        const cs = getComputedStyle(document.documentElement);
        function parseCSSColor(varName){
            const v=cs.getPropertyValue(varName).trim();
            if(!v)return[255,255,255];
            if(v.startsWith('#')){
                const h=v.replace('#','');
                if(h.length===3)return[parseInt(h[0]+h[0],16),parseInt(h[1]+h[1],16),parseInt(h[2]+h[2],16)];
                return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)];
            }
            const m=v.match(/(\d+)/g);
            return m?[+m[0],+m[1],+m[2]]:[255,255,255];
        }
        const cCloud=parseCSSColor('--c-cloud');
        const cOutline=parseCSSColor('--c-cloud-outline');

        let cardTick = 0;

        function render() {
            if (_destroyed || !blobs) return;
            // Normal pass — draw blobs at normal size
            c1.clearRect(0,0,TW,TH);
            for (const b of blobs) {
                b.update(mx, my, cardTick);
                const bx=b.hx+b.dx, by=b.hy+b.dy;
                const g = c1.createRadialGradient(bx,by,0,bx,by,b.r);
                g.addColorStop(0,'rgba(255,255,255,1)');
                g.addColorStop(0.5,'rgba(255,255,255,0.85)');
                g.addColorStop(1,'rgba(255,255,255,0)');
                c1.fillStyle=g; c1.beginPath(); c1.arc(bx,by,b.r,0,Math.PI*2); c1.fill();
            }
            c2.clearRect(0,0,TW,TH);
            c2.filter='blur(2.5px)'; c2.drawImage(off1,0,0); c2.filter='none';
            const imgN = c2.getImageData(0,0,TW,TH);
            // Grown pass — slightly larger blobs for outline ring
            c1.clearRect(0,0,TW,TH);
            for (const b of blobs) {
                const bx=b.hx+b.dx, by=b.hy+b.dy, br=b.r+1;
                const g = c1.createRadialGradient(bx,by,0,bx,by,br);
                g.addColorStop(0,'rgba(255,255,255,1)');
                g.addColorStop(0.5,'rgba(255,255,255,0.85)');
                g.addColorStop(1,'rgba(255,255,255,0)');
                c1.fillStyle=g; c1.beginPath(); c1.arc(bx,by,br,0,Math.PI*2); c1.fill();
            }
            c3.clearRect(0,0,TW,TH);
            c3.filter='blur(2.5px)'; c3.drawImage(off1,0,0); c3.filter='none';
            const imgG = c3.getImageData(0,0,TW,TH);
            const dN=imgN.data, dG=imgG.data;
            const out=c2.createImageData(TW,TH);
            const od=out.data;
            for (let i=0; i<dN.length; i+=4) {
                const vN=dN[i], vG=dG[i];
                const inNorm=vN>55, inGrown=vG>55;
                if (inGrown && !inNorm) {
                    od[i]=cOutline[0]; od[i+1]=cOutline[1]; od[i+2]=cOutline[2]; od[i+3]=255;
                } else if (inNorm) {
                    od[i]=cCloud[0]; od[i+1]=cCloud[1]; od[i+2]=cCloud[2]; od[i+3]=255;
                }
            }
            c2.putImageData(out,0,0);
            ctx.clearRect(0,0,TW,TH);
            ctx.drawImage(off2,0,0);
        }

        _onMM = function(e) {
            const r = cloudCv.getBoundingClientRect();
            if (r.width < 1) return;
            mx = (e.clientX - r.left) / r.width * TW;
            my = (e.clientY - r.top) / r.height * TH;
        };
        _onML = function() { mx=-999; my=-999; };
        cardEl.addEventListener('mousemove', _onMM);
        cardEl.addEventListener('mouseleave', _onML);

        let last=0;

        function frame(ts) {
            if (_destroyed) return;
            _raf = requestAnimationFrame(frame);
            if (ts-last < 32) return; last=ts; cardTick++;
            render();
        }

        _raf = requestAnimationFrame(frame);
    });
</script>

<div class="card" bind:this={cardEl}>
    <canvas bind:this={cloudCv} class="cloud-cv"></canvas>
    <div class="card-inner">
        <div class="card-top">
            <span class="card-name">{name.replace(/([A-Z])/g, "_$1").toUpperCase()}</span>
            {#if isCustom}
                <span class="custom-badge">CUSTOM</span>
            {/if}
        </div>
        <div class="card-preview">
            <div class="preview-container" style={previewStyle}>
                {#if isCustom}
                    <svelte:component
                        this={component}
                        playerName={name}
                        title={$trackTitle || "Track Title"}
                        artist={$trackArtist || "Artist Name"}
                        thumbnail={$trackThumbnail || "/thumbnail.jpg"}
                        colors={colors || {}}
                        font={fontFamily}
                        visible={true}
                    />
                {:else}
                    <svelte:component
                        this={component}
                        preview={false}
                        showAlways={true}
                    />
                {/if}
            </div>
        </div>
        <div class="card-bottom">
            <button class="act" on:click={selectPlayer}>COPY</button>
            <button class="act" on:click={openEditor}>EDIT</button>
            {#if isCustom}
                <button class="act del" on:click={handleDelete}>DEL</button>
            {/if}
        </div>
        {#if error}
            <div class="error-indicator" title={error}>!!!</div>
        {/if}
    </div>
</div>

<style>
    .card {
        position: relative; overflow: visible;
        aspect-ratio: 500 / 420;
        z-index: 0;
    }
    .card:hover {
        z-index: 10;
    }
    .cloud-cv {
        position: absolute;
        top: -15%; left: -15%;
        width: 130%; height: 130%;
        image-rendering: pixelated;
        display: block; pointer-events: none; z-index: 0;
    }
    .card-inner {
        position: relative; z-index: 1;
        display: flex; flex-direction: column; align-items: center;
        justify-content: space-between;
        height: 100%; padding: 12% 10% 8%;
        font-family: '8bitwonder', monospace;
        box-sizing: border-box;
        pointer-events: none;
    }
    .card-inner > :global(*) {
        pointer-events: auto;
    }
    .card-top {
        display: flex; align-items: center; gap: 8px;
        justify-content: center; flex-shrink: 0;
        margin-bottom: 2px;
    }
    .card-name {
        font-size: 13px; color: var(--c-text);
        letter-spacing: 0.05em;
        white-space: nowrap;
    }
    .custom-badge {
        font-size: 9px;
        color: #B87333;
        background: rgba(184, 115, 51, 0.15);
        border: 1px solid rgba(184, 115, 51, 0.3);
        padding: 2px 6px;
        letter-spacing: 0.05em;
    }
    .card-preview {
        position: absolute;
        top: -15%; left: -15%;
        width: 130%; height: 130%;
        overflow: hidden;
        pointer-events: none;
        z-index: 0;
    }
    .preview-container {
        position: absolute;
        top: 50%;
        left: 50%;
        transform-origin: center center;
    }
    .preview-container :global(> *) {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
    }
    .preview-container :global(.custom-player-wrapper) {
        width: 500px;
        height: 180px;
    }
    .card-bottom {
        display: flex; align-items: center; gap: 10px;
        justify-content: center; flex-shrink: 0;
        padding-top: 2px;
    }
    .act {
        font-family: '8bitwonder', monospace; font-size: 12px;
        background: none; color: var(--c1);
        border: none; cursor: pointer; padding: 2px 8px;
        transition: all 0.15s; opacity: 0.6;
        -webkit-text-stroke: 1px #ffffff;
        paint-order: stroke fill;
    }
    .act:hover { opacity: 1; background: rgba(10,10,18,0.15); }
    .act.del { color: #cc2222; }
    .act.del:hover { color: #ff3333; opacity: 1; }

    .error-indicator {
        position: absolute;
        bottom: 0.75rem;
        right: 1rem;
        font-size: 11px;
        font-weight: 700;
        color: #ef4444;
        background: rgba(239, 68, 68, 0.15);
        border: 1px solid rgba(239, 68, 68, 0.4);
        padding: 2px 6px;
        cursor: help;
        animation: errorPulse 2s ease-in-out infinite;
    }

    @keyframes errorPulse {
        0%, 100% { opacity: 1; }
        50% { opacity: 0.6; }
    }
</style>
