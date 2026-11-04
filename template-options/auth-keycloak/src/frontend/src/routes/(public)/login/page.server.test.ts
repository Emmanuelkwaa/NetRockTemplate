import { describe, expect, it, vi } from 'vitest';
import { isHttpError, isRedirect } from '@sveltejs/kit';
import { load } from './+page.server';
import { MOCK_USER, createMockLoadEvent } from '../../../test-utils';

type LoadEvent = Parameters<typeof load>[0];

/** Builds a complete mock SvelteKit load event for the login page. */
function mockLoadEvent(
	overrides: {
		user?: typeof MOCK_USER | null;
		backendError?: 'backend_unavailable' | null;
		returnUrl?: string;
	} = {}
) {
	const { user = null, backendError = null, returnUrl } = overrides;
	const url = new URL('http://localhost/login');
	if (returnUrl !== undefined) {
		url.searchParams.set('returnUrl', returnUrl);
	}

	return createMockLoadEvent({
		route: { id: '/(public)/login' },
		parent: vi.fn().mockResolvedValue({ user, backendError }),
		url
	}) as LoadEvent;
}

/** Runs the load function and returns the location of the thrown redirect. */
async function redirectLocation(event: LoadEvent): Promise<string> {
	try {
		await load(event);
	} catch (e) {
		if (isRedirect(e)) {
			expect(e.status).toBe(303);
			return e.location;
		}
		throw e;
	}
	throw new Error('Expected redirect to be thrown');
}

describe('login page server load (Keycloak)', () => {
	it('anonymous user - redirects to the Keycloak login with returnUrl /', async () => {
		expect(await redirectLocation(mockLoadEvent())).toBe('/api/auth/keycloak/login?returnUrl=%2F');
	});

	it('anonymous user - preserves a local returnUrl', async () => {
		expect(await redirectLocation(mockLoadEvent({ returnUrl: '/profile?tab=1' }))).toBe(
			'/api/auth/keycloak/login?returnUrl=%2Fprofile%3Ftab%3D1'
		);
	});

	it.each(['https://evil.example.com', '//evil.example.com', '/\\evil.example.com', 'profile'])(
		'non-local returnUrl %s - falls back to /',
		async (returnUrl) => {
			expect(await redirectLocation(mockLoadEvent({ returnUrl }))).toBe(
				'/api/auth/keycloak/login?returnUrl=%2F'
			);
		}
	);

	it('authenticated user - redirects to the returnUrl', async () => {
		expect(await redirectLocation(mockLoadEvent({ user: MOCK_USER, returnUrl: '/profile' }))).toBe(
			'/profile'
		);
	});

	it('authenticated user without returnUrl - redirects to /', async () => {
		expect(await redirectLocation(mockLoadEvent({ user: MOCK_USER }))).toBe('/');
	});

	it('backend unavailable - throws 503', async () => {
		try {
			await load(mockLoadEvent({ backendError: 'backend_unavailable' }));
			expect.fail('Expected error to be thrown');
		} catch (e) {
			expect(isHttpError(e, 503)).toBe(true);
		}
	});
});
