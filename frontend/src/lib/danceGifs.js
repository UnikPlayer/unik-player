// dance-gifs backend helpers
export async function fetchGifs() {
  const res = await fetch('/api/dance-gifs');
  if (!res.ok) return [];
  const data = await res.json().catch(() => ({}));
  return Array.isArray(data.gifs) ? data.gifs : [];
}

export async function uploadGif(file) {
  const url = '/api/dance-gifs?file=' + encodeURIComponent(file.name);
  const res = await fetch(url, { method: 'POST', body: file });
  const data = await res.json();
  if (!data.success) throw new Error(data.error || 'upload failed');
  return data; // { name, ext }
}

export async function deleteGif(name) {
  await fetch('/api/dance-gifs/' + encodeURIComponent(name), { method: 'DELETE' });
}

export function gifUrl(name) {
  return '/api/dance-gifs/' + encodeURIComponent(name);
}

export function obSLink(name) {
  return `${window.location.origin}/dancesync/player?gif=${encodeURIComponent(name)}`;
}

export function nameNoExt(name) {
  return name.replace(/\.[^.]+$/, '');
}
