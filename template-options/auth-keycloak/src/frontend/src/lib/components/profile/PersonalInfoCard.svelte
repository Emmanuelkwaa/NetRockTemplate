<script lang="ts">
	import * as Avatar from '$lib/components/ui/avatar';
	import * as Card from '$lib/components/ui/card';
	import { Input } from '$lib/components/ui/input';
	import { Label } from '$lib/components/ui/label';
	import type { User } from '$lib/types';
	import * as m from '$lib/paraglide/messages';

	interface Props {
		user: User | null | undefined;
	}

	let { user }: Props = $props();

	const displayName = $derived.by(() => {
		const first = user?.firstName;
		const last = user?.lastName;
		if (first || last) {
			return [first, last].filter(Boolean).join(' ');
		}
		return user?.username ?? m.common_user();
	});

	const initials = $derived.by(() => {
		const first = user?.firstName;
		const last = user?.lastName;
		if (first && last) {
			return `${first[0]}${last[0]}`.toUpperCase();
		}
		if (first) {
			return first.substring(0, 2).toUpperCase();
		}
		return user?.username?.substring(0, 2).toUpperCase() ?? 'ME';
	});
</script>

<Card.Root class="card-hover">
	<Card.Header>
		<Card.Title>{m.profile_personalInfo_title()}</Card.Title>
		<Card.Description>{m.profile_personalInfo_managedExternally()}</Card.Description>
	</Card.Header>
	<Card.Content class="space-y-6">
		<div class="flex flex-col items-center gap-4 sm:flex-row">
			<Avatar.Root class="h-24 w-24 ring-2 ring-border">
				<Avatar.Fallback class="text-lg">{initials}</Avatar.Fallback>
			</Avatar.Root>
			<div class="flex min-w-0 flex-col gap-1 text-center sm:text-start">
				<h3 class="truncate text-lg font-medium">{displayName}</h3>
				<p class="truncate text-sm text-muted-foreground">{user?.email ?? ''}</p>
			</div>
		</div>

		<div class="grid gap-4">
			<div class="grid gap-2">
				<Label for="email">{m.profile_personalInfo_email()}</Label>
				<Input id="email" type="email" value={user?.email ?? ''} readonly disabled />
			</div>
			<div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
				<div class="grid gap-2">
					<Label for="firstName">{m.profile_personalInfo_firstName()}</Label>
					<Input id="firstName" value={user?.firstName ?? ''} readonly disabled />
				</div>
				<div class="grid gap-2">
					<Label for="lastName">{m.profile_personalInfo_lastName()}</Label>
					<Input id="lastName" value={user?.lastName ?? ''} readonly disabled />
				</div>
			</div>
		</div>
	</Card.Content>
</Card.Root>
