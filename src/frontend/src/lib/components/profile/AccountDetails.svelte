<script lang="ts">
	import * as Card from '$lib/components/ui/card';
	// @init:if auth=jwt
	import { Badge } from '$lib/components/ui/badge';
	import { User as UserIcon, Shield } from '@lucide/svelte';
	// @init:else uncomment
	// import { User as UserIcon, AtSign } from '@lucide/svelte';
	// @init:end
	import { InfoItem } from '$lib/components/profile';
	import type { User } from '$lib/types';
	import * as m from '$lib/paraglide/messages';

	interface Props {
		user: User | null | undefined;
	}

	let { user }: Props = $props();
</script>

<Card.Root class="card-hover">
	<Card.Header>
		<Card.Title>{m.profile_account_title()}</Card.Title>
		<Card.Description>{m.profile_account_description()}</Card.Description>
	</Card.Header>
	<Card.Content class="space-y-6">
		<InfoItem icon={UserIcon} label={m.profile_account_userId()}>
			{user?.id}
		</InfoItem>

		<!-- @init:if auth=jwt -->
		<InfoItem icon={Shield} label={m.profile_account_roles()}>
			<div class="mt-1 flex flex-wrap gap-2">
				{#each user?.roles || [] as role (role)}
					<Badge variant="secondary">{role}</Badge>
				{:else}
					<span>{m.profile_account_noRoles()}</span>
				{/each}
			</div>
		</InfoItem>
		<!-- @init:else uncomment -->
		<!-- <InfoItem icon={AtSign} label={m.profile_account_username()}> -->
		<!-- 	{user?.username} -->
		<!-- </InfoItem> -->
		<!-- @init:end -->
	</Card.Content>
</Card.Root>
