// src/lib/marquee.js

// Action
export function marquee(node, opts = {}) {
  let destroy = initMarquee(node, opts) || (() => {});
  return {
    update(newOpts) {
      destroy();
      destroy = initMarquee(node, newOpts) || (() => {});
    },
    destroy() {
      destroy();
    },
  };
}

// Init
export function initMarquee(el, opts = {}) {
  if (!el) return () => {};
  const { speed = 60, gap: optGap = 50, debug = false } = opts;
  const gap = typeof optGap === "number" ? optGap : 20;

  // --- Если нет .marquee__content — создаём и переносим внутрь все дочерние узлы ---
  let createdSrc = false;
  let src = el.querySelector(".marquee__content");
  if (!src) {
    createdSrc = true;
    src = document.createElement("span");
    src.className = "marquee__content";
    // Переносим все дочерние узлы в span (сохраняет структуру)
    while (el.firstChild) src.appendChild(el.firstChild);
    el.appendChild(src);
  }
  if (!src) return () => {};

  let wrap = null;
  let a = null;
  let spacer = null;
  let b = null;

  // state
  let visible = 0;
  let single = 0;
  let spacerWidth = 0;
  let cycle = 0;
  let x = 0;
  let raf = null;
  let lastTs = 0;
  let running = false;
  let paused = false;

  function createWrapIfNeeded() {
    if (wrap) return;
    wrap = document.createElement("div");
    wrap.className = "marquee__wrap";
    Object.assign(wrap.style, {
      position: "absolute",
      top: "0",
      left: "0",
      height: "100%",
      display: "inline-flex",
      alignItems: "center",
      whiteSpace: "nowrap",
      transform: "translateX(0px)",
      willChange: "transform",
      pointerEvents: "none",
      color: "inherit", // наследуем цвет от родителя (h2)
    });

    // клонируем src, чтобы сохранить HTML и class/inline-styles
    a = src.cloneNode(true);
    a.classList.add("marquee__item");

    spacer = document.createElement("span");
    spacer.className = "marquee__spacer";
    Object.assign(spacer.style, {
      display: "inline-block",
      width: `${gap}px`,
      minWidth: `${gap}px`,
      flex: "0 0 auto",
      boxSizing: "content-box",
    });

    b = a.cloneNode(true);
    b.classList.add("marquee__item");

    wrap.appendChild(a);
    wrap.appendChild(spacer);
    wrap.appendChild(b);
  }

  function mountWrap() {
    createWrapIfNeeded();
    if (!wrap.parentNode) {
      // скрываем оригинал визуально, но оставляем его в DOM для доступности
      src.style.visibility = "hidden";
      // нужен position: relative на el для абсолютного позиционирования wrap
      const pos = window.getComputedStyle(el).position;
      if (pos !== "relative" && pos !== "absolute" && pos !== "fixed") {
        el.style.position = "relative";
      }
      el.appendChild(wrap);
    }
  }

  function unmountWrap() {
    if (wrap && wrap.parentNode) wrap.parentNode.removeChild(wrap);
    if (src) src.style.visibility = "";
  }

  function measure() {
    if (spacer) {
      spacer.style.width = `${gap}px`;
      spacer.style.minWidth = `${gap}px`;
    }

    visible = Math.round(el.clientWidth || 0);

    if (!wrap) {
      const r = src.getBoundingClientRect();
      single = Math.ceil(r.width || 0);
      spacerWidth = gap;
    } else {
      wrap.getBoundingClientRect(); // force layout
      const ra = a.getBoundingClientRect();
      single = Math.ceil(ra.width || 0);
      spacerWidth = spacer
        ? Math.ceil(spacer.getBoundingClientRect().width || 0)
        : gap;
    }

    cycle = single + spacerWidth;

    if (debug) {
      try {
        window.__marquee_last = { visible, single, spacerWidth, cycle, gap };
      } catch (e) {}
    }

    if (single <= visible) {
      unmountWrap();
      stop();
      return false;
    }

    mountWrap();
    x = 0;
    if (wrap) wrap.style.transform = `translateX(${x}px)`;
    return true;
  }

  function step(ts) {
    if (!lastTs) lastTs = ts;
    const dt = (ts - lastTs) / 1000;
    lastTs = ts;

    if (!paused && cycle > 0) {
      x -= speed * dt;
      if (x <= -cycle) x += cycle;
      if (wrap) wrap.style.transform = `translateX(${Math.round(x)}px)`;
    }

    raf = requestAnimationFrame(step);
  }

  function start() {
    if (running) return;
    running = true;
    lastTs = 0;
    if (raf) cancelAnimationFrame(raf);
    raf = requestAnimationFrame(step);
  }

  function stop() {
    running = false;
    if (raf) cancelAnimationFrame(raf);
    raf = null;
    lastTs = 0;
    if (wrap) wrap.style.transform = "translateX(0px)";
  }

  // hover pause
  const onEnter = () => (paused = true);
  const onLeave = () => {
    paused = false;
    lastTs = 0;
  };
  el.addEventListener("mouseenter", onEnter);
  el.addEventListener("mouseleave", onLeave);

  // scheduleMeasure
  let measureQueued = false;
  function scheduleMeasure(doubleRAF = false) {
    if (measureQueued) return;
    measureQueued = true;
    if (doubleRAF) {
      requestAnimationFrame(() =>
        requestAnimationFrame(() => {
          measureQueued = false;
          if (measure()) start();
          else stop();
        }),
      );
    } else {
      requestAnimationFrame(() => {
        measureQueued = false;
        if (measure()) start();
        else stop();
      });
    }
  }

  // ResizeObserver
  const ro =
    typeof ResizeObserver !== "undefined"
      ? new ResizeObserver(() => scheduleMeasure())
      : null;
  if (ro) {
    ro.observe(el);
    ro.observe(src);
  }

  // MutationObserver — обновляем клоны и реизмеряем при смене текста
  const mo = new MutationObserver(() => {
    const newHTML = src.innerHTML;
    if (a && b) {
      a.innerHTML = newHTML;
      b.innerHTML = newHTML;
    }
    // сохранить наследование цвета (если поменялось)
    if (wrap) wrap.style.color = "inherit";
    scheduleMeasure();
  });
  mo.observe(src, { childList: true, characterData: true, subtree: true });

  // картинки
  const imgs = Array.from(el.querySelectorAll("img"));
  imgs.forEach((img) => {
    if (!img.complete) img.addEventListener("load", () => scheduleMeasure());
  });

  // initial
  scheduleMeasure(true);

  return function destroy() {
    stop();
    if (ro) ro.disconnect();
    mo.disconnect();
    el.removeEventListener("mouseenter", onEnter);
    el.removeEventListener("mouseleave", onLeave);
    unmountWrap();

    // если мы создали src — вернём содержимое обратно и удалим span
    if (createdSrc && src && src.parentNode === el) {
      while (src.firstChild) el.appendChild(src.firstChild);
      el.removeChild(src);
    }

    wrap = a = b = spacer = null;
    try {
      if (debug) delete window.__marquee_last;
    } catch (e) {}
  };
}
