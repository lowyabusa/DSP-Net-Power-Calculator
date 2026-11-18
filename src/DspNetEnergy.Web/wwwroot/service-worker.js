// Development uses fresh responses. Offline behavior is enabled in the published build.
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", event => event.waitUntil(self.clients.claim()));
