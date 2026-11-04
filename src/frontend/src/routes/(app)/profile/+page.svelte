<script lang="ts">
	import { PageHeader } from '$lib/components/common';
	// @init:if auth=jwt
	import { ProfileForm, AccountDetails } from '$lib/components/profile';
	// @init:else uncomment
	// import { PersonalInfoCard, AccountDetails } from '$lib/components/profile';
	// @init:end
	import type { PageData } from './$types';
	import * as m from '$lib/paraglide/messages';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>{m.meta_titleTemplate({ title: m.meta_profile_title() })}</title>
	<meta name="description" content={m.meta_profile_description()} />
</svelte:head>

<div class="space-y-6">
	<PageHeader title={m.profile_title()} description={m.profile_description()} />
	<div class="grid gap-6 lg:grid-cols-2">
		<!-- @init:if auth=jwt -->
		<ProfileForm user={data.user} />
		<!-- @init:else uncomment -->
		<!-- <PersonalInfoCard user={data.user} /> -->
		<!-- @init:end -->
		<AccountDetails user={data.user} />
	</div>
</div>
