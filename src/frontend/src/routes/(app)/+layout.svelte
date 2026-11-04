<script lang="ts">
	import * as Sidebar from '$lib/components/ui/sidebar';
	import { AppSidebar, Header, ContentHeader, CommandPalette } from '$lib/components/layout';
	// @init:if auth=jwt
	import { EmailVerificationBanner } from '$lib/components/auth';
	// @init:end
	import { page } from '$app/state';
	import { invalidateAll } from '$app/navigation';
	import { healthState } from '$lib/state';

	let { children, data } = $props();

	// When health polling detects the backend went down, re-run server loads.
	// The (app) layout.server.ts will throw 503, showing the error page with
	// auto-recovery. Only trigger on a confirmed online->offline transition.
	let wasOnline = false;
	$effect(() => {
		if (healthState.checked && wasOnline && !healthState.online) {
			invalidateAll();
		}
		if (healthState.checked) wasOnline = healthState.online;
	});
</script>

<!-- @init:if auth=jwt -->
<CommandPalette user={data.user} />
<!-- @init:else uncomment -->
<!-- <CommandPalette /> -->
<!-- @init:end -->

<Sidebar.Provider open={data.sidebarOpen} class="h-dvh overflow-hidden">
	<!-- @init:if auth=jwt,keycloak -->
	<AppSidebar user={data.user} />
	<!-- @init:else uncomment -->
	<!-- <AppSidebar /> -->
	<!-- @init:end -->
	<Sidebar.Inset>
		<!-- @init:if auth=jwt,keycloak -->
		<Header user={data.user} />
		<!-- @init:else uncomment -->
		<!-- <Header /> -->
		<!-- @init:end -->
		<ContentHeader />
		<!-- @init:if auth=jwt -->
		{#if !data.user.emailConfirmed}
			<EmailVerificationBanner />
		{/if}
		<!-- @init:end -->
		<div
			class="flex flex-1 flex-col gap-4 overflow-y-auto overscroll-contain p-4 pb-[max(4rem,calc(env(safe-area-inset-bottom,0px)+2rem))] lg:gap-6 lg:p-6 lg:pb-[max(4rem,calc(env(safe-area-inset-bottom,0px)+2rem))]"
		>
			{#key page.url.pathname}
				<div
					class="mx-auto w-full max-w-7xl motion-safe:duration-300 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-4"
				>
					{@render children()}
				</div>
			{/key}
		</div>
	</Sidebar.Inset>
</Sidebar.Provider>
