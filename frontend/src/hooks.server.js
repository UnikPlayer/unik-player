/** @type {import('@sveltejs/kit').Handle} */
export async function handle({ event, resolve }) {
  // API routes are handled by src/routes/api/[...path]/+server.js
  // This hook only handles non-API requests
  return resolve(event);
}
