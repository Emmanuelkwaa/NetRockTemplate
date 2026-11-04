<script lang="ts">
	import '../styles/index.css';
	import 'flag-icons/css/flag-icons.min.css';
	import { onMount } from 'svelte';
	import { initTheme } from '$lib/state/theme.svelte';
	import * as m from '$lib/paraglide/messages';
	// @init:if auth=jwt,keycloak
	import { Toaster, toast } from '$lib/components/ui/sonner';
	// @init:else uncomment
	// import { Toaster } from '$lib/components/ui/sonner';
	// @init:end
	import * as Tooltip from '$lib/components/ui/tooltip';
	import { globalShortcuts } from '$lib/state/shortcuts.svelte';
	// @init:if auth=jwt,keycloak
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { logout, createAuthMiddleware } from '$lib/auth';
	import { initBrowserAuth } from '$lib/api';
	// @init:end
	import { initBackendMonitor } from '$lib/api/backend-monitor';
	import { ShortcutsHelp } from '$lib/components/layout';
	import { initHealthCheck } from '$lib/state';

	let { children } = $props();

	onMount(() => {
		initBackendMonitor();
		// @init:if auth=jwt,keycloak
		initBrowserAuth(
			createAuthMiddleware(fetch, '', async () => {
				toast.error(m.auth_sessionExpired_title(), {
					description: m.auth_sessionExpired_description()
				});
				await invalidateAll();
				await goto(resolve('/login'));
			})
		);
		// @init:end
		const cleanupTheme = initTheme();
		const cleanupHealth = initHealthCheck();
		return () => {
			cleanupTheme?.();
			cleanupHealth?.();
		};
	});
	// @init:if auth=jwt

	async function handleSettings() {
		await goto(resolve('/settings'));
	}
	// @init:end
</script>

<!-- @init:if auth=jwt -->
<svelte:window
	use:globalShortcuts={{
		settings: handleSettings,
		logout: logout
	}}
/>
<!-- @init:else -->
<!-- @init:if auth=keycloak uncomment -->
<!-- <svelte:window use:globalShortcuts={{ logout: logout }} /> -->
<!-- @init:else uncomment -->
<!-- <svelte:window use:globalShortcuts /> -->
<!-- @init:end -->
<!-- @init:end -->

<ShortcutsHelp />

<svelte:head>
	<title>{m.app_name()}</title>
	<meta name="description" content={m.meta_description()} />
</svelte:head>

<Tooltip.Provider>
	<Toaster />
	{@render children()}
</Tooltip.Provider>
