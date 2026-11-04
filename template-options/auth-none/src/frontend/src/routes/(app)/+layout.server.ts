import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ cookies }) => {
	// Read sidebar cookie for SSR-correct initial state (avoids flash on page load).
	// The shadcn SidebarProvider persists state to this cookie on toggle.
	const sidebarOpen = cookies.get('sidebar:state') !== 'false';

	return { sidebarOpen };
};
