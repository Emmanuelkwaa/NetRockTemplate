/**
 * Tests for the (app) layout server guard - the auth boundary for protected routes.
 */
import { describe, expect, it, vi } from 'vitest';
import { isHttpError, isRedirect } from '@sveltejs/kit';
import { load } from './+layout.server';
import { MOCK_USER, createMockLoadEvent } from '../../test-utils';

type LoadEvent = Parameters<typeof load>[0];

/** Builds a complete mock SvelteKit load event for the (app) layout. */
function mockLoadEvent(
	overrides: {
		user?: typeof MOCK_USER | null;
		backendError?: 'backend_unavailable' | null;
		url?: string;
	} = {}
) {
	const { user = null, backendError = null, url = 'http://localhost/' } = overrides;

	return createMockLoadEvent({
		route: { id: '/(app)' },
		parent: vi.fn().mockResolvedValue({ user, backendError }),
		url: new URL(url)
	}) as LoadEvent;
}

/** Asserts that a load function throws a SvelteKit redirect. */
async function expectRedirect(fn: () => ReturnType<typeof load>, status: number, location: string) {
	try {
		await fn();
		expect.fail('Expected redirect to be thrown');
	} catch (e) {
		expect(isRedirect(e)).toBe(true);
		if (isRedirect(e)) {
			expect(e.status).toBe(status);
			expect(e.location).toBe(location);
		}
	}
}

describe('(app) layout server load', () => {
	it('authenticated user - returns user data with sidebar state', async () => {
		const result = await load(mockLoadEvent({ user: MOCK_USER }));
		expect(result).toEqual({ user: MOCK_USER, sidebarOpen: true });
	});

	it('backend unavailable - throws 503 error', async () => {
		try {
			await load(mockLoadEvent({ backendError: 'backend_unavailable' }));
			expect.fail('Expected error to be thrown');
		} catch (e) {
			expect(isHttpError(e, 503)).toBe(true);
			expect(isRedirect(e)).toBe(false);
		}
	});

	it('no user on / - redirects to /login with returnUrl /', async () => {
		await expectRedirect(() => load(mockLoadEvent()), 303, '/login?returnUrl=%2F');
	});

	it('no user on a nested page - keeps path and query in returnUrl', async () => {
		await expectRedirect(
			() => load(mockLoadEvent({ url: 'http://localhost/profile?tab=1' })),
			303,
			'/login?returnUrl=%2Fprofile%3Ftab%3D1'
		);
	});
});
