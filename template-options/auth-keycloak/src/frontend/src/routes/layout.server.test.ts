/**
 * Tests for the root layout server load - the entry point for all routes.
 */
import { describe, expect, it, vi } from 'vitest';
import { MOCK_USER, createMockLoadEvent } from '../test-utils';

/** Mock getUser - we test its behavior separately in auth.test.ts. */
const mockGetUser = vi.fn();
vi.mock('$lib/auth', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/auth')>();
	return {
		...actual,
		getUser: (...args: unknown[]) => mockGetUser(...args)
	};
});

// Import AFTER mocks are set up
const { load } = await import('./+layout.server');

type LoadEvent = Parameters<typeof load>[0];

/** Narrows away the `void` branch of the load return type. */
async function loadResult(event: LoadEvent) {
	const result = await load(event);
	if (!result) throw new Error('Expected load to return data');
	return result;
}

describe('root layout server load', () => {
	it('authenticated user - returns user data and locale', async () => {
		mockGetUser.mockResolvedValueOnce({ user: MOCK_USER, error: null });

		const result = await loadResult(createMockLoadEvent() as LoadEvent);

		expect(result).toEqual({ user: MOCK_USER, backendError: null, locale: 'en' });
	});

	it('not authenticated - returns null user without error', async () => {
		mockGetUser.mockResolvedValueOnce({ user: null, error: null });

		const result = await loadResult(createMockLoadEvent() as LoadEvent);

		expect(result.user).toBeNull();
		expect(result.backendError).toBeNull();
	});

	it('backend unavailable - returns the error', async () => {
		mockGetUser.mockResolvedValueOnce({ user: null, error: 'backend_unavailable' });

		const result = await loadResult(createMockLoadEvent() as LoadEvent);

		expect(result.backendError).toBe('backend_unavailable');
	});
});
