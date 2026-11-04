/**
 * Tests for the (app) layout server load. The app has no authentication, so
 * the layout only restores the sidebar state.
 */
import { describe, expect, it } from 'vitest';
import { load } from './+layout.server';
import { createMockCookies, createMockLoadEvent } from '../../test-utils';

type LoadEvent = Parameters<typeof load>[0];

/** Builds a mock load event whose sidebar cookie holds `value`. */
function mockLoadEvent(value?: string) {
	return createMockLoadEvent({
		route: { id: '/(app)' },
		cookies: createMockCookies((name: string) => (name === 'sidebar:state' ? value : undefined))
	}) as LoadEvent;
}

describe('(app) layout server load', () => {
	it('no sidebar cookie - sidebar open', async () => {
		expect(await load(mockLoadEvent())).toEqual({ sidebarOpen: true });
	});

	it('sidebar cookie false - sidebar closed', async () => {
		expect(await load(mockLoadEvent('false'))).toEqual({ sidebarOpen: false });
	});

	it('sidebar cookie true - sidebar open', async () => {
		expect(await load(mockLoadEvent('true'))).toEqual({ sidebarOpen: true });
	});
});
