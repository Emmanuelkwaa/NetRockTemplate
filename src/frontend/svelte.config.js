import adapter from '@sveltejs/adapter-node';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	preprocess: vitePreprocess(),

	kit: {
		adapter: adapter(),

		csp: {
			mode: 'nonce',
			directives: {
				'default-src': ['self'],
				// @init:if auth=jwt
				'script-src': ['self', 'nonce', 'https://challenges.cloudflare.com'],
				// @init:else uncomment
				// 'script-src': ['self', 'nonce'],
				// @init:end
				'style-src': ['self', 'unsafe-inline'],
				'img-src': ['self', 'https:', 'data:', 'blob:'],
				'font-src': ['self'],
				'connect-src': ['self'],
				'worker-src': ['self', 'blob:'],
				// @init:if auth=jwt
				'frame-src': ['https://challenges.cloudflare.com'],
				// @init:end
				'frame-ancestors': ['none'],
				'base-uri': ['self'],
				'form-action': ['self'],
				'object-src': ['none']
			}
		}
	}
};

export default config;
