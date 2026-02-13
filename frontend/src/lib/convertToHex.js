export function rgbToHex(palette) {
  if (!palette) {
    //console.warn('[Vibrant] No palette received');
    return;
  }

  // Helper to safely set color variable
  const setColor = (varName, swatch) => {
    if (swatch && swatch.rgb) {
      // Round RGB values for browser compatibility
      const r = Math.round(swatch.rgb[0]);
      const g = Math.round(swatch.rgb[1]);
      const b = Math.round(swatch.rgb[2]);
      const rgb = `rgb(${r},${g},${b})`;
      document.documentElement.style.setProperty(varName, rgb);
      //console.log(`[Vibrant] Set ${varName} = ${rgb}`);
    }
  };

  setColor('--darkMuted', palette.DarkMuted);
  setColor('--vibrant', palette.Vibrant);
  setColor('--lightVibrant', palette.LightVibrant);
  setColor('--muted', palette.Muted);
  setColor('--darkVibrant', palette.DarkVibrant);
  setColor('--lightMuted', palette.LightMuted);

  // Debug: verify the values are actually set
  const computed = getComputedStyle(document.documentElement);
  //console.log('[Vibrant] Verification - computed --vibrant:', computed.getPropertyValue('--vibrant'));
  //console.log('[Vibrant] Colors applied to :root');
}
    //I want to make variable for thumbnail, but it doesn't work(
	// $: if (typeof document !== 'undefined') {
    //     console.log(thumbnail)
	// 	document.documentElement.style.setProperty('--thumbnail', `url(${thumbnail})`);
	// }
