/**
 * Tests for the root layout server load - the entry point for all routes.
 */
import { describe, expect, it } from 'vitest';
import { createMockLoadEvent } from '../test-utils';
import { load } from './+layout.server';

type LoadEvent = Parameters<typeof load>[0];

describe('root layout server load', () => {
	it('returns the request locale', async () => {
		const result = await load(createMockLoadEvent() as LoadEvent);

		expect(result).toEqual({ locale: 'en' });
	});
});
