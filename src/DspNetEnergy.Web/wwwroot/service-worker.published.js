self.importScripts("./service-worker-assets.js");
const base = self.registration.scope;
const prefix = "dsp-net-energy:" + base + ":";
const cacheName = prefix + self.assetsManifest.version;
const assets = self.assetsManifest.assets.filter(asset =>
    !/^(?:service-worker(?:-assets)?\.js|\.nojekyll)$/.test(asset.url) && !/\.(?:br|gz)$/.test(asset.url));

self.addEventListener("install", event => {
    event.waitUntil((async () => {
        const cache = await caches.open(cacheName);
        await cache.addAll(assets.map(asset => new Request(new URL(asset.url, base), {
            integrity: asset.hash,
            cache: "reload",
        })));
    })());
});

self.addEventListener("activate", event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys.filter(key => key.startsWith(prefix) && key !== cacheName).map(key => caches.delete(key)));
        await self.clients.claim();
    })());
});

self.addEventListener("fetch", event => {
    if (event.request.method !== "GET" || !event.request.url.startsWith(base)) return;
    event.respondWith((async () => {
        const cache = await caches.open(cacheName);
        const path = new URL(event.request.url);
        const entry = event.request.mode === "navigate" && path.pathname === new URL(base).pathname
            ? new URL("index.html", base) : event.request;
        return await cache.match(entry) ?? fetch(event.request);
    })());
});
