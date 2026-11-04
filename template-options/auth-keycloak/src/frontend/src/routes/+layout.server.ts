import type { LayoutServerLoad } from './$types';
import { getUser } from '$lib/auth';

export const load: LayoutServerLoad = async ({ locals, fetch, url }) => {
	const { user, error: backendError } = await getUser(fetch, url.origin);
	return {
		user,
		backendError,
		locale: locals.locale
	};
};
