import { error, redirect } from '@sveltejs/kit';
import type { paths } from '$lib/api/v1';
import * as m from '$lib/paraglide/messages';
import type { PageServerLoad } from './$types';

/** Hosted Keycloak sign-in on the API, tied to the generated OpenAPI spec. */
const KEYCLOAK_LOGIN_PATH = '/api/auth/keycloak/login' satisfies keyof paths;

/**
 * Returns `value` when it is a local path (`/foo`), otherwise `/`.
 * Rejects protocol-relative (`//host`) and backslash (`/\host`) forms that
 * browsers treat as another origin. The API validates the return URL again.
 */
function toLocalPath(value: string | null): string {
	if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
		return '/';
	}
	return value;
}

/**
 * Sign-in is hosted by Keycloak, so this page never renders. Signed-in users go
 * to the return URL; everyone else is sent through the API's Keycloak login.
 */
export const load: PageServerLoad = async ({ parent, url }) => {
	const { user, backendError } = await parent();

	if (backendError === 'backend_unavailable') {
		throw error(503, m.serverError_backendUnavailable());
	}

	const returnUrl = toLocalPath(url.searchParams.get('returnUrl'));

	if (user) {
		throw redirect(303, returnUrl);
	}

	throw redirect(303, `${KEYCLOAK_LOGIN_PATH}?${new URLSearchParams({ returnUrl })}`);
};
