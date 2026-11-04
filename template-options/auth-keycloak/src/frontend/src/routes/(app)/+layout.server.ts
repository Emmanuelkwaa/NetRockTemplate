import { error, redirect } from '@sveltejs/kit';
import * as m from '$lib/paraglide/messages';
import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ parent, cookies, url }) => {
	const { user, backendError } = await parent();

	if (backendError === 'backend_unavailable') {
		throw error(503, m.serverError_backendUnavailable());
	}

	if (!user) {
		// Sign-in happens on Keycloak; /login sends the user there and back here.
		const returnUrl = `${url.pathname}${url.search}`;
		throw redirect(303, `/login?${new URLSearchParams({ returnUrl })}`);
	}

	// Read sidebar cookie for SSR-correct initial state (avoids flash on page load).
	// The shadcn SidebarProvider persists state to this cookie on toggle.
	const sidebarOpen = cookies.get('sidebar:state') !== 'false';

	return { user, sidebarOpen };
};
