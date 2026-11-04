import type { RequestHandler } from './$types';
// @init:if auth=jwt,keycloak
import type { paths } from '$lib/api/v1';
// @init:end
import { SERVER_CONFIG } from '$lib/config/server';
import { isFetchErrorWithCode } from '$lib/api';

// @init:if auth=jwt,keycloak
/**
 * Strips the `/api/` prefix from a generated API path to produce the proxy
 * route parameter.  The `satisfies` constraint ensures a compile error if
 * the path is renamed or removed from the OpenAPI spec.
 */
function proxyPath<P extends keyof paths>(path: P): string {
	return (path as string).replace(/^\/api\//, '');
}

/**
 * Auth endpoint paths that receive the `?useCookies=true` query parameter.
 * These are the only endpoints where the backend needs to set/read HttpOnly
 * cookies - all other endpoints rely on the cookie being forwarded
 * automatically by the browser.
 *
 * Derived from the generated `paths` type so route renames cause compile errors.
 */
// @init:if auth=jwt
const COOKIE_AUTH_PATHS = [
	proxyPath('/api/auth/login'),
	proxyPath('/api/auth/refresh'),
	proxyPath('/api/auth/two-factor/login'),
	proxyPath('/api/auth/two-factor/login/recovery'),
	proxyPath('/api/auth/external/callback')
];
// @init:else uncomment
// const COOKIE_AUTH_PATHS = [proxyPath('/api/auth/refresh')];
// @init:end
// @init:end

/** HTTP methods that can mutate state and are vulnerable to CSRF */
const UNSAFE_METHODS = ['POST', 'PUT', 'PATCH', 'DELETE'];

/**
 * Request headers forwarded to the backend.
 * Only headers the API needs are proxied - everything else is stripped
 * to avoid leaking browser metadata to internal services.
 */
const FORWARDED_REQUEST_HEADERS = [
	'accept',
	'accept-language',
	'authorization',
	'content-type',
	'content-length',
	'cookie',
	'if-match',
	'if-none-match',
	'if-modified-since'
];

/**
 * Response headers stripped before sending to the browser.
 * Prevents leaking internal infrastructure details and removes
 * hop-by-hop headers that are meaningless across the proxy boundary.
 */
const STRIPPED_RESPONSE_HEADERS = [
	'connection',
	'keep-alive',
	'transfer-encoding',
	'server',
	'x-powered-by'
];

/**
 * Validates that the request Origin header matches the app's own origin
 * or an explicitly configured allowed origin (ALLOWED_ORIGINS env var).
 * This prevents cross-site request forgery for cookie-authenticated requests
 * proxied through SvelteKit (SameSite=None cookies are sent cross-origin).
 *
 * Only enforced for state-changing methods - GET/HEAD/OPTIONS pass through.
 * Does not affect mobile or API-key clients since they call the backend directly.
 */
function isOriginAllowed(request: Request, url: URL): boolean {
	if (!UNSAFE_METHODS.includes(request.method)) {
		return true;
	}

	const origin = request.headers.get('origin');

	// Browsers always send Origin on cross-origin requests and on same-origin
	// POST/PUT/PATCH/DELETE. A missing Origin on an unsafe method means either
	// a same-origin request from an older browser or a non-browser client -
	// both are safe to allow through.
	if (!origin) {
		return true;
	}

	if (origin === url.origin) {
		return true;
	}

	return SERVER_CONFIG.ALLOWED_ORIGINS.includes(origin);
}

/** Build a filtered copy of request headers using the allowlist. */
function filterRequestHeaders(source: Headers, clientAddress: string): Headers {
	const filtered = new Headers();
	for (const name of FORWARDED_REQUEST_HEADERS) {
		const value = source.get(name);
		if (value) {
			filtered.set(name, value);
		}
	}

	// Pass the real client IP so the backend can partition rate limits per user.
	// SvelteKit resolves this from the connecting socket by default. When behind
	// a reverse proxy (nginx, Caddy), set the XFF_DEPTH env var to the number of
	// trusted proxy hops so getClientAddress() reads the real IP from X-Forwarded-For.
	// See: https://svelte.dev/docs/kit/adapter-node#environment-variables-xff-depth
	filtered.set('x-forwarded-for', clientAddress);

	const proto = source.get('x-forwarded-proto');
	if (proto) {
		filtered.set('x-forwarded-proto', proto);
	}

	return filtered;
}

/**
 * Copies the backend response headers, minus the stripped ones.
 *
 * `Set-Cookie` is copied value by value via `getSetCookie()`, so a response that
 * sets several cookies (e.g. access and refresh token) keeps every one of them
 * instead of a single comma-joined value. `Location` passes through untouched,
 * so backend redirects reach the browser as-is.
 */
function stripResponseHeaders(response: Response): Response {
	const headers = new Headers();
	for (const [name, value] of response.headers) {
		if (name === 'set-cookie' || STRIPPED_RESPONSE_HEADERS.includes(name)) {
			continue;
		}
		headers.set(name, value);
	}
	for (const cookie of response.headers.getSetCookie()) {
		headers.append('set-cookie', cookie);
	}
	return new Response(response.body, {
		status: response.status,
		statusText: response.statusText,
		headers
	});
}

export const fallback: RequestHandler = async ({
	request,
	params,
	url,
	fetch,
	getClientAddress
}) => {
	if (!isOriginAllowed(request, url)) {
		return new Response(
			JSON.stringify({
				type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4',
				title: 'Forbidden',
				status: 403,
				detail: 'Cross-origin requests are not allowed'
			}),
			{
				status: 403,
				headers: { 'Content-Type': 'application/problem+json' }
			}
		);
	}

	// Build target URL with query string
	const targetParams = new URLSearchParams(url.search);

	// @init:if auth=jwt,keycloak
	// Web clients need cookies for auth endpoints
	if (COOKIE_AUTH_PATHS.includes(params.path)) {
		targetParams.set('useCookies', 'true');
	}
	// @init:end

	const queryString = targetParams.toString();
	const targetUrl = `${SERVER_CONFIG.API_URL}/api/${params.path}${queryString ? `?${queryString}` : ''}`;

	const newRequest = new Request(targetUrl, {
		method: request.method,
		headers: filterRequestHeaders(request.headers, getClientAddress()),
		body: request.body,
		// Never follow backend redirects here. A followed redirect would drop the 3xx
		// status, its Location and its Set-Cookie headers (e.g. a sign-in flow that sets
		// a state cookie and redirects to an identity provider). The browser follows it.
		redirect: 'manual',
		// @ts-expect-error - duplex is needed for streaming bodies in some node versions/fetch implementations
		duplex: 'half'
	});

	try {
		const response = await fetch(newRequest);
		return stripResponseHeaders(response);
	} catch (err) {
		console.error('Proxy error:', err);

		if (isFetchErrorWithCode(err, 'ECONNREFUSED')) {
			return new Response(
				JSON.stringify({
					type: 'https://tools.ietf.org/html/rfc9110#section-15.6.4',
					title: 'Service Unavailable',
					status: 503,
					detail: 'Backend unavailable'
				}),
				{
					status: 503,
					headers: { 'Content-Type': 'application/problem+json' }
				}
			);
		}

		return new Response(
			JSON.stringify({
				type: 'https://tools.ietf.org/html/rfc9110#section-15.6.3',
				title: 'Bad Gateway',
				status: 502,
				detail: 'An unexpected error occurred while proxying the request'
			}),
			{
				status: 502,
				headers: { 'Content-Type': 'application/problem+json' }
			}
		);
	}
};
