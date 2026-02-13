// Shared color manipulation utilities for UnikPlayer

function safeHex(hex) {
  if (!hex || typeof hex !== "string" || !/^#[0-9a-fA-F]{6}$/.test(hex))
    return "#B87333";
  return hex;
}

export function lightenColor(hex, percent) {
  hex = safeHex(hex);
  const num = parseInt(hex.slice(1), 16);
  const r = Math.min(255, (num >> 16) + Math.round((255 * percent) / 100));
  const g = Math.min(
    255,
    ((num >> 8) & 0x00ff) + Math.round((255 * percent) / 100),
  );
  const b = Math.min(255, (num & 0x0000ff) + Math.round((255 * percent) / 100));
  return `#${((1 << 24) | (r << 16) | (g << 8) | b).toString(16).slice(1)}`;
}

export function darkenColor(hex, percent) {
  hex = safeHex(hex);
  const num = parseInt(hex.slice(1), 16);
  const r = Math.max(0, (num >> 16) - Math.round((255 * percent) / 100));
  const g = Math.max(
    0,
    ((num >> 8) & 0x00ff) - Math.round((255 * percent) / 100),
  );
  const b = Math.max(0, (num & 0x0000ff) - Math.round((255 * percent) / 100));
  return `#${((1 << 24) | (r << 16) | (g << 8) | b).toString(16).slice(1)}`;
}

export function desaturateColor(hex, percent) {
  hex = safeHex(hex);
  const num = parseInt(hex.slice(1), 16);
  const r = num >> 16;
  const g = (num >> 8) & 0x00ff;
  const b = num & 0x0000ff;
  const gray = (r + g + b) / 3;
  const nr = Math.round(r + ((gray - r) * percent) / 100);
  const ng = Math.round(g + ((gray - g) * percent) / 100);
  const nb = Math.round(b + ((gray - b) * percent) / 100);
  return `#${((1 << 24) | (nr << 16) | (ng << 8) | nb).toString(16).slice(1)}`;
}

export function generateColorVars(hex) {
  const base = safeHex(hex);
  return {
    vibrant: base,
    lightVibrant: lightenColor(base, 30),
    darkVibrant: darkenColor(base, 30),
    muted: desaturateColor(base, 30),
    lightMuted: lightenColor(base, 20),
    darkMuted: darkenColor(base, 40),
  };
}
