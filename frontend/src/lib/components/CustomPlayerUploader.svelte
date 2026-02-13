<script>
    import { fly, fade } from "svelte/transition";
    import ValidationErrorDialog from "./ValidationErrorDialog.svelte";
    import {
        ShowNotification,
        notificationText,
        editorOpen,
        editingPlayer,
        editingPlayerIsCustom,
    } from "$lib/stores/stores";

    function getApiBase() {
        if (typeof window === "undefined") return "http://localhost:27272";
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

    /** @type {HTMLInputElement} */
    let fileInput;

    // Базовый HTML для "start from scratch"
    const BASE_HTML = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <style>
    * { font-family: 'Rubik', sans-serif; }
    html, body { margin: 0; padding: 0; background: transparent; }
    :root {
      --vibrant: #B87333; --lightVibrant: #D4944A;
      --darkVibrant: #5C4033; --muted: #8B6914;
      --lightMuted: #C8A86B; --darkMuted: rgba(20,15,10,0.9);
    }
  </style>
</head>
<body>
  <p>{{title}}</p>
  <p>{{artist}}</p>
  <img src="{{thumbnail}}" alt="cover" width="100" />
</body>
</html>`

    // Ввод имени для нового плеера
    let nameInput = '';
    let showNameInput = false;

    function openNameInput() {
        nameInput = '';
        showNameInput = true;
    }

    function cancelNameInput() {
        showNameInput = false;
        nameInput = '';
    }

    async function confirmNameInput() {
        const trimmed = nameInput.trim().replace(/[^a-zA-Z0-9_-]/g, '_');
        if (!trimmed) return;
        showNameInput = false;
        await uploadAndOpen(trimmed, BASE_HTML, true);
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
        const name = file.name.replace(".html", "");
        const text = await file.text();
        await uploadAndOpen(name, text, false);
    }

    async function startFromScratch() {
        // Генерируем уникальное имя
        const name = "MyPlayer_" + Date.now().toString(36);
        await uploadAndOpen(name, BASE_HTML, true);
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
                onClose();

                if (openEditor) {
                    // Открываем Editor сразу
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
</script>

{#if visible}
    <div
        class="overlay"
        transition:fade={{ duration: 200 }}
        on:click={onClose}
        on:keydown={(e) => e.key === "Escape" && onClose()}
        role="dialog"
        tabindex="-1"
    >
        <div
            class="dialog"
            transition:fly={{ y: 30, duration: 300 }}
            on:click|stopPropagation
            role="document"
        >
            <div class="header">
                <h2>ADD CUSTOM PLAYER</h2>
                <button class="close-btn" on:click={onClose}>X</button>
            </div>

            <div class="content">
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
                        <div class="icon">...</div>
                        <div class="text">UPLOADING</div>
                    {:else}
                        <div class="icon">&lt;/&gt;</div>
                        <div class="text">DROP HTML FILE</div>
                        <div class="subtext">or click to browse</div>
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
                        <div class="name-input-actions">
                            <button class="name-btn cancel" on:click={cancelNameInput}>CANCEL</button>
                            <button class="name-btn confirm" on:click={confirmNameInput} disabled={!nameInput.trim()}>CREATE</button>
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
                            <span class="scratch-sub">base template → opens in editor</span>
                        </div>
                    </button>
                {/if}
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

<style lang="scss">
    .overlay {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.8);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 9000;
        backdrop-filter: blur(5px);
    }

    .dialog {
        width: 90%;
        max-width: 420px;
        background: rgba(15, 15, 20, 0.98);
        border: 1px solid rgba(184, 115, 51, 0.5);
        border-radius: 8px;
        overflow: hidden;
        box-shadow:
            0 0 60px rgba(184, 115, 51, 0.15),
            0 20px 40px rgba(0, 0, 0, 0.5);
    }

    .header {
        padding: 1.25rem 1.5rem;
        border-bottom: 1px solid rgba(184, 115, 51, 0.3);
        display: flex;
        align-items: center;
        justify-content: space-between;

        h2 {
            font-family: "JetBrains Mono", monospace;
            font-size: 0.9rem;
            font-weight: 700;
            color: #b87333;
            letter-spacing: 0.15em;
            margin: 0;
        }

        .close-btn {
            background: transparent;
            border: 1px solid rgba(255, 255, 255, 0.3);
            color: rgba(255, 255, 255, 0.7);
            font-family: "JetBrains Mono", monospace;
            font-size: 0.8rem;
            padding: 0.3rem 0.6rem;
            cursor: pointer;
            transition: all 0.2s;

            &:hover {
                background: rgba(255, 255, 255, 0.1);
                color: white;
            }
        }
    }

    .content {
        padding: 1.5rem;
        display: flex;
        flex-direction: column;
        gap: 0;
    }

    .drop-zone {
        padding: 2rem 1.5rem;
        border: 2px dashed rgba(184, 115, 51, 0.4);
        border-radius: 8px;
        background: rgba(184, 115, 51, 0.04);
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.6rem;
        cursor: pointer;
        transition: all 0.2s;

        &:hover,
        &.drag-over {
            border-color: #b87333;
            background: rgba(184, 115, 51, 0.1);
        }

        &.uploading {
            pointer-events: none;
            opacity: 0.7;
        }

        .icon {
            font-family: "JetBrains Mono", monospace;
            font-size: 1.8rem;
            color: #b87333;
        }

        .text {
            font-family: "JetBrains Mono", monospace;
            font-size: 0.85rem;
            font-weight: 600;
            color: white;
            letter-spacing: 0.1em;
        }

        .subtext {
            font-family: "JetBrains Mono", monospace;
            font-size: 0.7rem;
            color: rgba(255, 255, 255, 0.4);
        }
    }

    .divider {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 1rem 0;

        &::before,
        &::after {
            content: "";
            flex: 1;
            height: 1px;
            background: rgba(255, 255, 255, 0.08);
        }

        span {
            font-family: "JetBrains Mono", monospace;
            font-size: 0.65rem;
            color: rgba(255, 255, 255, 0.3);
            letter-spacing: 0.15em;
        }
    }

    .scratch-btn {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 1rem 1.25rem;
        background: rgba(255, 255, 255, 0.03);
        border: 1px solid rgba(255, 255, 255, 0.1);
        border-radius: 8px;
        cursor: pointer;
        transition: all 0.2s;
        text-align: left;
        width: 100%;

        &:hover:not(:disabled) {
            border-color: rgba(184, 115, 51, 0.5);
            background: rgba(184, 115, 51, 0.07);

            .scratch-icon {
                color: #b87333;
                border-color: rgba(184, 115, 51, 0.5);
            }

            .scratch-title {
                color: white;
            }
        }

        &:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
    }

    .scratch-icon {
        font-family: "Press Start 2P", monospace;
        font-size: 0.9rem;
        color: rgba(255, 255, 255, 0.4);
        border: 1px solid rgba(255, 255, 255, 0.15);
        border-radius: 4px;
        padding: 0.5rem 0.6rem;
        flex-shrink: 0;
        transition: all 0.2s;
    }

    .scratch-text {
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
    }

    .scratch-title {
        font-family: "JetBrains Mono", monospace;
        font-size: 0.8rem;
        font-weight: 600;
        color: rgba(255, 255, 255, 0.8);
        letter-spacing: 0.08em;
        transition: color 0.2s;
    }

    .scratch-sub {
        font-family: "JetBrains Mono", monospace;
        font-size: 0.65rem;
        color: rgba(255, 255, 255, 0.35);
    }

    .name-input-wrap {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        padding: 1rem 1.25rem;
        background: rgba(184, 115, 51, 0.05);
        border: 1px solid rgba(184, 115, 51, 0.3);
        border-radius: 8px;
    }

    .name-input-label {
        font-family: 'JetBrains Mono', monospace;
        font-size: 0.65rem;
        color: rgba(255, 255, 255, 0.4);
        letter-spacing: 0.1em;
    }

    .name-input {
        background: rgba(0, 0, 0, 0.3);
        border: 1px solid rgba(184, 115, 51, 0.4);
        border-radius: 4px;
        padding: 0.5rem 0.75rem;
        font-family: 'JetBrains Mono', monospace;
        font-size: 0.85rem;
        color: white;
        outline: none;
        transition: border-color 0.2s;

        &:focus {
            border-color: #B87333;
        }

        &::placeholder {
            color: rgba(255, 255, 255, 0.2);
        }
    }

    .name-input-hint {
        font-family: 'JetBrains Mono', monospace;
        font-size: 0.6rem;
        color: rgba(255, 255, 255, 0.2);
    }

    .name-input-actions {
        display: flex;
        gap: 0.5rem;
        margin-top: 0.25rem;
    }

    .name-btn {
        flex: 1;
        padding: 0.45rem 0;
        font-family: 'JetBrains Mono', monospace;
        font-size: 0.7rem;
        font-weight: 600;
        letter-spacing: 0.08em;
        border-radius: 4px;
        cursor: pointer;
        transition: all 0.2s;

        &.cancel {
            background: transparent;
            border: 1px solid rgba(255, 255, 255, 0.15);
            color: rgba(255, 255, 255, 0.5);

            &:hover {
                border-color: rgba(255, 255, 255, 0.3);
                color: white;
            }
        }

        &.confirm {
            background: rgba(184, 115, 51, 0.15);
            border: 1px solid rgba(184, 115, 51, 0.5);
            color: #B87333;

            &:hover:not(:disabled) {
                background: rgba(184, 115, 51, 0.25);
                border-color: #B87333;
            }

            &:disabled {
                opacity: 0.35;
                cursor: not-allowed;
            }
        }
    }
</style>
