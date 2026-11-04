// @init:if auth=jwt,keycloak
export { createApiClient, browserClient, initBrowserAuth } from './client';
// @init:else uncomment
// export { createApiClient, browserClient } from './client';
// @init:end
export * from './error-handling';
export * from './mutation';

// backend-monitor.ts is intentionally excluded - it is client-only (uses setInterval,
// browser fetch) and must not be imported in .server.ts files.
