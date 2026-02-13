// Universal API proxy route - catches all /api/* requests
const BACKEND = 'http://localhost:27272';

async function proxyRequest(event) {
  const { request, url } = event;

  try {
    const fetchOptions = {
      method: request.method,
      headers: {}
    };

    // Copy relevant headers
    const contentType = request.headers.get('Content-Type');
    if (contentType) {
      fetchOptions.headers['Content-Type'] = contentType;
    }

    // Add body for non-GET/HEAD requests
    if (request.method !== 'GET' && request.method !== 'HEAD') {
      fetchOptions.body = await request.text();
    }

    const backendUrl = `${BACKEND}${url.pathname}${url.search}`;
    console.log('[API Proxy]', request.method, backendUrl);

    const res = await fetch(backendUrl, fetchOptions);
    const data = await res.text();

    return new Response(data, {
      status: res.status,
      headers: {
        'Content-Type': res.headers.get('Content-Type') || 'application/json',
        'Access-Control-Allow-Origin': '*'
      }
    });
  } catch (e) {
    console.error('[API Proxy] Error:', e.message);
    return new Response(JSON.stringify({ error: 'Backend not available', details: e.message }), {
      status: 503,
      headers: { 'Content-Type': 'application/json' }
    });
  }
}

/** @type {import('./$types').RequestHandler} */
export const GET = proxyRequest;

/** @type {import('./$types').RequestHandler} */
export const POST = proxyRequest;

/** @type {import('./$types').RequestHandler} */
export const PUT = proxyRequest;

/** @type {import('./$types').RequestHandler} */
export const DELETE = proxyRequest;

/** @type {import('./$types').RequestHandler} */
export const OPTIONS = async () => {
  return new Response(null, {
    status: 204,
    headers: {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type'
    }
  });
};
