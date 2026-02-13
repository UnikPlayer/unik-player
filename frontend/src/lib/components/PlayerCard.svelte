<script>
    import { onMount, tick } from "svelte";
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

    // Get saved style for this player
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
            // Dispatch event to refresh player list
            window.dispatchEvent(new CustomEvent("unik-player-deleted"));
        } else {
            notificationText.set("Delete failed");
            ShowNotification.set(true);
        }
    }

    onMount(async () => {
        await tick();
        // Wait for fonts to load
        if (document.fonts && document.fonts.ready) {
            await document.fonts.ready;
        }
    });
</script>

<div class="player-card" class:is-custom={isCustom} class:has-error={error}>
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
        <span class="player-name"
            >{name.replace(/([A-Z])/g, "_$1").toUpperCase()}</span
        >
        {#if isCustom}
            <span class="custom-badge">CUSTOM</span>
        {/if}
        {#if error}
            <div class="error-indicator" title={error}>!!!</div>
        {/if}
    </div>

    <div class="card-actions">
        <button class="btn btn-select" on:click={selectPlayer}> SELECT </button>
        <button class="btn btn-edit" on:click={openEditor}> EDIT </button>
        {#if isCustom}
            <button class="btn btn-delete" on:click={handleDelete}>
                DEL
            </button>
        {/if}
    </div>
</div>

<style lang="scss">
    .player-card {
        position: relative;
        background: linear-gradient(
            135deg,
            rgba(20, 20, 25, 0.9),
            rgba(30, 30, 40, 0.8)
        );
        border: 1px solid rgba(255, 255, 255, 0.1);
        border-radius: 8px;
        overflow: hidden;
        transition: all 0.3s ease;

        &::before {
            content: "";
            position: absolute;
            inset: 0;
            background: linear-gradient(
                135deg,
                rgba(184, 115, 51, 0.05) 0%,
                transparent 50%,
                rgba(99, 102, 241, 0.05) 100%
            );
            pointer-events: none;
            border-radius: inherit;
        }

        &:hover {
            border-color: rgba(184, 115, 51, 0.4);
            transform: translateY(-2px);
            box-shadow: 0 8px 32px rgba(184, 115, 51, 0.15);
        }
    }

    .card-preview {
        position: relative;
        height: 180px;
        display: flex;
        align-items: center;
        justify-content: center;
        overflow: hidden;
        background:
            linear-gradient(180deg, transparent 0%, rgba(0, 0, 0, 0.3) 100%),
            repeating-linear-gradient(
                0deg,
                transparent,
                transparent 2px,
                rgba(255, 255, 255, 0.02) 2px,
                rgba(255, 255, 255, 0.02) 4px
            );
    }

    .preview-container {
        position: absolute;
        top: 50%;
        left: 50%;
        transform-origin: center center;
        /* transform включает translate(-50%, -50%) и scale() - задаётся через inline style */
    }

    /* Center all direct children (both Svelte players and custom wrappers) */
    .preview-container :global(> *) {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
    }

    /* Custom player wrapper — explicit size so iframe has dimensions to fill */
    .preview-container :global(.custom-player-wrapper) {
        width: 500px;
        height: 180px;
    }

    .player-name {
        position: absolute;
        bottom: 0.75rem;
        left: 1rem;
        font-family: "Press Start 2P", monospace;
        font-size: 0.45rem;
        font-weight: 400;
        color: rgba(255, 255, 255, 0.6);
        letter-spacing: 0.02em;
    }

    .card-actions {
        display: flex;
        border-top: 1px solid rgba(255, 255, 255, 0.1);
        border-radius: 0 0 8px 8px;
        overflow: hidden;
    }

    .btn {
        flex: 1;
        padding: 1rem;
        font-family: "Press Start 2P", monospace;
        font-size: 0.5rem;
        font-weight: 400;
        letter-spacing: 0.05em;
        border: none;
        cursor: pointer;
        transition: all 0.2s ease;
        text-transform: uppercase;
    }

    .btn-select {
        background: rgba(184, 115, 51, 0.2);
        color: #b87333;
        border-right: 1px solid rgba(255, 255, 255, 0.1);

        &:hover {
            background: rgba(184, 115, 51, 0.4);
            color: #d4944a;
        }
    }

    .btn-edit {
        background: rgba(255, 255, 255, 0.05);
        color: rgba(255, 255, 255, 0.7);

        &:hover {
            background: rgba(255, 255, 255, 0.1);
            color: white;
        }
    }

    .btn-delete {
        background: rgba(255, 80, 80, 0.1);
        color: rgba(255, 100, 100, 0.8);
        border-left: 1px solid rgba(255, 255, 255, 0.1);
        flex: 0 0 auto;
        padding: 1rem 0.75rem;

        &:hover {
            background: rgba(255, 80, 80, 0.25);
            color: #ff6b6b;
        }
    }

    // Custom player badge
    .custom-badge {
        position: absolute;
        top: 0.5rem;
        right: 0.5rem;
        font-family: "JetBrains Mono", monospace;
        font-size: 0.5rem;
        font-weight: 600;
        color: #b87333;
        background: rgba(184, 115, 51, 0.15);
        border: 1px solid rgba(184, 115, 51, 0.3);
        padding: 0.2rem 0.4rem;
        border-radius: 3px;
        letter-spacing: 0.05em;
    }

    .is-custom {
        border-color: rgba(184, 115, 51, 0.25);

        &:hover {
            border-color: rgba(184, 115, 51, 0.5);
        }
    }

    // Error state
    .has-error {
        border-color: rgba(239, 68, 68, 0.4);

        &:hover {
            border-color: rgba(239, 68, 68, 0.6);
            box-shadow: 0 8px 32px rgba(239, 68, 68, 0.15);
        }
    }

    .error-indicator {
        position: absolute;
        bottom: 0.75rem;
        right: 1rem;
        font-family: "Press Start 2P", monospace;
        font-size: 0.6rem;
        font-weight: 700;
        color: #ef4444;
        background: rgba(239, 68, 68, 0.15);
        border: 1px solid rgba(239, 68, 68, 0.4);
        padding: 0.25rem 0.5rem;
        border-radius: 4px;
        cursor: help;
        animation: errorPulse 2s ease-in-out infinite;
    }

    @keyframes errorPulse {
        0%,
        100% {
            opacity: 1;
        }
        50% {
            opacity: 0.6;
        }
    }
</style>
