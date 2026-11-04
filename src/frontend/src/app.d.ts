// @init:if auth=jwt,keycloak
import type { components } from '$lib/api/v1';

// @init:end
declare global {
	namespace App {
		interface Locals {
			// @init:if auth=jwt,keycloak
			user: components['schemas']['UserResponse'] | null;
			// @init:end
			locale: string;
		}
	}
	// @init:if auth=jwt

	interface Window {
		turnstile?: {
			render: (container: HTMLElement, options: Record<string, unknown>) => string;
			reset: (widgetId: string) => void;
			remove: (widgetId: string) => void;
		};
	}
	// @init:end
}

export {};
