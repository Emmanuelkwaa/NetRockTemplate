<script lang="ts">
	import * as Command from '$lib/components/ui/command';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	// @init:if auth=jwt
	import { hasPermission, Permissions } from '$lib/utils';
	// @init:end
	// @init:if auth=jwt,keycloak
	import { shortcutsState, ShortcutAction, getShortcutSymbol } from '$lib/state/shortcuts.svelte';
	// @init:else uncomment
	// import { shortcutsState } from '$lib/state/shortcuts.svelte';
	// @init:end
	import { toggleTheme } from '$lib/state/theme.svelte';
	// @init:if auth=jwt,keycloak
	import { logout } from '$lib/auth';
	// @init:end
	import * as m from '$lib/paraglide/messages';
	// @init:if auth=jwt
	import {
		LayoutDashboard,
		User,
		Settings,
		Users,
		Shield,
		Clock,
		KeyRound,
		Sun,
		LogOut
	} from '@lucide/svelte';
	// @init:else
	// @init:if auth=keycloak uncomment
	// import { LayoutDashboard, User, Sun, LogOut } from '@lucide/svelte';
	// @init:else uncomment
	// import { LayoutDashboard, Sun } from '@lucide/svelte';
	// @init:end
	// @init:end
	import type { Component } from 'svelte';
	import type { IconProps } from '@lucide/svelte';
	// @init:if auth=jwt
	import type { User as UserType } from '$lib/types';

	interface Props {
		user: UserType | null | undefined;
	}

	let { user }: Props = $props();
	// @init:end

	type CommandItem = {
		label: () => string;
		icon: Component<IconProps>;
		action: () => void;
		shortcut?: string;
	};

	// @init:if auth=jwt
	type AdminCommandItem = CommandItem & { permission: string };
	// @init:end

	function close() {
		shortcutsState.isCommandPaletteOpen = false;
	}

	const navItems: CommandItem[] = [
		{
			label: m.nav_dashboard,
			icon: LayoutDashboard,
			action: () => {
				close();
				goto(resolve('/'));
			}
			// @init:if auth=jwt,keycloak
		},
		{
			label: m.profile_title,
			icon: User,
			action: () => {
				close();
				goto(resolve('/profile'));
			}
			// @init:end
			// @init:if auth=jwt
		},
		{
			label: m.nav_settings,
			icon: Settings,
			action: () => {
				close();
				goto(resolve('/settings'));
			},
			shortcut: getShortcutSymbol(ShortcutAction.Settings)
			// @init:end
		}
	];

	// @init:if auth=jwt
	const adminItems: AdminCommandItem[] = [
		{
			label: m.nav_adminUsers,
			icon: Users,
			action: () => {
				close();
				goto(resolve('/admin/users'));
			},
			permission: Permissions.Users.View
		},
		{
			label: m.nav_adminRoles,
			icon: Shield,
			action: () => {
				close();
				goto(resolve('/admin/roles'));
			},
			permission: Permissions.Roles.View
		},
		{
			label: m.nav_adminJobs,
			icon: Clock,
			action: () => {
				close();
				goto(resolve('/admin/jobs'));
			},
			permission: Permissions.Jobs.View
		},
		{
			label: m.nav_adminOAuthProviders,
			icon: KeyRound,
			action: () => {
				close();
				goto(resolve('/admin/oauth-providers'));
			},
			permission: Permissions.OAuthProviders.View
		}
	];
	// @init:end

	const actionItems: CommandItem[] = [
		{
			label: m.commandPalette_toggleTheme,
			icon: Sun,
			action: () => {
				toggleTheme();
				close();
			}
			// @init:if auth=jwt,keycloak
		},
		{
			label: m.nav_logout,
			icon: LogOut,
			action: () => {
				close();
				logout();
			},
			shortcut: getShortcutSymbol(ShortcutAction.Logout)
			// @init:end
		}
	];
	// @init:if auth=jwt

	let visibleAdminItems = $derived(
		adminItems.filter((item) => hasPermission(user, item.permission))
	);
	// @init:end
</script>

<Command.Dialog
	bind:open={shortcutsState.isCommandPaletteOpen}
	title={m.shortcuts_commandPalette()}
	description={m.commandPalette_placeholder()}
>
	<Command.Input placeholder={m.commandPalette_placeholder()} />
	<Command.List>
		<Command.Empty>{m.commandPalette_noResults()}</Command.Empty>

		<Command.Group heading={m.commandPalette_navigation()}>
			{#each navItems as item (item.label())}
				<Command.Item onSelect={item.action}>
					<item.icon />
					<span>{item.label()}</span>
					{#if item.shortcut}
						<Command.Shortcut>{item.shortcut}</Command.Shortcut>
					{/if}
				</Command.Item>
			{/each}
		</Command.Group>

		<!-- @init:if auth=jwt -->
		{#if visibleAdminItems.length > 0}
			<Command.Group heading={m.commandPalette_admin()}>
				{#each visibleAdminItems as item (item.label())}
					<Command.Item onSelect={item.action}>
						<item.icon />
						<span>{item.label()}</span>
					</Command.Item>
				{/each}
			</Command.Group>
		{/if}
		<!-- @init:end -->

		<Command.Separator />

		<Command.Group heading={m.commandPalette_actions()}>
			{#each actionItems as item (item.label())}
				<Command.Item onSelect={item.action}>
					<item.icon />
					<span>{item.label()}</span>
					{#if item.shortcut}
						<Command.Shortcut>{item.shortcut}</Command.Shortcut>
					{/if}
				</Command.Item>
			{/each}
		</Command.Group>
	</Command.List>
</Command.Dialog>
