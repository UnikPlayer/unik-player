<script>
    import { onMount } from "svelte";
    import {
        ShowNotification,
        notificationText,
        language,
    } from "$lib/stores/stores";

    export let mode = "dynamic";
    export let color = "#B87333";

    const palette = [
        ["--vibrant", "#D4944A"],
        ["--lightVibrant", "#F5DEB3"],
        ["--darkVibrant", "#5C4033"],
        ["--muted", "#8B6914"],
        ["--lightMuted", "#B87333"],
        ["--darkMuted", "#1a1510"],
    ];

    function rgbStrToHex(rgb) {
        const m = rgb.match(/\d+/g);
        if (!m || m.length < 3) return null;
        const toHex = (n) => (+n).toString(16).padStart(2, "0");
        return `#${toHex(m[0])}${toHex(m[1])}${toHex(m[2])}`.toUpperCase();
    }

    function copyText(text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(text).catch(() => {});
                return;
            }
        } catch {}
        // Fallback for insecure contexts (HTTP / LAN IP)
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.focus();
        ta.select();
        try { document.execCommand("copy"); } catch {}
        document.body.removeChild(ta);
    }

    function copyDotHex(e) {
        const bg = getComputedStyle(e.currentTarget).backgroundColor;
        const hex = rgbStrToHex(bg) || bg;
        copyText(hex);
        notificationText.set(
            $language === "ru" ? `Скопировано: ${hex}` : `Copied: ${hex}`,
        );
        ShowNotification.set(false);
        ShowNotification.set(true);
    }

    let hue = 30;
    let saturation = 70;
    let lightness = 50;
    let dragging = false;
    let wheelEl;

    onMount(() => {
        const hsl = hexToHsl(color);
        if (hsl) ({ h: hue, s: saturation, l: lightness } = hsl);
    });

    function hexToHsl(hex) {
        const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        if (!m) return null;
        let r = parseInt(m[1], 16) / 255;
        let g = parseInt(m[2], 16) / 255;
        let b = parseInt(m[3], 16) / 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        let h = 0, s = 0, l = (max + min) / 2;
        if (max !== min) {
            const d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
            else if (max === g) h = ((b - r) / d + 2) / 6;
            else h = ((r - g) / d + 4) / 6;
        }
        return { h: Math.round(h * 360), s: Math.round(s * 100), l: Math.round(l * 100) };
    }

    function hslToHex(h, s, l) {
        s /= 100; l /= 100;
        const a = s * Math.min(l, 1 - l);
        const f = (n) => {
            const k = (n + h / 30) % 12;
            const c = l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
            return Math.round(255 * c).toString(16).padStart(2, "0");
        };
        return `#${f(0)}${f(8)}${f(4)}`;
    }

    function pickFromMouse(e) {
        const r = wheelEl.getBoundingClientRect();
        const cx = r.left + r.width / 2;
        const cy = r.top + r.height / 2;
        const dx = e.clientX - cx;
        const dy = e.clientY - cy;
        const angle = (Math.atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360;
        const dist = Math.min(Math.hypot(dx, dy) / (r.width / 2), 1);
        hue = Math.round(angle);
        saturation = Math.round(dist * 100);
        color = hslToHex(hue, saturation, lightness);
    }

    function onWheelDown(e) { dragging = true; pickFromMouse(e); }
    function onWheelMove(e) { if (dragging) pickFromMouse(e); }
    function onWheelUp() { dragging = false; }

    function onLightness(e) {
        lightness = +e.target.value;
        color = hslToHex(hue, saturation, lightness);
    }

    function onHexInput(e) {
        const v = e.target.value.trim().replace(/^#/, "");
        if (/^[0-9A-Fa-f]{6}$/.test(v)) {
            color = "#" + v;
            const hsl = hexToHsl(color);
            if (hsl) ({ h: hue, s: saturation, l: lightness } = hsl);
        }
    }

    const WHEEL_SIZE = 80;
    $: dotAngle = hue;
    $: dotRadius = (saturation / 100) * (WHEEL_SIZE / 2);
</script>

<svelte:window on:mousemove={onWheelMove} on:mouseup={onWheelUp} />

<div class="picker">
    <div class="modes">
        <button id="color-picker" class:on={mode === "dynamic"} on:click={() => (mode = "dynamic")}>DYNAMIC</button>
        <button id="color-picker" class:on={mode === "static"} on:click={() => (mode = "static")}>STATIC</button>
    </div>

    {#if mode === "static"}
        <div class="hex">
            <span class="swatch" style="background: {color}"></span>
            <span class="hash">#</span>
            <input
                type="text"
                value={color.replace(/^#/, "").toUpperCase()}
                on:input={onHexInput}
                maxlength="6"
                spellcheck="false"
            />
        </div>

        <div
            class="wheel"
            bind:this={wheelEl}
            on:mousedown={onWheelDown}
            role="slider"
            tabindex="0"
            aria-valuenow={hue}
            aria-label="Color wheel"
        >
            <div
                class="wheel-dot"
                style="transform: translate(-50%, -50%) rotate({dotAngle}deg) translateY(-{dotRadius}px);"
            ></div>
        </div>

        <div class="lrow">
            <span>L</span>
            <input type="range" min="10" max="90" value={lightness} on:input={onLightness} />
            <span>{lightness}</span>
        </div>
    {:else}
        <div class="dyn">
            <span class="info">Colors sync from album art</span>
            <div class="palette">
                {#each palette as [v, fb]}
                    <button
                        class="dot"
                        style="background: var({v}, {fb})"
                        aria-label="Copy {v.replace(/^--/, '')}"
                        on:click={copyDotHex}
                    >
                        <span class="dot-tooltip">{v.replace(/^--/, "")}</span>
                    </button>
                {/each}
            </div>
        </div>
    {/if}
</div>

<style lang="scss">
    .picker {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .modes {
        display: flex;
        gap: 0.4rem;

        button {
            flex: 1;
            min-width: 0;
            padding: 0.5rem 0.3rem;
            font-family: '8bitwonder', monospace;
            font-size: 0.8rem;
            background: transparent;
            border: 3px solid rgba(0, 0, 0, 0.15);
            color: var(--c1);
            cursor: pointer;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;

            &.on {
                border-color: var(--c1);
                background: rgba(0, 0, 0, 0.06);
            }
        }
    }

    .dyn {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.4rem;
        padding: 0.5rem;
        background: rgba(0, 0, 0, 0.03);
    }

    .info {
        font-family: 'Rubik', sans-serif;
        font-size: 0.8rem;
        color: var(--c1);
        text-align: center;
    }

    .palette {
        display: flex;
        gap: 0.4rem;
        padding-top: 0.5rem;
    }

    .palette .dot {
        position: relative;
        width: 32px;
        height: 32px;
        border: 3px solid rgba(0, 0, 0, 0.2);
        background: #888;
        cursor: pointer;
        padding: 0;
        transition: transform 0.15s;

        &:hover {
            transform: scale(1.15);
            z-index: 5;

            .dot-tooltip {
                opacity: 1;
                transform: translate(-50%, -4px);
            }
        }
    }

    .dot-tooltip {
        position: absolute;
        bottom: 100%;
        left: 50%;
        transform: translate(-50%, 0);
        padding: 0.25rem 0.45rem;
        background: var(--c1);
        color: #fff;
        font-family: '8bitwonder', monospace;
        font-size: 0.7rem;
        white-space: nowrap;
        opacity: 0;
        pointer-events: none;
        transition: opacity 0.15s, transform 0.15s;
    }

    .hex {
        display: flex;
        align-items: center;
        gap: 0.4rem;
    }

    .swatch {
        width: 28px;
        height: 28px;
        border: 3px solid rgba(0, 0, 0, 0.2);
        flex-shrink: 0;
    }

    .hash {
        font-family: 'Press Start 2P', monospace;
        font-size: 0.8rem;
        color: var(--c1);
    }

    .hex input {
        flex: 1;
        min-width: 0;
        padding: 0.3rem 0.5rem;
        background: rgba(0, 0, 0, 0.05);
        border: 3px solid rgba(0, 0, 0, 0.15);
        font-family: '8bitwonder', monospace;
        font-size: 0.95rem;
        color: var(--c1);
        text-transform: uppercase;
        outline: none;
    }

    .wheel {
        align-self: center;
        width: 80px;
        height: 80px;
        border-radius: 50%;
        flex-shrink: 0;
        background:
            radial-gradient(circle, white 0%, transparent 70%),
            conic-gradient(
                from 0deg,
                hsl(0, 100%, 50%), hsl(60, 100%, 50%), hsl(120, 100%, 50%),
                hsl(180, 100%, 50%), hsl(240, 100%, 50%), hsl(300, 100%, 50%),
                hsl(360, 100%, 50%)
            );
        position: relative;
        cursor: crosshair;
    }

    .wheel-dot {
        position: absolute;
        top: 50%;
        left: 50%;
        width: 10px;
        height: 10px;
        border: 2px solid var(--c1);
        border-radius: 50%;
        background: transparent;
        pointer-events: none;
    }

    .lrow {
        display: flex;
        align-items: center;
        gap: 0.4rem;

        span {
            font-family: '8bitwonder', monospace;
            font-size: 1rem;
            color: var(--c1);
            min-width: 1.5rem;
            text-align: center;
        }

        input[type="range"] {
            flex: 1;
            min-width: 0;
            height: 4px;
            -webkit-appearance: none;
            appearance: none;
            background: linear-gradient(90deg, #000, #fff);
            cursor: pointer;

            &::-webkit-slider-thumb {
                -webkit-appearance: none;
                width: 12px;
                height: 12px;
                background: var(--c1);
                border-radius: 50%;
                cursor: pointer;
            }
        }
    }
</style>
