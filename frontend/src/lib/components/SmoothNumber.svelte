<script>
  // Smoothly tweens the displayed number from its previous value to `value`.
  export let value = 0;
  export let decimals = 0;
  export let duration = 1000;
  export let prefix = '';
  export let suffix = '';
  export let minDelta = 0; // tiny changes jump instantly (no jitter)

  const fmt = (v) => prefix + v.toFixed(decimals) + suffix;

  let cur = value == null ? 0 : value;          // numeric value currently shown
  let text = value == null ? '--' : fmt(value); // formatted output (string only)
  let raf = null;
  let prev = value;
  let rafStart = 0;
  let from = 0;
  let to = 0;

  function frame(t) {
    const p = Math.min(1, (t - rafStart) / duration);
    const eased = 1 - Math.pow(1 - p, 3);
    text = fmt(from + (to - from) * eased);
    if (p < 1) {
      raf = requestAnimationFrame(frame);
    } else {
      cur = to;
      text = fmt(to);
      raf = null;
    }
  }

  function animate() {
    if (raf) cancelAnimationFrame(raf);
    if (value == null) {
      cur = 0;
      text = '--';
      raf = null;
      return;
    }
    from = cur;
    to = value;
    if (from === to) {
      cur = to;
      text = fmt(to);
      raf = null;
      return;
    }
    if (Math.abs(to - from) <= minDelta) {
      cur = to;
      text = fmt(to);
      raf = null;
      return;
    }
    rafStart = performance.now();
    raf = requestAnimationFrame(frame);
  }

  $: if (prev !== value) {
    prev = value;
    animate();
  }
</script>

<span>{text}</span>
